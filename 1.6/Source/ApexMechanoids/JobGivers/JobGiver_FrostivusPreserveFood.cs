using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class ThinkNode_ConditionalFrostivusControlled : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            return FrostivusFoodPreservationUtility.HasPlayerFoodPreservationControl(pawn);
        }
    }

    public class ThinkNode_ConditionalFrostivusNonPlayer : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn)
        {
            return FrostivusFoodPreservationUtility.IsFrostivus(pawn) && pawn.Faction != Faction.OfPlayer;
        }
    }

    public class JobGiver_FrostivusRescuePawn : ThinkNode_JobGiver
    {
        private const string CryptoSwallowDefName = "APM_Ability_CryptoSwallow";

        public float maxDistance = 9999f;
        public int expiryInterval = 300;

        private static AbilityDef cachedCryptoSwallowDef;

        private static AbilityDef CryptoSwallowDef
        {
            get
            {
                if (cachedCryptoSwallowDef == null)
                {
                    cachedCryptoSwallowDef = DefDatabase<AbilityDef>.GetNamedSilentFail(CryptoSwallowDefName);
                }

                return cachedCryptoSwallowDef;
            }
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_FrostivusRescuePawn obj = (JobGiver_FrostivusRescuePawn)base.DeepCopy(resolve);
            obj.maxDistance = maxDistance;
            obj.expiryInterval = expiryInterval;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.CurJob?.ability != null || pawn?.CurJob?.playerForced == true || pawn?.Drafted == true)
            {
                return null;
            }

            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn))
            {
                return null;
            }

            Ability ability = GetCryptoSwallowAbility(pawn);
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            if (!TryFindBestRescueTarget(pawn, ability, out Pawn target))
            {
                return null;
            }

            LocalTargetInfo targetInfo = target;
            Job job = ability.GetJob(targetInfo, targetInfo);
            if (job == null)
            {
                return null;
            }

            job.expiryInterval = expiryInterval;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Ability GetCryptoSwallowAbility(Pawn pawn)
        {
            AbilityDef abilityDef = CryptoSwallowDef;
            if (abilityDef == null || pawn?.abilities == null)
            {
                return null;
            }

            return pawn.abilities.GetAbility(abilityDef);
        }

        private bool TryFindBestRescueTarget(Pawn pawn, Ability ability, out Pawn bestTarget)
        {
            bestTarget = null;

            Pawn overseer = pawn.GetOverseer();
            if (IsValidRescueTarget(pawn, ability, overseer))
            {
                bestTarget = overseer;
                return true;
            }

            int bestPriority = int.MinValue;
            int bestDistance = int.MaxValue;
            int maxDistanceSquared = maxDistance >= 9999f ? int.MaxValue : (int)System.Math.Ceiling(maxDistance * maxDistance);
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn target = pawns[i];
                if (target == overseer || !IsValidRescueTarget(pawn, ability, target))
                {
                    continue;
                }

                int priority = RescuePriority(target);
                if (priority <= 0)
                {
                    continue;
                }

                int distance = pawn.Position.DistanceToSquared(target.Position);
                if (distance > maxDistanceSquared)
                {
                    continue;
                }

                if (bestTarget == null || priority > bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    bestTarget = target;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }

            return bestTarget != null;
        }

        private static bool IsValidRescueTarget(Pawn pawn, Ability ability, Pawn target)
        {
            if (target == null
                || target == pawn
                || target.Destroyed
                || target.Dead
                || !target.Downed
                || !target.Spawned
                || target.Map != pawn.Map
                || target.ParentHolder is PawnFlyer)
            {
                return false;
            }

            if (target.RaceProps?.Humanlike != true || target.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (!HealthAIUtility.WantsToBeRescued(target) || FrostivusUtility.HasDevouredHediff(target))
            {
                return false;
            }

            LocalTargetInfo targetInfo = target;
            return CompAbilityEffect_CryptoSwallow.CanSwallowTarget(pawn, targetInfo).Accepted
                && ability.CanApplyOn(targetInfo)
                && pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly, 1, -1, null, true);
        }

        private static int RescuePriority(Pawn target)
        {
            if (target.IsColonist)
            {
                return 2;
            }

            if (target.IsPrisonerOfColony || target.IsSlaveOfColony)
            {
                return 1;
            }

            return 0;
        }
    }
}
