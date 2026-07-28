using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_IngestorProcessCorpses : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Corpse);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            if (IngestorCorpseProcessingUtility.RequiresMarkedCorpses(pawn))
            {
                foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(ApexDefsOf.APM_IngestorAbsorbCorpse))
                {
                    if (designation.target.Thing != null)
                    {
                        yield return designation.target.Thing;
                    }
                }

                yield break;
            }

            List<Thing> corpses = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                yield return corpses[i];
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!IngestorCorpseProcessingUtility.CanDoCorpseProcessing(pawn))
            {
                return true;
            }

            return IngestorCorpseProcessingUtility.RequiresMarkedCorpses(pawn)
                && !pawn.Map.designationManager.AnySpawnedDesignationOfDef(ApexDefsOf.APM_IngestorAbsorbCorpse);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Corpse corpse) || !IngestorCorpseProcessingUtility.CanUseAbsorbOnCorpse(pawn, corpse, forced))
            {
                return null;
            }

            return IngestorCorpseProcessingUtility.MakeAbsorbCorpseJob(pawn, corpse);
        }
    }
}
