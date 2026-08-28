using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class FloatMenuOptionProvider_TinkerRepair : FloatMenuOptionProvider
    {
        public override bool Drafted => true;

        public override bool Undrafted => true;

        public override bool Multiselect => false;

        public override bool MechanoidCanDo => true;

        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
        {
            return base.SelectedPawnValid(pawn, context)
                && pawn.Faction == Faction.OfPlayer
                && TinkerRepairUtility.CanDoTinkerRepair(pawn);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing is Pawn)
            {
                yield break;
            }

            Pawn tinker = context.FirstSelectedPawn;
            if (!TinkerRepairUtility.CanRepairBuildingNow(tinker, clickedThing, forced: true))
            {
                yield break;
            }

            yield return MakeRepairOption(tinker, clickedThing, JobDefOf.Repair);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn tinker = context.FirstSelectedPawn;
            if (!TinkerRepairUtility.CanRepairMechNow(tinker, clickedPawn, forced: true))
            {
                yield break;
            }

            yield return MakeRepairOption(tinker, clickedPawn, ApexDefsOf.APM_RepairMech);
        }

        private static FloatMenuOption MakeRepairOption(Pawn tinker, Thing target, JobDef jobDef)
        {
            FloatMenuOption option = new FloatMenuOption(
                "APM.Tinker.Order.Repair".Translate(target.Label, target),
                delegate
                {
                    Job job = JobMaker.MakeJob(jobDef, target);
                    job.playerForced = true;
                    tinker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                },
                MenuOptionPriority.High);

            return FloatMenuUtility.DecoratePrioritizedTask(option, tinker, target, "ReservedBy");
        }
    }
}
