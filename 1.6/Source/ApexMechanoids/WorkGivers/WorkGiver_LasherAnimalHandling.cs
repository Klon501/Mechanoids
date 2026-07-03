using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_LasherTame : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Tame))
            {
                yield return designation.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn) || !pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Tame);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn animal = t as Pawn;
            if (animal == null || !TameUtility.CanTame(animal))
            {
                return null;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Tame) == null || !LasherAnimalHandlingUtility.CanInteractWithAnimal(pawn, animal, forced))
            {
                return null;
            }

            if (TameUtility.TriedToTameTooRecently(animal))
            {
                JobFailReason.Is("AnimalInteractedTooRecently".Translate());
                return null;
            }

            Thing food = null;
            int count = -1;
            if (animal.RaceProps.EatsFood && animal.needs?.food != null && !LasherAnimalHandlingUtility.HasFoodToInteractAnimal(pawn, animal))
            {
                food = FoodUtility.BestFoodSourceOnMap(pawn, animal, false, out ThingDef foodDef, FoodPreferability.RawTasty, allowPlant: false, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: false, ignoreReservations: false, calculateWantedStackCount: false, minPrefOverride: FoodPreferability.Undefined, minNutrition: JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal) * 2f * 4f);
                if (food == null)
                {
                    JobFailReason.Is("NoFood".Translate());
                    return null;
                }

                float wantedNutrition = JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal) * 2f * 4f;
                float nutrition = FoodUtility.GetNutrition(animal, food, foodDef);
                count = FoodUtility.StackCountForNutrition(wantedNutrition, nutrition);
            }

            Job job = JobMaker.MakeJob(ApexDefsOf.APM_LasherTame, t, LocalTargetInfo.Invalid, food);
            job.count = count;
            return job;
        }
    }

    public class WorkGiver_LasherTrain : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn animal = t as Pawn;
            if (animal == null || !animal.IsAnimal || animal.RaceProps.animalType == AnimalType.Dryad || animal.Faction != pawn.Faction || animal.training == null)
            {
                return null;
            }

            if (animal.training.NextTrainableToTrain() == null || !LasherAnimalHandlingUtility.CanInteractWithAnimal(pawn, animal, forced))
            {
                return null;
            }

            if (animal.RaceProps.EatsFood && animal.needs?.food != null && !LasherAnimalHandlingUtility.HasFoodToInteractAnimal(pawn, animal))
            {
                Job takeFoodJob = LasherAnimalHandlingUtility.TakeFoodForAnimalInteractJob(pawn, animal);
                if (takeFoodJob == null)
                {
                    JobFailReason.Is("NoUsableFood".Translate());
                }

                return takeFoodJob;
            }

            if (TrainableUtility.TrainedTooRecently(animal))
            {
                JobFailReason.Is("AnimalInteractedTooRecently".Translate());
                return null;
            }

            return JobMaker.MakeJob(ApexDefsOf.APM_LasherTrain, t);
        }
    }

    public abstract class WorkGiver_LasherGatherAnimalBodyResources : WorkGiver_Scanner
    {
        protected abstract JobDef JobDef { get; }

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        protected abstract CompHasGatherableBodyResource GetComp(Pawn animal);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!LasherAnimalHandlingUtility.CanLasherWork(pawn))
            {
                return true;
            }

            List<Pawn> pawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].IsAnimal)
                {
                    CompHasGatherableBodyResource comp = GetComp(pawns[i]);
                    if (comp != null && comp.ActiveAndFull)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn animal = t as Pawn;
            if (!LasherAnimalHandlingUtility.CanLasherWork(pawn) || animal == null || !animal.IsAnimal)
            {
                return false;
            }

            CompHasGatherableBodyResource comp = GetComp(animal);
            return comp != null && comp.ActiveAndFull && !animal.Downed && (animal.roping == null || !animal.roping.IsRopedByPawn) && animal.CanCasuallyInteractNow() && pawn.CanReserve(animal, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDef, t);
        }
    }

    public class WorkGiver_LasherMilk : WorkGiver_LasherGatherAnimalBodyResources
    {
        protected override JobDef JobDef => ApexDefsOf.APM_LasherMilk;

        protected override CompHasGatherableBodyResource GetComp(Pawn animal)
        {
            return animal.TryGetComp<CompMilkable>();
        }
    }

    public class WorkGiver_LasherShear : WorkGiver_LasherGatherAnimalBodyResources
    {
        protected override JobDef JobDef => ApexDefsOf.APM_LasherShear;

        protected override CompHasGatherableBodyResource GetComp(Pawn animal)
        {
            return animal.TryGetComp<CompShearable>();
        }
    }

    public class WorkGiver_LasherSlaughter : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Slaughter))
            {
                yield return designation.target.Thing;
            }

            foreach (Pawn animal in pawn.Map.autoSlaughterManager.AnimalsToSlaughter)
            {
                yield return animal;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn) || (!pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Slaughter) && pawn.Map.autoSlaughterManager.AnimalsToSlaughter.Count == 0);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn animal = t as Pawn;
            if (!LasherAnimalHandlingUtility.CanLasherWork(pawn) || animal == null || !animal.IsAnimal || !animal.ShouldBeSlaughtered() || pawn.Faction != t.Faction || animal.InAggroMentalState || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                JobFailReason.Is("IsIncapableOfViolenceShort".Translate(pawn));
                return false;
            }

            if (ModsConfig.IdeologyActive && !new HistoryEvent(HistoryEventDefOf.SlaughteredAnimal, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
            {
                return false;
            }

            if (HistoryEventUtility.IsKillingInnocentAnimal(pawn, animal) && !new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
            {
                return false;
            }

            if (pawn.Ideo != null && pawn.Ideo.IsVeneratedAnimal(animal) && !new HistoryEvent(HistoryEventDefOf.SlaughteredVeneratedAnimal, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(ApexDefsOf.APM_LasherSlaughter, t);
        }
    }

    public class WorkGiver_LasherReleaseAnimalsToWild : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.ReleaseAnimalToWild))
            {
                yield return designation.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn) || !pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.ReleaseAnimalToWild);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn animal = t as Pawn;
            if (!LasherAnimalHandlingUtility.CanLasherWork(pawn) || animal == null || !animal.IsAnimal || pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.ReleaseAnimalToWild) == null || pawn.Faction != t.Faction || animal.InAggroMentalState || animal.Dead || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (!JobDriver_ReleaseAnimalToWild.TryFindClosestOutsideCell(t.Position, t.Map, TraverseParms.For(pawn), pawn, out IntVec3 _))
            {
                JobFailReason.Is("NoReachableOutsideCell".Translate());
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = JobMaker.MakeJob(ApexDefsOf.APM_LasherReleaseToWild, t);
            job.count = 1;
            return job;
        }
    }

    public class WorkGiver_LasherTakeToPen : WorkGiver_TakeToPen
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return LasherAnimalHandlingUtility.CanLasherWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_LasherTakeRoamingAnimalsToPen : WorkGiver_TakeRoamingAnimalsToPen
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return LasherAnimalHandlingUtility.CanLasherWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_LasherRebalanceAnimalsInPens : WorkGiver_RebalanceAnimalsInPens
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return LasherAnimalHandlingUtility.CanLasherWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_LasherFeedPatientAnimals : WorkGiver_FeedPatient
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !LasherAnimalHandlingUtility.CanLasherWork(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return LasherAnimalHandlingUtility.CanLasherWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return LasherAnimalHandlingUtility.CanLasherWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }
}
