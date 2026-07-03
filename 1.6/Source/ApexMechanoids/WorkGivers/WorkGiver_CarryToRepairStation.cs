using Verse;
using Verse.AI;
using RimWorld;

namespace ApexMechanoids
{
    public class WorkGiver_CarryToRepairStation : WorkGiver_CarryToBuilding
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_RepairStation)) return false;
            return base.HasJobOnThing(pawn, t, forced);
        }
    }
}