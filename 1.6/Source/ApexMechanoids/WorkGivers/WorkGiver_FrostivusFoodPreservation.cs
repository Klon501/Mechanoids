using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_FrostivusRescueFood : WorkGiver_Scanner
    {
        private const int TakeFoodExpiryInterval = 500;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool Prioritized => true;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn))
            {
                return true;
            }

            return !forced && Find.TickManager.TicksGame < FrostivusFoodPreservationUtility.GetNextRescueScanTick(pawn);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return FrostivusFoodPreservationUtility.RescuableFoodCandidates(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return FrostivusFoodPreservationUtility.CanRescueFoodNow(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return FrostivusFoodPreservationUtility.MakeTakeFoodJob(pawn, t, TakeFoodExpiryInterval, forced);
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            Thing thing = t.Thing;
            return thing != null ? FrostivusFoodPreservationUtility.FoodRescuePriorityScore(thing) : 0f;
        }
    }

    public class WorkGiver_FrostivusUnloadFood : WorkGiver
    {
        private const int UnloadExpiryInterval = 300;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn)
                || !FrostivusFoodPreservationUtility.HasInventoryFood(pawn);
        }

        public override Job NonScanJob(Pawn pawn)
        {
            return FrostivusFoodPreservationUtility.TryFindBestInventoryFoodStorageJob(pawn, UnloadExpiryInterval, out Job job) ? job : null;
        }
    }
}
