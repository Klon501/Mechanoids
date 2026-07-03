using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_Duel : ThinkNode_JobGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            var target = pawn.mindState.enemyTarget;
            if (target == null)
            {
                Log.Error($"{nameof(pawn.mindState.enemyTarget)} is null");
                return null;
            }
            Job job2 = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job2.maxNumMeleeAttacks = 1;
            job2.expiryInterval = Rand.Range(420, 900);
            job2.canBashDoors = true;
            return job2;
        }
    }
}
