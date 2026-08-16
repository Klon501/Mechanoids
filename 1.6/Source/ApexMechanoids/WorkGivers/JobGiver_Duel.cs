using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_Duel : ThinkNode_JobGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map == null || !pawn.Awake())
            {
                return null;
            }

            Thing target = pawn.mindState?.enemyTarget;
            if (!DuelUtility.IsValidActiveDuelOpponent(pawn, target))
            {
                pawn.mindState?.mentalStateHandler?.CurState?.RecoverFromState();
                return null;
            }

            if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.maxNumMeleeAttacks = 1;
            job.expiryInterval = Rand.Range(420, 900);
            job.checkOverrideOnExpire = true;
            job.canBashDoors = true;
            return job;
        }
    }
}
