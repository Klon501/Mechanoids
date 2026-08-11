using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_TinkerRepairBuilding : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!TinkerRepairUtility.CanDoTinkerRepair(pawn))
            {
                yield break;
            }

            List<Thing> repairableBuildings = pawn.Map.listerBuildingsRepairable.RepairableBuildings(pawn.Faction);
            for (int i = 0; i < repairableBuildings.Count; i++)
            {
                yield return repairableBuildings[i];
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !TinkerRepairUtility.CanDoTinkerRepair(pawn)
                || pawn.Map.listerBuildingsRepairable.RepairableBuildings(pawn.Faction).Count == 0;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return TinkerRepairUtility.CanRepairBuildingNow(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDefOf.Repair, t);
        }
    }
}
