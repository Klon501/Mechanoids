using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AIDominusDuelFight : JobGiver_AIFightEnemies
    {
        private const int CastJobExpiryTicks = 120;
        private const int ApproachJobExpiryTicks = 60;

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!DuelUtility.CanStartDuel(pawn))
            {
                return null;
            }

            Ability ability = DuelUtility.GetDuelAbility(pawn);
            if (ability == null || !ability.CanCast || ability.verb == null)
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState?.enemyTarget;
            if (!DuelUtility.IsValidAIDuelTarget(pawn, enemyTarget, ability))
            {
                return null;
            }

            LocalTargetInfo target = enemyTarget;
            if (!ability.AICanTargetNow(target))
            {
                return null;
            }

            if (ability.verb.CanHitTarget(target))
            {
                pawn.pather?.StopDead();
                Job castJob = ability.GetJob(target, target);
                castJob.expiryInterval = CastJobExpiryTicks;
                castJob.checkOverrideOnExpire = true;
                return castJob;
            }

            if (!TryFindDuelCastPosition(pawn, enemyTarget, ability, out IntVec3 dest))
            {
                return null;
            }

            if (dest == pawn.Position)
            {
                pawn.pather?.StopDead();
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, ApproachJobExpiryTicks, checkOverrideOnExpiry: true);
            }

            Job approachJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
            approachJob.expiryInterval = ApproachJobExpiryTicks;
            approachJob.checkOverrideOnExpire = true;
            approachJob.canBashDoors = true;
            return approachJob;
        }

        public override Thing FindAttackTarget(Pawn pawn)
        {
            if (!DuelUtility.CanStartDuel(pawn))
            {
                return null;
            }

            return base.FindAttackTarget(pawn);
        }

        public override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            return base.ExtraTargetValidator(pawn, target)
                && DuelUtility.IsValidAIDuelTarget(pawn, target, DuelUtility.GetDuelAbility(pawn));
        }

        public override bool ShouldLoseTarget(Pawn pawn)
        {
            if (!DuelUtility.CanStartDuel(pawn))
            {
                return true;
            }

            Thing enemyTarget = pawn.mindState?.enemyTarget;
            if (!DuelUtility.IsValidAIDuelTarget(pawn, enemyTarget, DuelUtility.GetDuelAbility(pawn)))
            {
                return true;
            }

            return base.ShouldLoseTarget(pawn);
        }

        private static bool TryFindDuelCastPosition(Pawn pawn, Thing enemyTarget, Ability ability, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            if (pawn == null || enemyTarget == null || ability?.verb == null)
            {
                return false;
            }

            return CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = ability.verb,
                maxRangeFromTarget = ability.verb.EffectiveRange,
                wantCoverFromTarget = false
            }, out dest) && dest.IsValid;
        }
    }

    internal static class DuelUtility
    {
        public static Ability GetDuelAbility(Pawn pawn)
        {
            if (pawn?.abilities == null)
            {
                return null;
            }

            Ability bossAbility = pawn.abilities.GetAbility(ApexDefsOf.APM_Mech_Duel_Boss);
            if (bossAbility != null)
            {
                return bossAbility;
            }

            return pawn.abilities.GetAbility(ApexDefsOf.APM_Mech_Duel);
        }

        public static bool CanStartDuel(Pawn pawn)
        {
            return IsValidDuelCaster(pawn) && pawn.CurJob?.ability == null;
        }

        public static bool IsValidDuelCaster(Pawn pawn)
        {
            return IsBasicSpawnedPawn(pawn)
                && !pawn.Downed
                && Utils.IsAwakeAndNotDormant(pawn)
                && !IsInDuel(pawn);
        }

        public static bool IsDominus(Pawn pawn)
        {
            return pawn != null && pawn.def == ApexDefsOf.APM_Mech_Dominus;
        }

        public static bool IsValidAIDuelTarget(Pawn caster, Thing target, Ability ability)
        {
            if (ability == null || !ability.CanCast)
            {
                return false;
            }

            if (!IsValidDuelTargetForAbility(caster, target as Pawn, requireHostile: true))
            {
                return false;
            }

            Pawn targetPawn = (Pawn)target;
            if (targetPawn.IsPsychologicallyInvisible())
            {
                return false;
            }

            return !(targetPawn is IAttackTarget attackTarget) || !attackTarget.ThreatDisabled(caster);
        }

        public static bool IsValidDuelTargetForAbility(Pawn caster, Pawn targetPawn, bool requireHostile)
        {
            if (!IsValidDuelCaster(caster) || !IsBasicSpawnedPawn(targetPawn))
            {
                return false;
            }

            if (targetPawn == caster || targetPawn.Map != caster.Map || targetPawn.Downed)
            {
                return false;
            }

            if (requireHostile && !targetPawn.HostileTo(caster))
            {
                return false;
            }

            return !IsInDuel(targetPawn) && !HasPendingDuelFor(caster, targetPawn);
        }

        public static bool IsValidActiveDuelOpponent(Pawn pawn, Thing target)
        {
            if (!IsBasicSpawnedPawn(pawn) || pawn.Downed)
            {
                return false;
            }

            Pawn targetPawn = target as Pawn;
            return IsBasicSpawnedPawn(targetPawn)
                && targetPawn != pawn
                && targetPawn.Map == pawn.Map
                && !targetPawn.Downed;
        }

        public static bool IsInDuel(Pawn pawn)
        {
            return pawn != null
                && (pawn.MentalState is MentalState_Duel
                    || pawn.health?.hediffSet?.GetFirstHediffOfDef(ApexDefsOf.APM_InDuel) != null);
        }

        private static bool HasPendingDuelFor(Pawn caster, Pawn targetPawn)
        {
            if (!IsBasicSpawnedPawn(caster) || !IsBasicSpawnedPawn(targetPawn) || caster.Map != targetPawn.Map)
            {
                return false;
            }

            foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn == caster)
                {
                    continue;
                }

                Job job = pawn.CurJob;
                if (!IsDuelAbility(job?.ability?.def))
                {
                    continue;
                }

                if (JobTargetsPawn(job, caster) || JobTargetsPawn(job, targetPawn) || pawn == targetPawn)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDuelAbility(AbilityDef abilityDef)
        {
            return abilityDef == ApexDefsOf.APM_Mech_Duel || abilityDef == ApexDefsOf.APM_Mech_Duel_Boss;
        }

        private static bool JobTargetsPawn(Job job, Pawn pawn)
        {
            return pawn != null
                && (job.targetA.Pawn == pawn || job.targetB.Pawn == pawn || job.targetC.Pawn == pawn);
        }

        private static bool IsBasicSpawnedPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && pawn.Spawned
                && pawn.Map != null;
        }
    }
}
