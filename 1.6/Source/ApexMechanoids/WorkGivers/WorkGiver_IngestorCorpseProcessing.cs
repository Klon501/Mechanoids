using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_IngestorProcessCorpses : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Corpse);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !IngestorCorpseProcessingUtility.CanDoCorpseProcessing(pawn);
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

            Ability absorb = IngestorCorpseProcessingUtility.GetAbsorbAbility(pawn);
            Job job = absorb.GetJob(corpse, corpse);
            job.expiryInterval = 500;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }
}
