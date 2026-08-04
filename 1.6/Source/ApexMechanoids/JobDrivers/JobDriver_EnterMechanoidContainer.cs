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
    public class JobDriver_EnterMechanoidContainer : JobDriver
    {
        public const int EnterDelay = 250;

        private TargetIndex MechanoidContainerTarget => TargetIndex.A;
        private Comp_MechanoidContainerControlled MechanoidContainerComp => job.targetA.Thing.TryGetComp<Comp_MechanoidContainerControlled>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(MechanoidContainerTarget);
            this.FailOn(() => !MechanoidContainerComp.CanAcceptMech(pawn));
            yield return Toils_Goto.GotoThing(MechanoidContainerTarget, PathEndMode.InteractionCell);
            yield return Toils_General.WaitWith(MechanoidContainerTarget, EnterDelay, useProgressBar: true);
            yield return Toils_General.Do((Action)delegate
            {
                this.MechanoidContainerComp.TryAcceptPawn(pawn);
            });
        }
    }
}
