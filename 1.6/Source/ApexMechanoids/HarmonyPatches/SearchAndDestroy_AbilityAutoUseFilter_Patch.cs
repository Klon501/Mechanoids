using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.AICastableAbilities))]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_AbilityAutoUseFilter_Patch
    {
        [HarmonyPostfix]
        private static void AICastableAbilitiesPostfix(Pawn_AbilityTracker __instance, ref List<Ability> __result)
        {
            if (__result == null || __result.Count == 0)
            {
                return;
            }

            Pawn pawn = __instance.pawn;
            bool searchAndDestroyEnabled = SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn);
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                Ability ability = __result[i];
                if (SearchAndDestroyCompatUtility.AutoUseBlockedBecausePawnNotAwake(pawn, ability)
                    || RavagerArtilleryUtility.AutoAbilityBlockedByArtilleryToggle(pawn, ability)
                    || GazerLaserUtility.AutoAbilityBlockedByLaserToggle(pawn, ability)
                    || (searchAndDestroyEnabled && SearchAndDestroyCompatUtility.AutoUseDisabledWithSearchAndDestroy(pawn, ability)))
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_AbilityJobStability_Patch
    {
        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(__result.Job);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_AbilityJobStartStability_Patch
    {
        [HarmonyPrefix]
        private static void StartJobPrefix(Pawn_JobTracker __instance, Job newJob)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(newJob);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_DuelMentalStateMemory_Patch
    {
        [HarmonyPrefix]
        private static void TryStartMentalStatePrefix(MentalStateHandler __instance, MentalStateDef stateDef)
        {
            if (stateDef?.defName != "APM_Duel" && stateDef?.defName != "APM_Duel_Boss")
            {
                return;
            }

            SearchAndDestroyDuelStateMemory.Capture(SearchAndDestroyCompatUtility.GetPawn(__instance));
        }
    }

    internal static class SearchAndDestroyDuelStateMemory
    {
        private static readonly Dictionary<Pawn, bool> pendingRestoreStates = new Dictionary<Pawn, bool>();

        public static void Capture(Pawn pawn)
        {
            if (SearchAndDestroyCompatUtility.TryGetSearchAndDestroyEnabledRaw(pawn, out bool enabled) && enabled)
            {
                pendingRestoreStates[pawn] = true;
            }
            else if (pawn != null)
            {
                pendingRestoreStates.Remove(pawn);
            }
        }

        public static bool Consume(Pawn pawn)
        {
            if (pawn == null || !pendingRestoreStates.TryGetValue(pawn, out bool enabled))
            {
                return false;
            }

            pendingRestoreStates.Remove(pawn);
            return enabled;
        }
    }

    internal static class SearchAndDestroyCompatUtility
    {
        private const string SearchAndDestroyPackageId = "memegoddess.searchanddestroy";
        private const string ApexAbilityDefPrefix = "APM_";

        private static readonly FieldInfo jobTrackerPawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
        private static readonly FieldInfo mentalStateHandlerPawnField = AccessTools.Field(typeof(MentalStateHandler), "pawn");

        private static bool reflectionInitialized;
        private static bool reflectionAvailable;
        private static PropertyInfo searchAndDestroyInstanceProperty;
        private static PropertyInfo extendedDataStorageProperty;
        private static MethodInfo getExtendedDataForMethod;
        private static FieldInfo searchAndDestroyEnabledField;

        public static Pawn GetPawn(Pawn_JobTracker jobTracker)
        {
            return jobTrackerPawnField?.GetValue(jobTracker) as Pawn;
        }

        public static Pawn GetPawn(MentalStateHandler mentalStateHandler)
        {
            return mentalStateHandlerPawnField?.GetValue(mentalStateHandler) as Pawn;
        }

        public static bool SearchAndDestroyEnabledFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Drafted || !ModsConfig.IsActive(SearchAndDestroyPackageId))
            {
                return false;
            }

            return TryGetSearchAndDestroyEnabledRaw(pawn, out bool enabled) && enabled;
        }

        public static bool TryGetSearchAndDestroyEnabledRaw(Pawn pawn, out bool enabled)
        {
            enabled = false;
            if (pawn == null || !ModsConfig.IsActive(SearchAndDestroyPackageId) || !EnsureReflection())
            {
                return false;
            }

            try
            {
                object pawnData = GetSearchAndDestroyPawnData(pawn);
                object enabledValue = searchAndDestroyEnabledField.GetValue(pawnData);
                if (enabledValue is bool enabledBool)
                {
                    enabled = enabledBool;
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        public static bool TrySetSearchAndDestroyEnabled(Pawn pawn, bool enabled)
        {
            if (pawn == null || !ModsConfig.IsActive(SearchAndDestroyPackageId) || !EnsureReflection())
            {
                return false;
            }

            try
            {
                object pawnData = GetSearchAndDestroyPawnData(pawn);
                searchAndDestroyEnabledField.SetValue(pawnData, enabled);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool AutoUseDisabledWithSearchAndDestroy(Pawn pawn, Ability ability)
        {
            List<AbilityDef> disabledAbilities = pawn?.def?.GetModExtension<DefModExtension_SearchAndDestroyMech>()?.disabledAutoUseAbilitiesWhenSearchAndDestroy;
            return ability?.def != null && disabledAbilities != null && disabledAbilities.Contains(ability.def);
        }

        public static bool AutoUseBlockedBecausePawnNotAwake(Pawn pawn, Ability ability)
        {
            return pawn != null
                && pawn.RaceProps?.IsMechanoid == true
                && !Utils.IsAwakeAndNotDormant(pawn)
                && IsApexAbility(ability);
        }

        public static bool ProtectApexAbilityJobFromOverride(Job job)
        {
            if (!ShouldProtectApexAbilityJob(job))
            {
                return false;
            }

            job.expiryInterval = 0;
            job.checkOverrideOnExpire = false;
            return true;
        }

        private static bool ShouldProtectApexAbilityJob(Job job)
        {
            AbilityDef abilityDef = job?.ability?.def;
            return abilityDef != null
                && !job.playerForced
                && job.verbToUse is Verb_CastAbility
                && abilityDef.defName.StartsWith(ApexAbilityDefPrefix, StringComparison.Ordinal);
        }

        private static bool IsApexAbility(Ability ability)
        {
            return ability?.def?.defName?.StartsWith(ApexAbilityDefPrefix, StringComparison.Ordinal) == true;
        }

        private static object GetSearchAndDestroyPawnData(Pawn pawn)
        {
            object searchAndDestroy = searchAndDestroyInstanceProperty.GetValue(null);
            object extendedDataStorage = extendedDataStorageProperty.GetValue(searchAndDestroy);
            return getExtendedDataForMethod.Invoke(extendedDataStorage, new object[] { pawn });
        }

        private static bool EnsureReflection()
        {
            if (reflectionInitialized)
            {
                return reflectionAvailable;
            }

            reflectionInitialized = true;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Type baseType = GenTypes.GetTypeInAnyAssembly("SearchAndDestroy.Base");
            Type storageType = GenTypes.GetTypeInAnyAssembly("SearchAndDestroy.Storage.ExtendedDataStorage");
            Type pawnDataType = GenTypes.GetTypeInAnyAssembly("SearchAndDestroy.Storage.ExtendedPawnData");

            searchAndDestroyInstanceProperty = baseType?.GetProperty("Instance", flags);
            extendedDataStorageProperty = baseType?.GetProperty("ExtendedDataStorage", flags);
            getExtendedDataForMethod = storageType?.GetMethod("GetExtendedDataFor", flags, null, new[] { typeof(Pawn) }, null);
            searchAndDestroyEnabledField = pawnDataType?.GetField("SD_enabled", flags);

            reflectionAvailable = searchAndDestroyInstanceProperty != null
                && extendedDataStorageProperty != null
                && getExtendedDataForMethod != null
                && searchAndDestroyEnabledField != null;
            return reflectionAvailable;
        }
    }
}
