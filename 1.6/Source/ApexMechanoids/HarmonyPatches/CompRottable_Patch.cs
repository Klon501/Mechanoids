using HarmonyLib;
using RimWorld;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.Active), MethodType.Getter)]
    internal static class CompRottable_Patch
    {
        public static void Postfix(CompRottable __instance, ref bool __result)
        {
            if (__result
                && (FrostivusFoodPreservationUtility.IsRotPreservedByFrostivus(__instance.parent)
                    || FrostivusCaravanFoodCalculatorUtility.IsFoodPreservedInTransferableRotContext(__instance.parent)))
            {
                __result = false;
            }
        }
    }
}
