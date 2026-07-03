using HarmonyLib;
using RimWorld;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(Bill_Mech), "WorkSpeedMultiplier", MethodType.Getter)]
    public class Patch_Bill_Mech_WorkSpeedMultiplier
    {
        public static void Postfix(Bill_Mech __instance, ref float __result) 
        {
            float factor = __instance?.Gestator?.GetStatValue(ApexDefsOf.APM_GestationFactor) ?? 1f;

            __result *= factor;
        }
    }
}
