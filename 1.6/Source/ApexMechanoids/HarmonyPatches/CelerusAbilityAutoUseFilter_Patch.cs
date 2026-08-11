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
            if (!CelerusAbilityAutoUseUtility.AutoUseBlockedInEscortMode(pawn))
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
        public static bool AutoUseBlockedInEscortMode(Pawn pawn)
        {
            return IsCelerus(pawn)
                && pawn.IsColonyMechPlayerControlled
                && pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort;
        }

        public static bool IsCelerusAbility(Ability ability)
        {
            AbilityDef def = ability?.def;
            return def == ApexDefsOf.APM_CelerusBlink
                || def == ApexDefsOf.APM_Ability_SmokeScreen
                || def == ApexDefsOf.APM_Ability_SmokeScreen_Boss;
        }

        private static bool IsCelerus(Pawn pawn)
        {
            return pawn?.def == ApexDefsOf.APM_Mech_Celerus || pawn?.def == ApexDefsOf.APM_Mech_CelerusB;
        }
    }
}
