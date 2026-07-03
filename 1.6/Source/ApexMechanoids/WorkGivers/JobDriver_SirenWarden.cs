using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobDriver_SirenChatWithPrisoner : JobDriver
    {
        private const TargetIndex PrisonerInd = TargetIndex.A;
        private const int VerseDuration = 350;

        private Pawn Prisoner => job.GetTarget(PrisonerInd).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Prisoner != null && pawn.Reserve(Prisoner, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PrisonerInd);
            this.FailOnMentalState(PrisonerInd);
            this.FailOnNotAwake(PrisonerInd);
            this.FailOnCannotTouch(PrisonerInd, PathEndMode.Touch);
            this.FailOn(() => !SirenWardenUtility.CanSirenWork(pawn));
            this.FailOn(() => !SirenWardenUtility.CanContinueChatWithPrisoner(pawn, Prisoner));

            yield return Toils_Goto.GotoThing(PrisonerInd, PathEndMode.Touch);
            yield return SingVerse();
            yield return Toils_Goto.GotoThing(PrisonerInd, PathEndMode.Touch);
            yield return SingVerse();
            yield return ResolveRecruitment();
        }

        private Toil SingVerse()
        {
            Toil toil = ToilMaker.MakeToil("SirenSingToPrisoner");
            toil.initAction = delegate
            {
                Pawn prisoner = Prisoner;
                if (prisoner != null)
                {
                    PawnUtility.ForceWait(prisoner, VerseDuration, toil.actor);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = VerseDuration;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }

        private Toil ResolveRecruitment()
        {
            Toil toil = ToilMaker.MakeToil("SirenResolveRecruitment");
            toil.initAction = delegate
            {
                SirenWardenUtility.DoRecruitInteraction(toil.actor, Prisoner);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = VerseDuration;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }
    }
}
