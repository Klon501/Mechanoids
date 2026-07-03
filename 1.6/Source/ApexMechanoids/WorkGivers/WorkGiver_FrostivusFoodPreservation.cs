using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_FrostivusUnloadFood : WorkGiver
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn)
                || !FrostivusFoodPreservationUtility.HasInventoryFood(pawn)
                || FrostivusFoodPreservationUtility.HasRescuableFoodAvailable(pawn);
        }

        public override Job NonScanJob(Pawn pawn)
        {
            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn)
                || FrostivusFoodPreservationUtility.HasRescuableFoodAvailable(pawn))
            {
                return null;
            }

            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing food = innerList[i];
                if (!FrostivusFoodPreservationUtility.IsInventoryFood(food))
                {
                    continue;
                }

                if (FrostivusFoodPreservationUtility.TryFindColdStorageCell(pawn, food, out IntVec3 cell))
                {
                    Job job = JobMaker.MakeJob(ApexDefsOf.APM_FrostivusUnloadFoodToStorage, food, cell);
                    job.count = System.Math.Min(food.stackCount, cell.GetItemStackSpaceLeftFor(pawn.Map, food.def));
                    job.expiryInterval = 300;
                    job.checkOverrideOnExpire = true;
                    return job;
                }
            }

            return null;
        }
    }

    public class WorkGiver_FrostivusRescueFood : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn))
            {
                yield break;
            }

            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                yield return things[i];
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!FrostivusFoodPreservationUtility.CanRescueFoodNow(pawn, t, forced))
            {
                return null;
            }

            int count = FrostivusFoodPreservationUtility.CountToPickUp(pawn, t);
            if (count <= 0)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, t);
            job.count = count;
            job.checkEncumbrance = true;
            job.takeInventoryDelay = FrostivusFoodPreservationUtility.PickupDelayTicks;
            job.expiryInterval = 500;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }
}
