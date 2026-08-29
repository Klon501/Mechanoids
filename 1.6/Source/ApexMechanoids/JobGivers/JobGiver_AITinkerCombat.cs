using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ApexMechanoids
{
    public class JobGiver_AITinkerCombat : ThinkNode_JobGiver
    {
        private const float ShieldDistanceFactor = 0.35f;
        private const float RepairDistanceFactor = 1f;
        private const float RepairSafetyFactor = 0.25f;
        private const float AttackingScore = 150f;
        private const float NearEnemyScore = 150f;
        private const float NearEnemyRadius = 15f;

        public float targetAcquireRadius = 30f;
        public float targetKeepRadius = 35f;
        public float fleeEnemyDistance = 7.9f;
        public int fleeDistance = 16;
        public float shieldSearchRadius = 11f;
        public float repairSearchRadius = 18f;
        public float repairTargetMinEnemyDistance = 8f;
        public float maxRepositionDistance = 12f;
        public bool requireLineOfSightToTargets = true;
        public bool allowPlayerControlled = false;
        public bool useCombatWaitFallback = true;
        public int abilityJobExpiryInterval = 120;
        public int moveJobExpiryInterval = 120;
        public int repairJobExpiryInterval = 120;
        public int waitJobExpiryInterval = 60;
        public int shieldRecentAttackTargetTicks = 300;
        public int shieldRecentRangedHarmTicks = 2500;
        public int shieldTargetLockTicks = 500;
        public float shieldSelfMeleeThreatRadius = 2.9f;

        public float shieldAimedAtScore = 2500f;
        public float shieldRecentlyAttackedScore = 1500f;
        public float shieldRecentHarmScore = 1000f;
        public float shieldMissingHealthScore = 900f;
        public float shieldMeleeAllyScore = 1000f;
        public float shieldLockScore = 400f;
        public float shieldNoThreatFactor = 0.35f;
        public float shieldWeight = 1f;
        public float repairMissingHealthScore = 1200f;
        public float repairDownedScore = 900f;
        public float repairValueFactor = 0.5f;
        public float repairMaxSafetyScore = 300f;
        public float repairMinScore = 200f;
        public float repairWeight = 1f;

        private static readonly Dictionary<int, ShieldTargetMemory> shieldTargetMemory = new Dictionary<int, ShieldTargetMemory>();
        private static readonly List<int> tmpShieldTargetMemoryKeysToRemove = new List<int>();
        private static readonly List<ThreatInfo> tmpThreats = new List<ThreatInfo>();
        private static int lastShieldTargetMemoryCleanupTick = -99999;

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
            obj.allowPlayerControlled = allowPlayerControlled;
            obj.useCombatWaitFallback = useCombatWaitFallback;
            obj.abilityJobExpiryInterval = abilityJobExpiryInterval;
            obj.moveJobExpiryInterval = moveJobExpiryInterval;
            obj.repairJobExpiryInterval = repairJobExpiryInterval;
            obj.waitJobExpiryInterval = waitJobExpiryInterval;
            obj.shieldRecentAttackTargetTicks = shieldRecentAttackTargetTicks;
            obj.shieldRecentRangedHarmTicks = shieldRecentRangedHarmTicks;
            obj.shieldTargetLockTicks = shieldTargetLockTicks;
            obj.shieldSelfMeleeThreatRadius = shieldSelfMeleeThreatRadius;
            obj.shieldAimedAtScore = shieldAimedAtScore;
            obj.shieldRecentlyAttackedScore = shieldRecentlyAttackedScore;
            obj.shieldRecentHarmScore = shieldRecentHarmScore;
            obj.shieldMissingHealthScore = shieldMissingHealthScore;
            obj.shieldMeleeAllyScore = shieldMeleeAllyScore;
            obj.shieldLockScore = shieldLockScore;
            obj.shieldNoThreatFactor = shieldNoThreatFactor;
            obj.shieldWeight = shieldWeight;
            obj.repairMissingHealthScore = repairMissingHealthScore;
            obj.repairDownedScore = repairDownedScore;
            obj.repairValueFactor = repairValueFactor;
            obj.repairMaxSafetyScore = repairMaxSafetyScore;
            obj.repairMinScore = repairMinScore;
            obj.repairWeight = repairWeight;
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
            Ability blindingLaser = pawn.abilities.GetAbility(ApexDefsOf.APM_BlindingLaser);
            Ability defenceMatrix = pawn.abilities.GetAbility(ApexDefsOf.APM_DefenceMatrix);

            if (enemyTarget != null)
            {
                Job blindJob = TryGetBlindingJob(pawn, blindingLaser, enemyTarget);
                if (blindJob != null)
                {
                    return blindJob;
                }
            }

            Job supportJob = TryGetSupportJob(pawn, defenceMatrix, enemyTarget);
            if (supportJob != null)
            {
                return supportJob;
            }

            if (enemyTarget == null)
            {
                return null;
            }

            Job positionJob = TryGetBlindingPositionJob(pawn, blindingLaser, enemyTarget);
            if (positionJob != null)
            {
                return positionJob;
            }

            return useCombatWaitFallback ? MakeWaitCombatJob(pawn) : null;
        }

        private bool CanRunFor(Pawn pawn)
        {
            return pawn != null
                && pawn.def == ApexDefsOf.APM_Mech_Tinker
                && pawn.Spawned
                && pawn.Map != null
                && pawn.Faction != null
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && Utils.IsAwakeAndNotDormant(pawn)
                && (!pawn.IsPlayerControlled || allowPlayerControlled)
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

        private Job TryGetSupportJob(Pawn pawn, Ability shieldAbility, Thing enemyTarget)
        {
            bool canShield = enemyTarget != null
                && shieldAbility != null
                && shieldAbility.CanCast
                && !HasCloseMeleeThreat(pawn);

            int ticksGame = Find.TickManager.TicksGame;
            BuildThreatCache(pawn);
            Pawn lockedShieldTarget = canShield ? CurrentLockedShieldTarget(pawn, ticksGame) : null;

            Pawn bestShieldTarget = null;
            float bestShieldScore = float.MinValue;
            Pawn bestShieldTinker = null;
            float bestShieldTinkerScore = float.MinValue;
            Pawn bestRepairTarget = null;
            float bestRepairScore = float.MinValue;

            float shieldMaxDistanceSq = shieldSearchRadius * shieldSearchRadius;
            float repairMaxDistanceSq = repairSearchRadius * repairSearchRadius;
            List<Pawn> factionPawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < factionPawns.Count; i++)
            {
                Pawn candidate = factionPawns[i];
                if (candidate == null || candidate == pawn || candidate.Destroyed || candidate.Dead || !candidate.Spawned || candidate.Map != pawn.Map)
                {
                    continue;
                }

                float distanceSq = pawn.Position.DistanceToSquared(candidate.Position);

                if (canShield && distanceSq <= shieldMaxDistanceSq && CanShieldTarget(pawn, candidate, shieldAbility))
                {
                    float score = ShieldTargetScore(candidate, enemyTarget, distanceSq, ticksGame);
                    if (candidate == lockedShieldTarget)
                    {
                        score += shieldLockScore;
                    }

                    if (candidate.def == ApexDefsOf.APM_Mech_Tinker)
                    {
                        if (bestShieldTinker == null || score > bestShieldTinkerScore)
                        {
                            bestShieldTinker = candidate;
                            bestShieldTinkerScore = score;
                        }
                    }
                    else if (bestShieldTarget == null || score > bestShieldScore)
                    {
                        bestShieldTarget = candidate;
                        bestShieldScore = score;
                    }
                }

                if (distanceSq <= repairMaxDistanceSq && CanRepairCombatMechNow(pawn, candidate, enemyTarget))
                {
                    float score = RepairTargetScore(candidate, enemyTarget, distanceSq);
                    if (bestRepairTarget == null || score > bestRepairScore)
                    {
                        bestRepairTarget = candidate;
                        bestRepairScore = score;
                    }
                }
            }

            Pawn shieldTarget = bestShieldTarget ?? bestShieldTinker;
            float shieldScore = bestShieldTarget != null ? bestShieldScore : bestShieldTinkerScore;

            if (bestRepairTarget != null
                && bestRepairScore >= repairMinScore
                && (shieldTarget == null || bestRepairScore * repairWeight > shieldScore * shieldWeight))
            {
                return MakeRepairJob(bestRepairTarget, enemyTarget != null);
            }

            if (shieldTarget != null)
            {
                return MakeShieldJob(pawn, shieldAbility, shieldTarget, ticksGame);
            }

            return null;
        }

        private float ShieldTargetScore(Pawn target, Thing enemyTarget, float distanceSq, int ticksGame)
        {
            float threatScore = IncomingRangedThreatScore(target, ticksGame);
            if (WasRecentlyHitByRanged(target, ticksGame))
            {
                threatScore += shieldRecentHarmScore;
            }

            float score = threatScore;
            score += MissingHealthOf(target) * shieldMissingHealthScore;
            score += SupportTargetValueScore(target);
            score -= distanceSq * ShieldDistanceFactor;

            if (IsMeleeAlly(target))
            {
                score += shieldMeleeAllyScore;
            }
            if (target.IsAttacking())
            {
                score += AttackingScore;
            }
            if (enemyTarget != null && target.Position.InHorDistOf(enemyTarget.Position, NearEnemyRadius))
            {
                score += NearEnemyScore;
            }

            if (threatScore <= 0f && score > 0f)
            {
                score *= shieldNoThreatFactor;
            }

            return score;
        }

        private float RepairTargetScore(Pawn target, Thing enemyTarget, float distanceSq)
        {
            float score = MissingHealthOf(target) * repairMissingHealthScore;
            score += SupportTargetValueScore(target) * repairValueFactor;
            score -= distanceSq * RepairDistanceFactor;

            if (target.Downed)
            {
                score += repairDownedScore;
            }

            if (enemyTarget != null)
            {
                float enemyDistanceSq = target.Position.DistanceToSquared(enemyTarget.Position);
                score += Mathf.Min(enemyDistanceSq * RepairSafetyFactor, repairMaxSafetyScore);
            }
            else
            {
                score += repairMaxSafetyScore;
            }

            return score;
        }

        private float IncomingRangedThreatScore(Pawn target, int ticksGame)
        {
            float score = 0f;
            for (int i = 0; i < tmpThreats.Count; i++)
            {
                ThreatInfo threat = tmpThreats[i];
                if (!threat.ranged)
                {
                    continue;
                }

                if (threat.aimingAt == target)
                {
                    score += shieldAimedAtScore;
                }
                if (threat.lastAttacked == target && ticksGame - threat.lastAttackTick <= shieldRecentAttackTargetTicks)
                {
                    score += shieldRecentlyAttackedScore;
                }
            }

            return score;
        }

        private void BuildThreatCache(Pawn pawn)
        {
            tmpThreats.Clear();
            List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                IAttackTarget threat = potentialTargets[i];
                Thing threatThing = threat.Thing;
                if (!IsValidEnemyTarget(pawn, threatThing) || threat.ThreatDisabled(pawn))
                {
                    continue;
                }

                IAttackTargetSearcher searcher = threat as IAttackTargetSearcher;
                tmpThreats.Add(new ThreatInfo
                {
                    thing = threatThing,
                    ranged = HasRangedThreatVerb(searcher),
                    aimingAt = threat.TargetCurrentlyAimingAt.Thing,
                    lastAttacked = searcher != null ? searcher.LastAttackedTarget.Thing : null,
                    lastAttackTick = searcher != null ? searcher.LastAttackTargetTick : -99999
                });
            }
        }

        private bool EnemyIsNearRepairTarget(Pawn target)
        {
            float radiusSq = repairTargetMinEnemyDistance * repairTargetMinEnemyDistance;
            for (int i = 0; i < tmpThreats.Count; i++)
            {
                Thing threatThing = tmpThreats[i].thing;
                if (target.Position.DistanceToSquared(threatThing.Position) > radiusSq)
                {
                    continue;
                }

                if (GenSight.LineOfSightToThing(target.Position, threatThing, target.Map))
                {
                    return true;
                }
            }

            return false;
        }

        private Pawn CurrentLockedShieldTarget(Pawn pawn, int ticksGame)
        {
            if (shieldTargetLockTicks <= 0 || pawn == null)
            {
                return null;
            }

            if (!shieldTargetMemory.TryGetValue(pawn.thingIDNumber, out ShieldTargetMemory memory))
            {
                return null;
            }

            return ticksGame - memory.startTick < shieldTargetLockTicks ? memory.target : null;
        }

        private void RememberShieldTarget(Pawn pawn, Pawn target, int ticksGame)
        {
            if (shieldTargetLockTicks <= 0 || pawn == null || target == null)
            {
                return;
            }

            shieldTargetMemory[pawn.thingIDNumber] = new ShieldTargetMemory(target, ticksGame);
            CleanupShieldTargetMemory(ticksGame);
        }

        private static void CleanupShieldTargetMemory(int ticksGame)
        {
            if (ticksGame - lastShieldTargetMemoryCleanupTick < 2500)
            {
                return;
            }

            lastShieldTargetMemoryCleanupTick = ticksGame;
            tmpShieldTargetMemoryKeysToRemove.Clear();
            foreach (KeyValuePair<int, ShieldTargetMemory> pair in shieldTargetMemory)
            {
                Pawn target = pair.Value.target;
                if (ticksGame - pair.Value.startTick > 60000
                    || target == null
                    || target.Destroyed
                    || target.Dead
                    || !target.Spawned)
                {
                    tmpShieldTargetMemoryKeysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < tmpShieldTargetMemoryKeysToRemove.Count; i++)
            {
                shieldTargetMemory.Remove(tmpShieldTargetMemoryKeysToRemove[i]);
            }
            tmpShieldTargetMemoryKeysToRemove.Clear();
        }

        private struct ShieldTargetMemory
        {
            public Pawn target;
            public int startTick;

            public ShieldTargetMemory(Pawn target, int startTick)
            {
                this.target = target;
                this.startTick = startTick;
            }
        }

        private struct ThreatInfo
        {
            public Thing thing;
            public bool ranged;
            public Thing aimingAt;
            public Thing lastAttacked;
            public int lastAttackTick;
        }

        private bool WasRecentlyHitByRanged(Pawn target, int ticksGame)
        {
            return target.mindState != null
                && target.mindState.lastRangedHarmTick > 0
                && ticksGame - target.mindState.lastRangedHarmTick <= shieldRecentRangedHarmTicks;
        }

        private static bool HasRangedThreatVerb(IAttackTargetSearcher searcher)
        {
            Verb verb = searcher?.CurrentEffectiveVerb;
            return verb != null && verb.verbProps != null && verb.verbProps.Ranged;
        }

        private static bool IsMeleeAlly(Pawn target)
        {
            Verb verb = target.CurrentEffectiveVerb;
            return verb == null || verb.verbProps == null || !verb.verbProps.Ranged;
        }

        private static float MissingHealthOf(Pawn target)
        {
            return 1f - target.health.summaryHealth.SummaryHealthPercent;
        }

        private static float SupportTargetValueScore(Pawn target)
        {
            float score = target.kindDef != null ? target.kindDef.combatPower * 0.6f : 0f;
            if (target.RaceProps != null)
            {
                score += target.BodySize * 45f;
            }
            return score;
        }

        private static bool CanShieldTarget(Pawn pawn, Pawn target, Ability ability)
        {
            if (target.Downed || target.def == ApexDefsOf.APM_Mech_Dynamo)
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

        private bool CanRepairCombatMechNow(Pawn pawn, Pawn target, Thing enemyTarget)
        {
            if (!ModsConfig.BiotechActive || target.RaceProps == null || !target.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (target.Faction != pawn.Faction || target.HostileTo(pawn) || target.InAggroMentalState || target.IsAttacking())
            {
                return false;
            }

            if (target.TryGetComp<CompMechRepairable>() == null || !MechRepairUtility.CanRepair(target))
            {
                return false;
            }

            if (Building_RepairStation.IsPawnClaimedByAnyRepairStation(target))
            {
                return false;
            }

            if (enemyTarget != null && target.Position.InHorDistOf(enemyTarget.Position, repairTargetMinEnemyDistance))
            {
                return false;
            }

            if (EnemyIsNearRepairTarget(target))
            {
                return false;
            }

            return pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly);
        }

        private Job MakeRepairJob(Pawn target, bool inCombat)
        {
            Job job = JobMaker.MakeJob(ApexDefsOf.APM_RepairMech, target);
            job.expiryInterval = repairJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = inCombat;
            return job;
        }

        private Job MakeShieldJob(Pawn pawn, Ability ability, Pawn target, int ticksGame)
        {
            LocalTargetInfo targetInfo = target;
            RememberShieldTarget(pawn, target, ticksGame);
            return ability.GetJob(targetInfo, targetInfo);
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

        private bool HasCloseMeleeThreat(Pawn pawn)
        {
            if (pawn?.mindState != null && pawn.mindState.MeleeThreatStillThreat)
            {
                return true;
            }

            return GenAI.EnemyIsNear(pawn, shieldSelfMeleeThreatRadius, out Thing threat, meleeOnly: true, requireLos: true)
                && IsValidEnemyTarget(pawn, threat);
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
