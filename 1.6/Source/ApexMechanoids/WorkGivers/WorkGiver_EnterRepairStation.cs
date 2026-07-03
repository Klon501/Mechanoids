using Verse;
using Verse.AI;
using RimWorld;

namespace ApexMechanoids
{
    public class WorkGiver_EnterRepairStation : WorkGiver_EnterBuilding
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!pawn.RaceProps.IsMechanoid) return true;
            if (pawn.Faction != Faction.OfPlayer) return true;
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_RepairStation station)) return false;

            // Normal flow: pawn was already selected for this station by the station's own timer.
            if (station.selectedPawn == pawn)
                return base.HasJobOnThing(pawn, t, forced);

            // Auto-seek path: damaged mech finds a station that has Auto-Repair enabled.
            if (!station.AutoRepairEnabled) return false;
            if (pawn.Drafted) return false;
            if (!IsMechDamaged(pawn)) return false;
            if (!station.CanAcceptPawn(pawn).Accepted) return false;
            if (!pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Deadly)) return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced)) return false;

            // Reserve the station for this pawn so others don't also target it.
            station.SelectPawn(pawn);
            return true;
        }

        private static bool IsMechDamaged(Pawn pawn)
        {
            System.Collections.Generic.List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury) return true;
            }
            foreach (Hediff_MissingPart _ in pawn.health.hediffSet.GetMissingPartsCommonAncestors())
                return true;
            return false;
        }
    }
}
