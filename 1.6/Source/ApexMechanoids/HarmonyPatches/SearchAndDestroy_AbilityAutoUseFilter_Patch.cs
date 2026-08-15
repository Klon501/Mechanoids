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

    internal static class SearchAndDestroyCompatUtility
    {
        private const string SearchAndDestroyPackageId = "memegoddess.searchanddestroy";
        private const string ApexAbilityDefPrefix = "APM_";

        private static readonly FieldInfo pawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        private static bool reflectionInitialized;
        private static bool reflectionAvailable;
        private static PropertyInfo searchAndDestroyInstanceProperty;
        private static PropertyInfo extendedDataStorageProperty;
        private static MethodInfo getExtendedDataForMethod;
        private static FieldInfo searchAndDestroyEnabledField;

        public static Pawn GetPawn(Pawn_JobTracker jobTracker)
        {
            return pawnField?.GetValue(jobTracker) as Pawn;
        }

        public static bool SearchAndDestroyEnabledFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Drafted || !ModsConfig.IsActive(SearchAndDestroyPackageId))
            {
                return false;
            }

            return TryGetSearchAndDestroyEnabled(pawn);
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
                && !pawn.Awake()
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

        private static bool TryGetSearchAndDestroyEnabled(Pawn pawn)
        {
            if (!EnsureReflection())
            {
                return false;
            }

            try
            {
                object searchAndDestroy = searchAndDestroyInstanceProperty.GetValue(null);
                object extendedDataStorage = extendedDataStorageProperty.GetValue(searchAndDestroy);
                object pawnData = getExtendedDataForMethod.Invoke(extendedDataStorage, new object[] { pawn });
                object enabled = searchAndDestroyEnabledField.GetValue(pawnData);
                return enabled is bool enabledBool && enabledBool;
            }
            catch (Exception)
            {
                return false;
            }
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
