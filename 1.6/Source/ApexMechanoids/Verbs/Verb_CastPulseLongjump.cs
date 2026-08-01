using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class Verb_CastPulseLongjump : Verb_CastAbilityJump
    {
        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Pawn casterPawn = CasterPawn;
            Map map = casterPawn?.Map;
            JobDef jobDef = ApexDefsOf.APM_CastPulseJump;
            if (casterPawn == null || map == null || jobDef == null)
            {
                base.OrderForceTarget(target);
                return;
            }

            IntVec3 destination = RCellFinder.BestOrderedGotoDestNear(
                target.Cell,
                casterPawn,
                c => JumpUtility.ValidJumpTarget(casterPawn, map, c)
                    && JumpUtility.CanHitTargetFrom(casterPawn, casterPawn.Position, c, EffectiveRange),
                reachable: false);

            if (!destination.IsValid)
            {
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, destination);
            job.verbToUse = this;
            job.ability = ability;

            if (casterPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                FleckMaker.Static(destination, map, FleckDefOf.FeedbackGoto);
            }
        }
    }
}
