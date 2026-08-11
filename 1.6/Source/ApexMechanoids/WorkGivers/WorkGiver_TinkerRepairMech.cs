using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_TinkerRepairMech : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!TinkerRepairUtility.CanDoTinkerRepair(pawn))
            {
                yield break;
            }

            foreach (Pawn candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                if (candidate != pawn)
                {
                    yield return candidate;
                }
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !TinkerRepairUtility.CanDoTinkerRepair(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return !pawn.Drafted && TinkerRepairUtility.CanRepairMechNow(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDefOf.RepairMech, t);
        }
    }
}
