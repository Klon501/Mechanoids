using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.AICastableAbilities))]
    public static class CelerusAbilityAutoUseFilter_Patch
    {
        [HarmonyPostfix]
        private static void AICastableAbilitiesPostfix(Pawn_AbilityTracker __instance, ref List<Ability> __result)
        {
            if (__result == null || __result.Count == 0)
            {
                return;
            }

            Pawn pawn = __instance.pawn;
            if (!CelerusAbilityAutoUseUtility.AutoUseBlockedInGenericAI(pawn))
            {
                return;
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                if (CelerusAbilityAutoUseUtility.IsCelerusAbility(__result[i]))
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    internal static class CelerusAbilityAutoUseUtility
    {
        public static bool AutoUseBlockedInGenericAI(Pawn pawn)
        {
            return CelerusRaidUtility.IsCelerus(pawn)
                && (!pawn.IsPlayerControlled
                    || AutoUseBlockedInEscortMode(pawn)
                    || SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn));
        }

        public static bool AutoUseBlockedInEscortMode(Pawn pawn)
        {
            return CelerusRaidUtility.IsCelerus(pawn)
                && pawn.IsColonyMechPlayerControlled
                && pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort;
        }

        public static bool IsCelerusAbility(Ability ability)
        {
            AbilityDef def = ability?.def;
            return CelerusRaidUtility.IsCelerusAbility(def);
        }
    }
}
