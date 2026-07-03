using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public abstract class JobDriver_LasherInteractAnimal : JobDriver
    {
        private const TargetIndex AnimalInd = TargetIndex.A;
        private const TargetIndex FoodHandInd = TargetIndex.B;
        private const int FeedDuration = 270;

        private float feedNutritionLeft;

        protected Pawn Animal => (Pawn)job.targetA.Thing;

        protected virtual bool CanInteractNow => true;

        protected virtual bool CanFeedEver => Animal?.needs?.food != null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref feedNutritionLeft, "feedNutritionLeft", 0f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Animal, job, 1, -1, null, errorOnFailed);
        }

        protected IEnumerable<Toil> MakeAnimalInteractionToils()
        {
            this.FailOnDespawnedNullOrForbidden(AnimalInd);
            this.FailOnDowned(AnimalInd);
            this.FailOnNotCasualInterruptible(AnimalInd);

            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch);
            yield return TalkToAnimal(AnimalInd);
            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch);
            yield return TalkToAnimal(AnimalInd);

            if (CanFeedEver)
            {
                foreach (Toil toil in FeedToils())
                {
                    yield return toil;
                }
            }

            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch);
            yield return TalkToAnimal(AnimalInd);

            if (CanFeedEver)
            {
                foreach (Toil toil in FeedToils())
                {
                    yield return toil;
                }
            }

            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch).FailOn(() => !CanInteractNow);
            yield return SetLastAnimalInteractTime(AnimalInd);
        }

        private IEnumerable<Toil> FeedToils()
        {
            Toil initFeed = ToilMaker.MakeToil("InitFeedAnimal");
            initFeed.initAction = delegate
            {
                feedNutritionLeft = JobDriver_InteractAnimal.RequiredNutritionPerFeed(Animal);
            };
            initFeed.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initFeed;

            Toil gotoAnimal = Toils_Goto.GotoThing(AnimalInd, PathEndMode.Touch);
            yield return gotoAnimal;
            yield return StartFeedAnimal(AnimalInd);
            yield return Toils_Ingest.FinalizeIngest(Animal, FoodHandInd);
            yield return Toils_General.PutCarriedThingInInventory();
            yield return Toils_General.ClearTarget(FoodHandInd);
            yield return Toils_Jump.JumpIf(gotoAnimal, () => feedNutritionLeft > 0f);
        }

        private Toil TalkToAnimal(TargetIndex animalInd)
        {
            Toil toil = ToilMaker.MakeToil("LasherTalkToAnimal");
            toil.FailOn(() => !CanInteractNow);
            toil.initAction = delegate
            {
                Pawn target = (Pawn)toil.actor.CurJob.GetTarget(animalInd).Thing;
                if (target != null)
                {
                    PawnUtility.ForceWait(target, FeedDuration, toil.actor);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = FeedDuration;
            return toil;
        }

        private Toil StartFeedAnimal(TargetIndex animalInd)
        {
            Toil toil = ToilMaker.MakeToil("LasherStartFeedAnimal");
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                Pawn animal = (Pawn)actor.CurJob.GetTarget(animalInd).Thing;
                PawnUtility.ForceWait(animal, FeedDuration, actor);
                Thing food = FoodUtility.BestFoodInInventory(actor, animal, FoodPreferability.NeverForNutrition, FoodPreferability.RawTasty);
                if (food == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                actor.mindState.lastInventoryRawFoodUseTick = Find.TickManager.TicksGame;
                int stackCountForNutrition = FoodUtility.StackCountForNutrition(feedNutritionLeft, food.GetStatValue(StatDefOf.Nutrition));
                int stackCount = food.stackCount;
                Thing carriedFood = actor.inventory.innerContainer.Take(food, Math.Min(stackCountForNutrition, stackCount));
                actor.carryTracker.TryStartCarry(carriedFood);
                actor.CurJob.SetTarget(FoodHandInd, carriedFood);
                float carriedNutrition = carriedFood.stackCount * carriedFood.GetStatValue(StatDefOf.Nutrition);
                ticksLeftThisToil = (int)Math.Ceiling(FeedDuration * (carriedNutrition / JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal)));
                if (stackCountForNutrition <= stackCount)
                {
                    feedNutritionLeft = 0f;
                }
                else
                {
                    feedNutritionLeft -= carriedNutrition;
                    if (feedNutritionLeft < 0.001f)
                    {
                        feedNutritionLeft = 0f;
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            return toil;
        }

        private static Toil SetLastAnimalInteractTime(TargetIndex animalInd)
        {
            Toil toil = ToilMaker.MakeToil("SetLastAnimalInteractTime");
            toil.initAction = delegate
            {
                Pawn animal = (Pawn)toil.actor.jobs.curJob.GetTarget(animalInd).Thing;
                if (animal?.mindState != null)
                {
                    animal.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                    animal.mindState.interactionsToday++;
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }

    public class JobDriver_LasherTame : JobDriver_LasherInteractAnimal
    {
        private const TargetIndex FoodIndex = TargetIndex.C;

        protected override bool CanInteractNow => !TameUtility.TriedToTameTooRecently(Animal);

        public override IEnumerable<Toil> MakeNewToils()
        {
            Func<bool> noLongerDesignated = () => Animal?.Map?.designationManager?.DesignationOn(Animal, DesignationDefOf.Tame) == null;

            if (job.GetTarget(FoodIndex).HasThing)
            {
                yield return Toils_Goto.GotoThing(FoodIndex, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FoodIndex).FailOn(noLongerDesignated);
                yield return Toils_Haul.TakeToInventory(FoodIndex, job.count).FailOn(noLongerDesignated);
            }

            foreach (Toil toil in MakeAnimalInteractionToils())
            {
                toil.FailOn(noLongerDesignated);
                yield return toil;
            }

            yield return TryTameToil();
            yield return TryStartRopeToPenToil();
        }

        private Toil TryTameToil()
        {
            Toil toil = ToilMaker.MakeToil("LasherTryTame");
            toil.initAction = delegate
            {
                LasherAnimalHandlingUtility.DoTameAttempt(toil.actor, Animal);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 350;
            return toil;
        }

        private Toil TryStartRopeToPenToil()
        {
            Toil toil = ToilMaker.MakeToil("LasherTryStartRopeToPen");
            toil.initAction = delegate
            {
                Pawn animal = job.GetTarget(TargetIndex.A).Thing as Pawn;
                if (animal == null || !AnimalPenUtility.NeedsToBeManagedByRope(animal) || animal.Faction != Faction.OfPlayer || AnimalPenUtility.GetCurrentPenOf(animal, false) != null)
                {
                    return;
                }

                CompAnimalPenMarker pen = AnimalPenUtility.GetPenAnimalShouldBeTakenTo(pawn, animal, out string jobFailReason, false, true, true, true, RopingPriority.Closest);
                Job ropeJob = null;
                if (pen != null)
                {
                    ropeJob = WorkGiver_TakeToPen.MakeJob(pawn, animal, pen, true, RopingPriority.Closest, out jobFailReason);
                }

                if (ropeJob != null)
                {
                    pawn.jobs.StartJob(ropeJob, JobCondition.Succeeded);
                }
                else
                {
                    Messages.Message("MessageTameNoSuitablePens".Translate(animal.Named("ANIMAL")), animal, MessageTypeDefOf.NeutralEvent);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }

    public class JobDriver_LasherTrain : JobDriver_LasherInteractAnimal
    {
        protected override bool CanInteractNow => !TrainableUtility.TrainedTooRecently(Animal);

        public override IEnumerable<Toil> MakeNewToils()
        {
            Func<bool> noLongerTrainable = () => Animal?.training?.NextTrainableToTrain() == null;
            foreach (Toil toil in MakeAnimalInteractionToils())
            {
                toil.FailOn(noLongerTrainable);
                yield return toil;
            }

            yield return TryTrainToil();
        }

        private Toil TryTrainToil()
        {
            Toil toil = ToilMaker.MakeToil("LasherTryTrain");
            toil.initAction = delegate
            {
                LasherAnimalHandlingUtility.DoTrainAttempt(toil.actor, Animal);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 100;
            return toil;
        }
    }

    public abstract class JobDriver_LasherGatherAnimalBodyResources : JobDriver
    {
        private float gatherProgress;

        protected abstract float WorkTotal { get; }

        protected abstract CompHasGatherableBodyResource GetComp(Pawn animal);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref gatherProgress, "gatherProgress", 0f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOnNotCasualInterruptible(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil wait = ToilMaker.MakeToil("LasherGatherAnimalBodyResources");
            wait.initAction = delegate
            {
                Pawn animal = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                wait.actor.pather.StopDead();
                PawnUtility.ForceWait(animal, 15000, null, true);
            };
            wait.tickIntervalAction = delegate(int delta)
            {
                gatherProgress += LasherAnimalHandlingUtility.AnimalGatherSpeed(wait.actor) * delta;
                if (gatherProgress >= WorkTotal)
                {
                    GetComp((Pawn)job.GetTarget(TargetIndex.A).Thing).Gathered(pawn);
                    wait.actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };
            wait.AddFinishAction(delegate
            {
                Pawn animal = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                if (animal != null && animal.CurJobDef == JobDefOf.Wait_MaintainPosture)
                {
                    animal.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });
            wait.FailOnDespawnedOrNull(TargetIndex.A);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            wait.AddEndCondition(() => GetComp((Pawn)job.GetTarget(TargetIndex.A).Thing).ActiveAndFull ? JobCondition.Ongoing : JobCondition.Incompletable);
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.WithProgressBar(TargetIndex.A, () => gatherProgress / WorkTotal);
            yield return wait;
        }
    }

    public class JobDriver_LasherMilk : JobDriver_LasherGatherAnimalBodyResources
    {
        protected override float WorkTotal => 400f;

        protected override CompHasGatherableBodyResource GetComp(Pawn animal)
        {
            return animal.TryGetComp<CompMilkable>();
        }
    }

    public class JobDriver_LasherShear : JobDriver_LasherGatherAnimalBodyResources
    {
        protected override float WorkTotal => 1700f;

        protected override CompHasGatherableBodyResource GetComp(Pawn animal)
        {
            return animal.TryGetComp<CompShearable>();
        }
    }

    public class JobDriver_LasherSlaughter : JobDriver
    {
        private const TargetIndex VictimInd = TargetIndex.A;
        private const int SlaughterDuration = 180;

        private Pawn Victim => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnAggroMentalState(VictimInd);
            this.FailOn(() => !job.ignoreDesignations && !Victim.ShouldBeSlaughtered());
            yield return Toils_Goto.GotoThing(VictimInd, PathEndMode.Touch);
            yield return Toils_General.WaitWith(VictimInd, SlaughterDuration, true);
            yield return Toils_General.Do(delegate
            {
                ExecutionUtility.DoExecutionByCut(pawn, Victim);
                pawn.records?.Increment(RecordDefOf.AnimalsSlaughtered);
                pawn.MentalState?.Notify_SlaughteredTarget();
            });
        }
    }

    public class JobDriver_LasherReleaseAnimalToWild : JobDriver
    {
        private const TargetIndex AnimalInd = TargetIndex.A;
        private const int WaitForTicks = 180;

        private Pawn Animal => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(AnimalInd), job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Animal.Dead);
            this.FailOn(() => Animal.Faction != Faction.OfPlayer);
            this.FailOn(() => Animal.InAggroMentalState);
            this.FailOn(() => Animal.MapHeld.designationManager.DesignationOn(Animal, DesignationDefOf.ReleaseAnimalToWild) == null);
            yield return Toils_Reserve.Reserve(AnimalInd);
            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.OnCell);

            Toil waitToil = Toils_General.WaitWith(AnimalInd, WaitForTicks).WithProgressBarToilDelay(AnimalInd);
            yield return Toils_Jump.JumpIf(waitToil, () => Animal.Position.GetRegion(Map).District.TouchesMapEdge);
            yield return Toils_Haul.StartCarryThing(AnimalInd);

            Toil goToEdge = ToilMaker.MakeToil("LasherReleaseAnimalToWildGoToEdge");
            goToEdge.initAction = delegate
            {
                if (!JobDriver_ReleaseAnimalToWild.TryFindClosestOutsideCell(pawn.Position, pawn.Map, TraverseParms.For(pawn), pawn, out IntVec3 cell))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.pather.StartPath(cell, PathEndMode.OnCell);
            };
            goToEdge.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return goToEdge;

            yield return Toils_Haul.PlaceHauledThingInCell(AnimalInd, null, false);
            yield return waitToil;
            yield return Toils_General.Do(delegate
            {
                LasherAnimalHandlingUtility.DoReleaseAnimal(Animal, pawn);
            });
        }
    }
}
