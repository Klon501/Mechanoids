using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ApexMechanoids
{
    public class JobGiver_AITinkerCombat : ThinkNode_JobGiver
    {
        public float targetAcquireRadius = 30f;
        public float targetKeepRadius = 35f;
        public float fleeEnemyDistance = 7.9f;
        public int fleeDistance = 16;
        public float shieldSearchRadius = 11f;
        public float repairSearchRadius = 18f;
        public float repairTargetMinEnemyDistance = 8f;
        public float maxRepositionDistance = 12f;
        public bool requireLineOfSightToTargets = true;
        public int abilityJobExpiryInterval = 120;
        public int moveJobExpiryInterval = 120;
        public int repairJobExpiryInterval = 120;
        public int waitJobExpiryInterval = 60;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AITinkerCombat obj = (JobGiver_AITinkerCombat)base.DeepCopy(resolve);
            obj.targetAcquireRadius = targetAcquireRadius;
            obj.targetKeepRadius = targetKeepRadius;
            obj.fleeEnemyDistance = fleeEnemyDistance;
            obj.fleeDistance = fleeDistance;
            obj.shieldSearchRadius = shieldSearchRadius;
            obj.repairSearchRadius = repairSearchRadius;
            obj.repairTargetMinEnemyDistance = repairTargetMinEnemyDistance;
            obj.maxRepositionDistance = maxRepositionDistance;
            obj.requireLineOfSightToTargets = requireLineOfSightToTargets;
            obj.abilityJobExpiryInterval = abilityJobExpiryInterval;
            obj.moveJobExpiryInterval = moveJobExpiryInterval;
            obj.repairJobExpiryInterval = repairJobExpiryInterval;
            obj.waitJobExpiryInterval = waitJobExpiryInterval;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return null;
            }

            Job fleeJob = TryGetFleeJob(pawn);
            if (fleeJob != null)
            {
                return fleeJob;
            }

            Thing enemyTarget = GetEnemyTarget(pawn);
            if (enemyTarget == null)
            {
                return null;
            }

            Ability blindingLaser = pawn.abilities.GetAbility(ApexDefsOf.APM_BlindingLaser);
            Ability defenceMatrix = pawn.abilities.GetAbility(ApexDefsOf.APM_DefenceMatrix);

            Job blindJob = TryGetBlindingJob(pawn, blindingLaser, enemyTarget);
            if (blindJob != null)
            {
                return blindJob;
            }

            Job shieldJob = TryGetShieldJob(pawn, defenceMatrix, enemyTarget);
            if (shieldJob != null)
            {
                return shieldJob;
            }

            Job repairJob = TryGetCombatRepairJob(pawn, enemyTarget);
            if (repairJob != null)
            {
                return repairJob;
            }

            Job positionJob = TryGetBlindingPositionJob(pawn, blindingLaser, enemyTarget);
            if (positionJob != null)
            {
                return positionJob;
            }

            return MakeWaitCombatJob(pawn);
        }

        private static bool CanRunFor(Pawn pawn)
        {
            return pawn != null
                && pawn.def == ApexDefsOf.APM_Mech_Tinker
                && pawn.Spawned
                && pawn.Map != null
                && pawn.Faction != null
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && !pawn.IsPlayerControlled
                && pawn.abilities != null
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        private Thing GetEnemyTarget(Pawn pawn)
        {
            Thing enemyTarget = pawn.mindState?.enemyTarget;
            if (IsCombatEnemyTarget(pawn, enemyTarget, targetKeepRadius))
            {
                return enemyTarget;
            }

            enemyTarget = FindEnemyTarget(pawn, targetAcquireRadius);
            if (pawn.mindState != null)
            {
                pawn.mindState.enemyTarget = enemyTarget;
                if (enemyTarget != null)
                {
                    pawn.mindState.Notify_EngagedTarget();
                    pawn.GetLord()?.Notify_PawnAcquiredTarget(pawn, enemyTarget);
                }
            }

            return enemyTarget;
        }

        private Thing FindEnemyTarget(Pawn pawn, float maxDistance)
        {
            Thing bestTarget = null;
            float bestScore = float.MinValue;
            float maxDistanceSq = maxDistance * maxDistance;
            List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                IAttackTarget attackTarget = potentialTargets[i];
                Thing target = attackTarget.Thing;
                if (!IsCombatEnemyTarget(pawn, target, maxDistance) || !AttackTargetFinder.IsAutoTargetable(attackTarget))
                {
                    continue;
                }

                float distanceSq = pawn.Position.DistanceToSquared(target.Position);
                if (distanceSq > maxDistanceSq || !pawn.CanReach(target, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                float score = 10000f - distanceSq;
                if (target is Pawn targetPawn && targetPawn.RaceProps.Humanlike)
                {
                    score += 300f;
                }

                if (bestTarget == null || score > bestScore)
                {
                    bestTarget = target;
                    bestScore = score;
                }
            }

            return bestTarget;
        }

        private Job TryGetFleeJob(Pawn pawn)
        {
            if (!GenAI.EnemyIsNear(pawn, fleeEnemyDistance, out Thing threat, meleeOnly: false, requireLos: true) || !IsValidEnemyTarget(pawn, threat))
            {
                return null;
            }

            Job job = FleeUtility.FleeJob(pawn, threat, fleeDistance);
            if (job == null)
            {
                return null;
            }

            job.expiryInterval = waitJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            return job;
        }

        private Job TryGetBlindingJob(Pawn pawn, Ability ability, Thing enemyTarget)
        {
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            Pawn target = FindBlindingTarget(pawn, ability, enemyTarget, requireCanHit: true);
            if (target == null)
            {
                return null;
            }

            LocalTargetInfo targetInfo = target;
            Job job = ability.GetJob(targetInfo, targetInfo);
            job.expiryInterval = abilityJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private Pawn FindBlindingTarget(Pawn pawn, Ability ability, Thing preferredTarget, bool requireCanHit)
        {
            Pawn bestTarget = null;
            float bestScore = float.MinValue;
            float maxDistanceSq = targetAcquireRadius * targetAcquireRadius;
            List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                Pawn candidate = potentialTargets[i].Thing as Pawn;
                if (candidate == null || !IsCombatEnemyTarget(pawn, candidate, targetAcquireRadius))
                {
                    continue;
                }

                float distanceSq = pawn.Position.DistanceToSquared(candidate.Position);
                if (distanceSq > maxDistanceSq || !CanUseBlindingOn(ability, candidate, requireCanHit))
                {
                    continue;
                }

                float score = 10000f - distanceSq;
                if (candidate == preferredTarget)
                {
                    score += 500f;
                }
                if (candidate.IsAttacking())
                {
                    score += 200f;
                }

                if (bestTarget == null || score > bestScore)
                {
                    bestTarget = candidate;
                    bestScore = score;
                }
            }

            return bestTarget;
        }

        private static bool CanUseBlindingOn(Ability ability, Pawn target, bool requireCanHit)
        {
            LocalTargetInfo targetInfo = target;
            if (!ability.def.verbProperties.targetParams.CanTarget(target) || !ability.CanApplyOn(targetInfo))
            {
                return false;
            }

            if (requireCanHit)
            {
                return ability.AICanTargetNow(targetInfo) && ability.verb.CanHitTarget(targetInfo);
            }

            return true;
        }

        private Job TryGetShieldJob(Pawn pawn, Ability ability, Thing enemyTarget)
        {
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            Pawn target = FindShieldTarget(pawn, ability, enemyTarget);
            if (target == null)
            {
                return null;
            }

            LocalTargetInfo targetInfo = target;
            return ability.GetJob(targetInfo, targetInfo);
        }

        private Pawn FindShieldTarget(Pawn pawn, Ability ability, Thing enemyTarget)
        {
            Pawn bestTarget = null;
            float bestScore = float.MinValue;
            float maxDistanceSq = shieldSearchRadius * shieldSearchRadius;
            List<Pawn> factionPawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < factionPawns.Count; i++)
            {
                Pawn candidate = factionPawns[i];
                if (!CanShieldTarget(pawn, candidate, ability))
                {
                    continue;
                }

                float distanceSq = pawn.Position.DistanceToSquared(candidate.Position);
                if (distanceSq > maxDistanceSq)
                {
                    continue;
                }

                float score = 10000f - distanceSq;
                if (candidate.def != ApexDefsOf.APM_Mech_Tinker)
                {
                    score += 400f;
                }
                if (candidate.IsAttacking())
                {
                    score += 250f;
                }
                if (candidate.Position.InHorDistOf(enemyTarget.Position, 15f))
                {
                    score += 150f;
                }
                if (candidate.RaceProps != null && candidate.RaceProps.IsMechanoid && MechRepairUtility.CanRepair(candidate))
                {
                    score += 100f;
                }

                if (bestTarget == null || score > bestScore)
                {
                    bestTarget = candidate;
                    bestScore = score;
                }
            }

            return bestTarget;
        }

        private static bool CanShieldTarget(Pawn pawn, Pawn target, Ability ability)
        {
            if (target == null || target == pawn || target.Destroyed || target.Dead || target.Downed || !target.Spawned || target.Map != pawn.Map)
            {
                return false;
            }

            if (target.Faction != pawn.Faction || target.HostileTo(pawn) || target.InAggroMentalState)
            {
                return false;
            }

            if (HasActiveShield(target))
            {
                return false;
            }

            LocalTargetInfo targetInfo = target;
            return ability.def.verbProperties.targetParams.CanTarget(target)
                && ability.CanApplyOn(targetInfo)
                && ability.AICanTargetNow(targetInfo)
                && ability.verb.CanHitTarget(targetInfo);
        }

        private Job TryGetCombatRepairJob(Pawn pawn, Thing enemyTarget)
        {
            Pawn target = FindCombatRepairTarget(pawn, enemyTarget);
            if (target == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.RepairMech, target);
            job.expiryInterval = repairJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            return job;
        }

        private Pawn FindCombatRepairTarget(Pawn pawn, Thing enemyTarget)
        {
            Pawn bestTarget = null;
            float bestScore = float.MinValue;
            float maxDistanceSq = repairSearchRadius * repairSearchRadius;
            List<Pawn> factionPawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < factionPawns.Count; i++)
            {
                Pawn candidate = factionPawns[i];
                if (!CanRepairCombatMechNow(pawn, candidate, enemyTarget))
                {
                    continue;
                }

                float distanceToTinkerSq = pawn.Position.DistanceToSquared(candidate.Position);
                if (distanceToTinkerSq > maxDistanceSq)
                {
                    continue;
                }

                float missingHealth = 1f - candidate.health.summaryHealth.SummaryHealthPercent;
                float distanceFromEnemySq = enemyTarget != null ? candidate.Position.DistanceToSquared(enemyTarget.Position) : 0f;
                float score = missingHealth * 1200f - distanceToTinkerSq + distanceFromEnemySq * 0.25f;
                if (candidate.Downed)
                {
                    score += 150f;
                }

                if (bestTarget == null || score > bestScore)
                {
                    bestTarget = candidate;
                    bestScore = score;
                }
            }

            return bestTarget;
        }

        private bool CanRepairCombatMechNow(Pawn pawn, Pawn target, Thing enemyTarget)
        {
            if (!ModsConfig.BiotechActive || target == null || target == pawn || target.Destroyed || target.Dead || !target.Spawned || target.Map != pawn.Map)
            {
                return false;
            }

            if (target.Faction != pawn.Faction || target.HostileTo(pawn) || target.InAggroMentalState || target.IsAttacking())
            {
                return false;
            }

            if (target.RaceProps == null || !target.RaceProps.IsMechanoid || target.TryGetComp<CompMechRepairable>() == null)
            {
                return false;
            }

            if (target.needs?.energy == null || !MechRepairUtility.CanRepair(target))
            {
                return false;
            }

            if (enemyTarget != null && target.Position.InHorDistOf(enemyTarget.Position, repairTargetMinEnemyDistance))
            {
                return false;
            }

            if (GenAI.EnemyIsNear(target, repairTargetMinEnemyDistance, out _, meleeOnly: false, requireLos: true))
            {
                return false;
            }

            if (Building_RepairStation.IsPawnClaimedByAnyRepairStation(target))
            {
                return false;
            }

            return pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly);
        }

        private Job TryGetBlindingPositionJob(Pawn pawn, Ability ability, Thing enemyTarget)
        {
            if (ability == null || ability.verb == null || !ability.CanCast)
            {
                return null;
            }

            Pawn target = FindBlindingTarget(pawn, ability, enemyTarget, requireCanHit: false);
            if (target == null)
            {
                target = enemyTarget as Pawn;
            }
            if (target == null)
            {
                return null;
            }

            if (!CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = target,
                verb = ability.verb,
                maxRangeFromTarget = ability.verb.EffectiveRange,
                maxRangeFromCaster = maxRepositionDistance,
                wantCoverFromTarget = false,
                preferredCastPosition = pawn.Position
            }, out IntVec3 dest))
            {
                return null;
            }

            if (!dest.IsValid || dest == pawn.Position)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.expiryInterval = moveJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            job.collideWithPawns = true;
            return job;
        }

        private Job MakeWaitCombatJob(Pawn pawn)
        {
            pawn.pather?.StopDead();
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, waitJobExpiryInterval, checkOverrideOnExpiry: true);
        }

        private bool IsCombatEnemyTarget(Pawn pawn, Thing target, float maxDistance)
        {
            if (!IsValidEnemyTarget(pawn, target) || !pawn.Position.InHorDistOf(target.Position, maxDistance))
            {
                return false;
            }

            return !requireLineOfSightToTargets || GenSight.LineOfSightToThing(pawn.Position, target, pawn.Map);
        }

        private static bool IsValidEnemyTarget(Pawn pawn, Thing target)
        {
            if (target == null || target.Destroyed || !target.Spawned || target.Map != pawn.Map || !target.HostileTo(pawn))
            {
                return false;
            }

            if (target is Pawn targetPawn)
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

        private static bool HasActiveShield(Pawn target)
        {
            List<Thing> thingList = target.Position.GetThingList(target.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                MechShield shield = thingList[i] as MechShield;
                if (shield != null && shield.IsTargeting(target))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
