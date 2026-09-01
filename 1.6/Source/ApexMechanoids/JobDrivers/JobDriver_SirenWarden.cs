using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public abstract class JobDriver_SirenSongInteraction : JobDriver
    {
        protected const TargetIndex TargetInd = TargetIndex.A;
        private const int VerseDuration = 350;

        protected Pawn Target => job.GetTarget(TargetInd).Thing as Pawn;

        protected virtual int VerseCount => 2;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Target != null && pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetInd);
            this.FailOnMentalState(TargetInd);
            this.FailOnNotAwake(TargetInd);
            this.FailOnForbidden(TargetInd);
            this.FailOn(() => !SirenWardenUtility.CanSirenWork(pawn));
            this.FailOn(() => !CanContinueInteraction());
            this.FailOn(() => !SirenWardenUtility.HasReachableInteractablePosition(pawn, Target));

            for (int i = 0; i < VerseCount; i++)
            {
                yield return GotoTarget();
                yield return WaitToBeAbleToSing();
                yield return Toils_Interpersonal.GotoInteractablePosition(TargetInd);
                yield return SingVerse();
            }

            yield return ResolveOutcome();
        }

        protected abstract bool CanContinueInteraction();

        protected abstract Toil GotoTarget();

        protected abstract void ResolveInteraction();

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
            Toil toil = ToilMaker.MakeToil("SirenSingVerse");
            toil.initAction = delegate
            {
                Pawn target = Target;
                if (target != null)
                {
                    PawnUtility.ForceWait(target, VerseDuration, toil.actor);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = VerseDuration;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }

        private Toil ResolveOutcome()
        {
            Toil toil = ToilMaker.MakeToil("SirenResolveSongInteraction");
            toil.initAction = delegate
            {
                ResolveInteraction();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }
    }

    public abstract class JobDriver_SirenPrisonerSong : JobDriver_SirenSongInteraction
    {
        protected override Toil GotoTarget()
        {
            Pawn prisoner = Target;
            PrisonerInteractionModeDef mode = prisoner?.guest?.ExclusiveInteractionMode ?? PrisonerInteractionModeDefOf.AttemptRecruit;
            return Toils_Interpersonal.GotoPrisoner(pawn, prisoner, mode);
        }
    }

    public class JobDriver_SirenChatWithPrisoner : JobDriver_SirenPrisonerSong
    {
        protected override bool CanContinueInteraction()
        {
            return SirenWardenUtility.CanContinueChatWithPrisoner(pawn, Target);
        }

        protected override void ResolveInteraction()
        {
            SirenWardenUtility.DoRecruitInteraction(pawn, Target);
        }
    }

    public class JobDriver_SirenEnslavePrisoner : JobDriver_SirenPrisonerSong
    {
        protected override bool CanContinueInteraction()
        {
            return SirenWardenUtility.CanContinueEnslavePrisoner(pawn, Target);
        }

        protected override void ResolveInteraction()
        {
            SirenWardenUtility.DoEnslaveInteraction(pawn, Target);
        }
    }

    public class JobDriver_SirenConvertPrisoner : JobDriver_SirenPrisonerSong
    {
        protected override bool CanContinueInteraction()
        {
            return SirenWardenUtility.CanContinueConvertPrisoner(pawn, Target);
        }

        protected override void ResolveInteraction()
        {
            SirenWardenUtility.DoConvertInteraction(pawn, Target);
        }
    }

    public class JobDriver_SirenSuppressSlave : JobDriver_SirenSongInteraction
    {
        protected override int VerseCount => 1;

        protected override Toil GotoTarget()
        {
            return Toils_Interpersonal.GotoSlave(pawn, Target);
        }

        protected override bool CanContinueInteraction()
        {
            return SirenWardenUtility.CanContinueSuppressSlave(pawn, Target);
        }

        protected override void ResolveInteraction()
        {
            Pawn slave = Target;
            if (SirenWardenUtility.DoSuppressInteraction(pawn, slave))
            {
                SirenWardenUtility.SetLastSuppressionTime(slave);
            }
        }
    }
}
