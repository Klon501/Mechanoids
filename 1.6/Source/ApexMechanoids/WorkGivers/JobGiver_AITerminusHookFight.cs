using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AITerminusHookFight : ThinkNode_JobGiver
    {
        public float targetAcquireRadius = 30f;
        public float minHookDistance = TerminusHookUtility.DefaultMinAIHookDistance;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AITerminusHookFight obj = (JobGiver_AITerminusHookFight)base.DeepCopy(resolve);
            obj.targetAcquireRadius = targetAcquireRadius;
            obj.minHookDistance = minHookDistance;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!TerminusHookUtility.TryMakeBestAIHookJob(pawn, minHookDistance, targetAcquireRadius, out Job job))
            {
                return null;
            }

            return job;
        }
    }
}
