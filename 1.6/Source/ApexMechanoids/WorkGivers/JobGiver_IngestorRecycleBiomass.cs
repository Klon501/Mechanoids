using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_IngestorRecycleBiomass : ThinkNode_JobGiver
    {
        public float maxDistance = 9999f;
        public int expiryInterval = 500;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_IngestorRecycleBiomass obj = (JobGiver_IngestorRecycleBiomass)base.DeepCopy(resolve);
            obj.maxDistance = maxDistance;
            obj.expiryInterval = expiryInterval;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!IngestorCorpseProcessingUtility.CanDoCorpseProcessing(pawn) || pawn.CurJob?.ability != null)
            {
                return null;
            }

            Ability absorb = IngestorCorpseProcessingUtility.GetAbsorbAbility(pawn);
            if (absorb == null || !absorb.CanCast)
            {
                return null;
            }

            Corpse corpse = IngestorCorpseProcessingUtility.FindBestAutoAbsorbCorpse(pawn, maxDistance);
            if (corpse == null)
            {
                return null;
            }

            return IngestorCorpseProcessingUtility.MakeAbsorbCorpseJob(pawn, corpse, absorb, expiryInterval);
        }
    }
}