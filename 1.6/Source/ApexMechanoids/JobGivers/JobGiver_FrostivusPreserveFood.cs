using RimWorld;
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

    public class JobGiver_FrostivusPreserveFood : ThinkNode_JobGiver
    {
        public float maxDistance = 9999f;
        public int expiryInterval = 500;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_FrostivusPreserveFood obj = (JobGiver_FrostivusPreserveFood)base.DeepCopy(resolve);
            obj.maxDistance = maxDistance;
            obj.expiryInterval = expiryInterval;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.CurJob?.ability != null || pawn?.Drafted == true)
            {
                return null;
            }

            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn))
            {
                return null;
            }

            if (!FrostivusFoodPreservationUtility.TryFindBestRescuableFood(pawn, out Thing food, maxDistance))
            {
                return null;
            }

            return FrostivusFoodPreservationUtility.MakeTakeFoodJob(pawn, food, expiryInterval);
        }
    }

    public class JobGiver_FrostivusUnloadFoodToStorage : ThinkNode_JobGiver
    {
        public int expiryInterval = 300;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_FrostivusUnloadFoodToStorage obj = (JobGiver_FrostivusUnloadFoodToStorage)base.DeepCopy(resolve);
            obj.expiryInterval = expiryInterval;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.CurJob?.ability != null || pawn?.Drafted == true)
            {
                return null;
            }

            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn))
            {
                return null;
            }

            if (!FrostivusFoodPreservationUtility.TryFindBestInventoryFoodStorageJob(pawn, expiryInterval, out Job job))
            {
                return null;
            }

            return job;
        }
    }
}
