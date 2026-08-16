using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AICelerusAbilityFight : JobGiver_AIFightEnemy
    {
        private const int AbilityJobExpiryTicks = 120;
        private const int MeleeJobExpiryTicks = 240;
        private const int SmokeApproachExpiryTicks = 180;
        private const int ShortWaitTicks = 30;

        public bool allowPlayerControlled = false;
        public float blinkMinDistance = 2f;
        public float smokeCheckRadius = 5f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AICelerusAbilityFight obj = (JobGiver_AICelerusAbilityFight)base.DeepCopy(resolve);
            obj.allowPlayerControlled = allowPlayerControlled;
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

            CompCelerusRaidController controller = pawn.GetComp<CompCelerusRaidController>();
            if (controller == null)
            {
                return null;
            }

            Thing enemyTarget = ResolveRaidTarget(pawn, controller);
            if (!IsValidEnemy(pawn, enemyTarget))
            {
                controller.ResetRaid();
                return null;
            }

            if (controller.Phase == CelerusRaidPhase.Retreat || controller.Phase == CelerusRaidPhase.CooldownWait)
            {
                controller.ResetRaid();
            }

            if (controller.Phase == CelerusRaidPhase.SmokeThrown)
            {
                if (SmokeReadyForStrike(pawn, enemyTarget, controller))
                {
                    Job blinkJob = TryGetBlinkInJob(pawn, enemyTarget, controller);
                    if (blinkJob != null)
                    {
                        return blinkJob;
                    }

                    return TryGetMeleeJob(pawn, enemyTarget, controller);
                }

                if (!controller.PhaseExpired)
                {
                    return MakeWaitJob(ShortWaitTicks);
                }

                controller.ResetRaid();
            }

            if (!HasCelerusSmokeNear(enemyTarget.Position, pawn.Map, smokeCheckRadius))
            {
                Job smokeJob = TryGetSmokescreenJob(pawn, enemyTarget, controller);
                if (smokeJob != null)
                {
                    return smokeJob;
                }

                Job smokeApproachJob = TryGetSmokeApproachJob(pawn, enemyTarget, controller);
                if (smokeApproachJob != null)
                {
                    return smokeApproachJob;
                }
            }

            Job blinkInJob = TryGetBlinkInJob(pawn, enemyTarget, controller);
            if (blinkInJob != null)
            {
                return blinkInJob;
            }

            return TryGetMeleeJob(pawn, enemyTarget, controller);
        }

        private Thing ResolveRaidTarget(Pawn pawn, CompCelerusRaidController controller)
        {
            if (controller.Phase != CelerusRaidPhase.Ready)
            {
                Thing storedTarget = controller.RaidTarget;
                if (IsValidEnemy(pawn, storedTarget))
                {
                    pawn.mindState.enemyTarget = storedTarget;
                    return storedTarget;
                }

                controller.ResetRaid();
            }

            UpdateEnemyTarget(pawn);
            return pawn.mindState.enemyTarget;
        }

        private Job TryGetSmokescreenJob(Pawn pawn, Thing enemyTarget, CompCelerusRaidController controller)
        {
            Ability smokeAbility = GetSmokescreenAbility(pawn);
            if (smokeAbility == null || !smokeAbility.CanCast)
            {
                return null;
            }

            if (!TryFindSmokeTargetCell(pawn, enemyTarget, smokeAbility, controller, requireCanHitTarget: true, out IntVec3 smokeCell))
            {
                return null;
            }

            LocalTargetInfo target = smokeCell;
            Job job = smokeAbility.GetJob(target, target);
            job.expiryInterval = AbilityJobExpiryTicks;
            job.checkOverrideOnExpire = true;
            controller.StartSmoke(enemyTarget, smokeCell);
            return job;
        }

        private Job TryGetSmokeApproachJob(Pawn pawn, Thing enemyTarget, CompCelerusRaidController controller)
        {
            Ability smokeAbility = GetSmokescreenAbility(pawn);
            if (smokeAbility == null || !smokeAbility.CanCast)
            {
                return null;
            }

            if (!TryFindSmokeTargetCell(pawn, enemyTarget, smokeAbility, controller, requireCanHitTarget: false, out IntVec3 smokeCell))
            {
                return null;
            }

            if (!TryFindSmokeCastPosition(pawn, enemyTarget, smokeAbility, smokeCell, out IntVec3 destination))
            {
                return null;
            }

            if (!destination.IsValid || destination == pawn.Position)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, destination);
            job.expiryInterval = SmokeApproachExpiryTicks;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            job.collideWithPawns = true;
            return job;
        }

        private Job TryGetBlinkInJob(Pawn pawn, Thing enemyTarget, CompCelerusRaidController controller)
        {
            Ability ability = GetBlinkAbility(pawn);
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
            job.expiryInterval = AbilityJobExpiryTicks;
            job.checkOverrideOnExpire = true;
            controller.StartStrike(enemyTarget);
            return job;
        }

        private Job TryGetMeleeJob(Pawn pawn, Thing enemyTarget, CompCelerusRaidController controller)
        {
            if (!CanMeleeTarget(pawn, enemyTarget))
            {
                return null;
            }

            controller.StartStrike(enemyTarget);
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, enemyTarget);
            job.expiryInterval = MeleeJobExpiryTicks;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            return job;
        }

        private bool SmokeReadyForStrike(Pawn pawn, Thing enemyTarget, CompCelerusRaidController controller)
        {
            if (HasCelerusSmokeNear(enemyTarget.Position, pawn.Map, smokeCheckRadius))
            {
                return true;
            }

            return controller.SmokeCell.IsValid && HasCelerusSmokeNear(controller.SmokeCell, pawn.Map, smokeCheckRadius);
        }

        private bool TryFindSmokeTargetCell(Pawn pawn, Thing enemyTarget, Ability ability, CompCelerusRaidController controller, bool requireCanHitTarget, out IntVec3 smokeCell)
        {
            smokeCell = IntVec3.Invalid;
            int bestScore = int.MinValue;

            TryScoreSmokeCell(pawn, enemyTarget, ability, controller, requireCanHitTarget, enemyTarget.Position, ref smokeCell, ref bestScore);
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(enemyTarget))
            {
                TryScoreSmokeCell(pawn, enemyTarget, ability, controller, requireCanHitTarget, cell, ref smokeCell, ref bestScore);
            }

            return smokeCell.IsValid;
        }

        private void TryScoreSmokeCell(Pawn pawn, Thing enemyTarget, Ability ability, CompCelerusRaidController controller, bool requireCanHitTarget, IntVec3 cell, ref IntVec3 bestCell, ref int bestScore)
        {
            if (!cell.InBounds(pawn.Map) || !cell.Walkable(pawn.Map) || SmokeWouldHitProtectedAlly(pawn, cell, controller.Props.smokeRadius))
            {
                return;
            }

            LocalTargetInfo target = cell;
            if (!CanUseSmokeTarget(pawn, ability, target, requireCanHitTarget))
            {
                return;
            }

            int score = CountHostilesInRadius(pawn, cell, controller.Props.smokeRadius) * 100;
            score -= cell.DistanceToSquared(enemyTarget.Position);
            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        private static bool TryFindSmokeCastPosition(Pawn pawn, Thing enemyTarget, Ability ability, IntVec3 smokeCell, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            if (pawn == null || enemyTarget == null || ability?.verb == null || !smokeCell.IsValid)
            {
                return false;
            }

            LocalTargetInfo target = smokeCell;
            return CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = ability.verb,
                maxRangeFromTarget = ability.verb.EffectiveRange,
                wantCoverFromTarget = false,
                preferredCastPosition = pawn.Position,
                validator = cell => CanStandAt(pawn, cell) && ability.verb.CanHitTargetFrom(cell, target)
            }, out destination) && destination.IsValid;
        }

        private static bool CanUseSmokeTarget(Pawn pawn, Ability ability, LocalTargetInfo target, bool requireCanHitTarget)
        {
            if (ability?.verb == null || !target.IsValid || !ability.CanApplyOn(target) || !ability.verb.verbProps.targetParams.CanTarget(target.ToTargetInfo(pawn.Map), ability.verb))
            {
                return false;
            }

            return !requireCanHitTarget || ability.verb.CanHitTarget(target);
        }

        private bool TryFindBlinkDestination(Pawn pawn, Thing enemyTarget, Ability ability, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            IntVec3 fallback = IntVec3.Invalid;
            int bestSmokeDistance = int.MaxValue;
            int bestFallbackDistance = int.MaxValue;

            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(enemyTarget))
            {
                if (!CanBlinkTo(pawn, ability, cell))
                {
                    continue;
                }

                int distance = pawn.Position.DistanceToSquared(cell);
                if (HasCelerusSmokeNear(cell, pawn.Map, smokeCheckRadius))
                {
                    if (distance < bestSmokeDistance)
                    {
                        bestSmokeDistance = distance;
                        destination = cell;
                    }
                }
                else if (distance < bestFallbackDistance)
                {
                    bestFallbackDistance = distance;
                    fallback = cell;
                }
            }

            if (destination.IsValid)
            {
                return true;
            }

            destination = fallback;
            return destination.IsValid;
        }

        private static bool CanBlinkTo(Pawn pawn, Ability ability, IntVec3 cell)
        {
            if (!CanStandAt(pawn, cell))
            {
                return false;
            }

            LocalTargetInfo target = cell;
            return ability.AICanTargetNow(target) && ability.verb.CanHitTarget(target);
        }

        private static bool CanStandAt(Pawn pawn, IntVec3 cell)
        {
            if (!cell.InBounds(pawn.Map) || !cell.WalkableBy(pawn.Map, pawn))
            {
                return false;
            }

            Pawn blockingPawn = cell.GetFirstPawn(pawn.Map);
            return blockingPawn == null || blockingPawn == pawn;
        }

        private static bool CanMeleeTarget(Pawn pawn, Thing enemyTarget)
        {
            return pawn.CanReserveAndReach(enemyTarget, PathEndMode.Touch, Danger.Deadly);
        }

        private static Job MakeWaitJob(int ticks)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            job.expiryInterval = ticks;
            job.checkOverrideOnExpire = true;
            return job;
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

        private static bool SmokeWouldHitProtectedAlly(Pawn pawn, IntVec3 center, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                foreach (Thing thing in cell.GetThingList(pawn.Map))
                {
                    Pawn otherPawn = thing as Pawn;
                    if (otherPawn != null && otherPawn != pawn && !otherPawn.Dead && !otherPawn.HostileTo(pawn) && !CelerusRaidUtility.IsCelerus(otherPawn))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountHostilesInRadius(Pawn pawn, IntVec3 center, float radius)
        {
            int count = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
            {
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                foreach (Thing thing in cell.GetThingList(pawn.Map))
                {
                    Pawn targetPawn = thing as Pawn;
                    if (targetPawn != null && IsValidEnemy(pawn, targetPawn))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static Ability GetBlinkAbility(Pawn pawn)
        {
            return pawn.abilities.GetAbility(ApexDefsOf.APM_CelerusBlink);
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

        private bool CanRunFor(Pawn pawn)
        {
            return pawn != null
                && Utils.CanRunAutonomousPawn(pawn)
                && CelerusRaidUtility.IsCelerus(pawn)
                && pawn.abilities != null
                && pawn.CurJob?.ability == null
                && (!pawn.IsPlayerControlled || allowPlayerControlled);
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

    internal static class CelerusRaidUtility
    {
        public static bool IsCelerus(Pawn pawn)
        {
            return pawn?.def == ApexDefsOf.APM_Mech_Celerus || pawn?.def == ApexDefsOf.APM_Mech_CelerusB;
        }

        public static bool IsCelerusAbility(AbilityDef def)
        {
            return def == ApexDefsOf.APM_CelerusBlink
                || def == ApexDefsOf.APM_Ability_SmokeScreen
                || def == ApexDefsOf.APM_Ability_SmokeScreen_Boss;
        }
    }
}
