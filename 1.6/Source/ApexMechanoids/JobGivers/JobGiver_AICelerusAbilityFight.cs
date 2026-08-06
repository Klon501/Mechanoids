using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AICelerusAbilityFight : JobGiver_AIFightEnemy
    {
        public float blinkMinDistance = 2f;
        public float smokeCheckRadius = 5f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AICelerusAbilityFight obj = (JobGiver_AICelerusAbilityFight)base.DeepCopy(resolve);
            obj.blinkMinDistance = blinkMinDistance;
            obj.smokeCheckRadius = smokeCheckRadius;
            return obj;
        }

        public override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            dest = IntVec3.Invalid;
            return false;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (!IsValidEnemy(pawn, enemyTarget))
            {
                return null;
            }

            Job smokeJob = TryGetSmokescreenJob(pawn, enemyTarget);
            if (smokeJob != null)
            {
                return smokeJob;
            }

            return TryGetBlinkJob(pawn, enemyTarget);
        }

        private Job TryGetSmokescreenJob(Pawn pawn, Thing enemyTarget)
        {
            Ability ability = GetSmokescreenAbility(pawn);
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            LocalTargetInfo target = enemyTarget;
            if (!ability.AICanTargetNow(target) || !ability.verb.CanHitTarget(target))
            {
                return null;
            }

            Job job = ability.GetJob(target, target);
            job.expiryInterval = 120;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private Job TryGetBlinkJob(Pawn pawn, Thing enemyTarget)
        {
            Ability ability = pawn.abilities.GetAbility(ApexDefsOf.APM_CelerusBlink);
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(enemyTarget.Position) <= blinkMinDistance)
            {
                return null;
            }

            if (!TryFindBlinkDestination(pawn, enemyTarget, ability, out IntVec3 destination))
            {
                return null;
            }

            LocalTargetInfo target = destination;
            Job job = ability.GetJob(target, target);
            job.expiryInterval = 120;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private bool TryFindBlinkDestination(Pawn pawn, Thing enemyTarget, Ability ability, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            IntVec3 bestFallback = IntVec3.Invalid;
            int bestFallbackDistance = int.MaxValue;

            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(enemyTarget))
            {
                if (!CanBlinkTo(pawn, ability, cell))
                {
                    continue;
                }

                if (HasCelerusSmokeNear(cell, pawn.Map, smokeCheckRadius))
                {
                    destination = cell;
                    return true;
                }

                int distance = pawn.Position.DistanceToSquared(cell);
                if (distance < bestFallbackDistance)
                {
                    bestFallbackDistance = distance;
                    bestFallback = cell;
                }
            }

            if (bestFallback.IsValid)
            {
                destination = bestFallback;
                return true;
            }

            return false;
        }

        private static bool CanBlinkTo(Pawn pawn, Ability ability, IntVec3 cell)
        {
            if (!cell.InBounds(pawn.Map) || !cell.WalkableBy(pawn.Map, pawn) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                return false;
            }

            LocalTargetInfo target = cell;
            return ability.AICanTargetNow(target) && ability.verb.CanHitTarget(target);
        }

        private static bool HasCelerusSmokeNear(IntVec3 center, Map map, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                Thing gas = cell.GetGas(map);
                if (gas != null && (gas.def == ApexDefsOf.APM_Smokescreen || gas.def == ApexDefsOf.APM_Smokescreen_Boss))
                {
                    return true;
                }
            }

            return false;
        }

        private static Ability GetSmokescreenAbility(Pawn pawn)
        {
            Ability bossAbility = pawn.abilities.GetAbility(ApexDefsOf.APM_Ability_SmokeScreen_Boss);
            if (bossAbility != null)
            {
                return bossAbility;
            }

            return pawn.abilities.GetAbility(ApexDefsOf.APM_Ability_SmokeScreen);
        }

        private static bool CanRunFor(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && pawn.Map != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Awake()
                && !pawn.IsPlayerControlled
                && (pawn.def == ApexDefsOf.APM_Mech_Celerus || pawn.def == ApexDefsOf.APM_Mech_CelerusB)
                && pawn.abilities != null
                && pawn.CurJob?.ability == null;
        }

        private static bool IsValidEnemy(Pawn pawn, Thing enemyTarget)
        {
            if (enemyTarget == null || enemyTarget.Destroyed || !enemyTarget.Spawned || enemyTarget.Map != pawn.Map || !enemyTarget.HostileTo(pawn))
            {
                return false;
            }

            if (enemyTarget is Pawn targetPawn)
            {
                if (targetPawn.Dead || targetPawn.IsPsychologicallyInvisible())
                {
                    return false;
                }

                if (targetPawn is IAttackTarget attackTarget && attackTarget.ThreatDisabled(pawn))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
