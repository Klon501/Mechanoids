using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class LasherAnimalHandlingUtility
    {
        public const string LasherDefName = "APM_Mech_Lasher";
        public const int AnimalsSkillLevel = 10;

        private static readonly SimpleCurve TameChanceFactorCurveWildness = new SimpleCurve
        {
            new CurvePoint(1f, 0f),
            new CurvePoint(0.5f, 1f),
            new CurvePoint(0f, 2f)
        };

        public static bool IsLasher(Pawn pawn)
        {
            return pawn?.def?.defName == LasherDefName;
        }

        public static bool CanLasherWork(Pawn pawn)
        {
            return IsLasher(pawn) && pawn.Spawned && pawn.Map != null && !pawn.Dead && !pawn.Downed && pawn.Faction != null;
        }

        public static bool CanInteractWithAnimal(Pawn pawn, Pawn animal, bool forced)
        {
            if (CanInteractWithAnimal(pawn, animal, out string failReason, forced))
            {
                return true;
            }

            if (failReason != null)
            {
                JobFailReason.Is(failReason);
            }

            return false;
        }

        public static bool CanInteractWithAnimal(Pawn pawn, Pawn animal, out string failReason, bool forced, bool canInteractWhileSleeping = false, bool canInteractWhileRoaming = false)
        {
            failReason = null;
            if (!CanLasherWork(pawn) || animal == null || animal.Destroyed || animal.Dead || animal.Map != pawn.Map)
            {
                return false;
            }

            if (!pawn.CanReserve(animal, 1, -1, null, forced))
            {
                return false;
            }

            if (animal.Downed)
            {
                failReason = "CantInteractAnimalDowned".Translate();
                return false;
            }

            if (!animal.Awake() && !canInteractWhileSleeping)
            {
                failReason = "CantInteractAnimalAsleep".Translate();
                return false;
            }

            if (!animal.CanCasuallyInteractNow(false, canInteractWhileSleeping, canInteractWhileRoaming))
            {
                failReason = "CantInteractAnimalBusy".Translate();
                return false;
            }

            int minimumHandlingSkill = TrainableUtility.MinimumHandlingSkill(animal);
            if (minimumHandlingSkill > AnimalsSkillLevel)
            {
                failReason = "AnimalsSkillTooLow".Translate(minimumHandlingSkill);
                return false;
            }

            return true;
        }

        public static bool HasFoodToInteractAnimal(Pawn pawn, Pawn animal)
        {
            if (pawn?.inventory?.innerContainer == null || animal == null)
            {
                return false;
            }

            int feedsAvailable = 0;
            float nutritionPerFeed = JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal);
            float nutritionAccumulated = 0f;
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                Thing food = pawn.inventory.innerContainer[i];
                if (!animal.WillEat(food, pawn) || food.def.ingestible == null || food.def.ingestible.preferability > FoodPreferability.RawTasty || food.def.IsDrug)
                {
                    continue;
                }

                for (int j = 0; j < food.stackCount; j++)
                {
                    nutritionAccumulated += food.GetStatValue(StatDefOf.Nutrition);
                    if (nutritionAccumulated >= nutritionPerFeed)
                    {
                        feedsAvailable++;
                        nutritionAccumulated = 0f;
                    }

                    if (feedsAvailable >= 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Job TakeFoodForAnimalInteractJob(Pawn pawn, Pawn animal)
        {
            Thing food = FoodUtility.BestFoodSourceOnMap(pawn, animal, false, out ThingDef foodDef, FoodPreferability.RawTasty, allowPlant: false, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: false, ignoreReservations: false, calculateWantedStackCount: false, minPrefOverride: FoodPreferability.Undefined, minNutrition: JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal) * 2f * 4f);
            if (food == null)
            {
                return null;
            }

            float wantedNutrition = JobDriver_InteractAnimal.RequiredNutritionPerFeed(animal) * 2f * 4f;
            float nutrition = FoodUtility.GetNutrition(animal, food, foodDef);
            Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, food);
            job.count = FoodUtility.StackCountForNutrition(wantedNutrition, nutrition);
            return job;
        }

        public static float TameChance(Pawn lasher, Pawn animal)
        {
            if (DebugSettings.instantRecruit)
            {
                return 1f;
            }

            float chance = 0.04f + (AnimalsSkillLevel * 0.03f);
            chance *= TameChanceFactorCurveWildness.Evaluate(animal.GetStatValue(StatDefOf.Wildness));
            if (animal.IsPrisonerInPrisonCell())
            {
                chance *= 0.6f;
            }

            if (lasher.Ideo != null && lasher.Ideo.IsVeneratedAnimal(animal))
            {
                chance *= 2f;
            }

            return chance;
        }

        public static float TrainChance(Pawn animal)
        {
            float chance = 0.10f + (AnimalsSkillLevel * 0.05f);
            chance *= (float)GenMath.LerpDouble(0f, 1f, 1.5f, 0.5f, animal.GetStatValue(StatDefOf.Wildness));
            return Mathf.Clamp01(chance);
        }

        public static float AnimalGatherSpeed(Pawn lasher)
        {
            float skillFactor = 0.04f + (AnimalsSkillLevel * 0.12f);
            float workSpeed = lasher?.GetStatValue(StatDefOf.WorkSpeedGlobal) ?? 1f;
            return Mathf.Max(0.1f, skillFactor * workSpeed);
        }

        public static void DoTameAttempt(Pawn lasher, Pawn animal)
        {
            if (lasher == null || animal == null || !animal.Spawned || !animal.Awake())
            {
                return;
            }

            float chance = TameChance(lasher, animal);
            if (Rand.Chance(chance))
            {
                DoTameSuccess(lasher, animal);
            }
            else
            {
                MoteMaker.ThrowText((lasher.DrawPos + animal.DrawPos) / 2f, lasher.Map, "TextMote_TameFail".Translate(chance.ToStringPercent()), 8f);
                animal.mindState?.CheckStartMentalStateBecauseRecruitAttempted(lasher);
            }
        }

        public static void DoTrainAttempt(Pawn lasher, Pawn animal)
        {
            if (lasher == null || animal?.training == null || !animal.Spawned || !animal.Awake())
            {
                return;
            }

            TrainableDef trainableDef = animal.training.NextTrainableToTrain();
            if (trainableDef == null)
            {
                Log.ErrorOnce("Attempted to train untrainable animal", 7842936);
                return;
            }

            float chance = TrainChance(animal);
            string text;
            if (Rand.Value < chance)
            {
                animal.training.Train(trainableDef, lasher);
                animal.caller?.DoCall();
                text = "TextMote_TrainSuccess".Translate(trainableDef.LabelCap, chance.ToStringPercent());
            }
            else
            {
                text = "TextMote_TrainFail".Translate(trainableDef.LabelCap, chance.ToStringPercent());
            }

            text += "\n" + animal.training.GetSteps(trainableDef) + " / " + trainableDef.steps;
            MoteMaker.ThrowText((lasher.DrawPos + animal.DrawPos) / 2f, lasher.Map, text, 5f);
        }

        public static void DoReleaseAnimal(Pawn animal, Pawn releasedBy)
        {
            if (animal == null || releasedBy == null)
            {
                return;
            }

            PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(animal, null, PawnDiedOrDownedThoughtsKind.ReleasedToWild);
            Designation designation = animal.Map?.designationManager?.DesignationOn(animal, DesignationDefOf.ReleaseAnimalToWild);
            if (designation != null)
            {
                animal.Map.designationManager.RemoveDesignation(designation);
            }

            animal.SetFaction(null);
            animal.ownership?.UnclaimAll();
            Messages.Message("MessageAnimalReturnedWildReleased".Translate(animal.LabelShort, animal), releasedBy, MessageTypeDefOf.NeutralEvent);
        }

        private static void DoTameSuccess(Pawn lasher, Pawn animal)
        {
            string previousLabel = animal.LabelIndefinite();
            bool hadName = animal.Name != null;
            RecruitUtility.Recruit(animal, lasher.Faction ?? Faction.OfPlayer, lasher);

            Designation designation = animal.Map?.designationManager?.DesignationOn(animal, DesignationDefOf.Tame);
            if (designation != null)
            {
                animal.Map.designationManager.RemoveDesignation(designation);
            }

            if (!hadName && animal.Name != null)
            {
                Messages.Message("MessageTameAndNameSuccess".Translate(lasher.LabelShort, previousLabel, animal.Name.ToStringFull, lasher.Named("RECRUITER"), animal.Named("RECRUITEE")).AdjustedFor(animal), animal, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("MessageTameSuccess".Translate(lasher.LabelShort, previousLabel, lasher.Named("RECRUITER")), animal, MessageTypeDefOf.PositiveEvent);
            }

            if (lasher.Spawned && animal.Spawned)
            {
                MoteMaker.ThrowText((lasher.DrawPos + animal.DrawPos) / 2f, lasher.Map, "TextMote_TameSuccess".Translate(), 8f);
            }

            lasher.records?.Increment(RecordDefOf.AnimalsTamed);
            animal.caller?.DoCall();
        }
    }
}
