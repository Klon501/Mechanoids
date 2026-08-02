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
                if (RavagerArtilleryUtility.AutoAbilityBlockedByArtilleryToggle(pawn, ability)
                    || (searchAndDestroyEnabled && SearchAndDestroyCompatUtility.AutoUseDisabledWithSearchAndDestroy(pawn, ability)))
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    internal static class SearchAndDestroyCompatUtility
    {
        private const string SearchAndDestroyPackageId = "memegoddess.searchanddestroy";

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
