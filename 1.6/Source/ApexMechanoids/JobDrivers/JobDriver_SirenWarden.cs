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
            this.FailOn(() => !SirenWardenUtility.CanSirenWork(pawn));
            this.FailOn(() => !SirenWardenUtility.CanContinueChatWithPrisoner(pawn, Prisoner));
            this.FailOn(() => !SirenWardenUtility.HasReachableInteractablePosition(pawn, Prisoner));

            yield return GotoPrisoner();
            yield return WaitToBeAbleToSing();
            yield return Toils_Interpersonal.GotoInteractablePosition(PrisonerInd);
            yield return SingVerse();
            yield return GotoPrisoner();
            yield return WaitToBeAbleToSing();
            yield return Toils_Interpersonal.GotoInteractablePosition(PrisonerInd);
            yield return SingVerse();
            yield return ResolveRecruitment();
        }

        private Toil GotoPrisoner()
        {
            Pawn prisoner = Prisoner;
            PrisonerInteractionModeDef mode = prisoner?.guest?.ExclusiveInteractionMode ?? PrisonerInteractionModeDefOf.AttemptRecruit;
            return Toils_Interpersonal.GotoPrisoner(pawn, prisoner, mode);
        }

        private Toil WaitToBeAbleToSing()
        {
            Toil toil = ToilMaker.MakeToil("SirenWaitToBeAbleToSing");
            toil.initAction = delegate
            {
                if (CanStartSirenVerse(toil.actor))
                {
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            toil.tickIntervalAction = delegate
            {
                if (CanStartSirenVerse(toil.actor))
                {
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }

        private static bool CanStartSirenVerse(Pawn siren)
        {
            return siren?.interactions == null || !siren.interactions.InteractedTooRecentlyToInteract();
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
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }
    }
}
