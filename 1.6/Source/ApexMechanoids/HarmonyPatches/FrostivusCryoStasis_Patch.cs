using HarmonyLib;
using Verse;

namespace ApexMechanoids
{
    // Stasis is per-pawn, keyed off the devoured hediff. Doing it at the holder level (via
    // ThingOwnerUtility.ContentsSuspended) froze the whole Frostivus inventory instead of just the
    // swallowed pawn, and got hammered every tick. Pawn.TickInterval bails on Suspended.
    [HarmonyPatch]
    internal static class FrostivusCryoStasis_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Thing), nameof(Thing.Suspended), MethodType.Getter)]
        public static void SuspendedPostfix(Thing __instance, ref bool __result)
        {
            if (!__result && !__instance.Spawned && FrostivusUtility.IsInCryoStasis(__instance))
            {
                __result = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Thing), nameof(Thing.InCryptosleep), MethodType.Getter)]
        public static void InCryptosleepPostfix(Thing __instance, ref bool __result)
        {
            if (!__result && !__instance.Spawned && FrostivusUtility.IsInCryoStasis(__instance))
            {
                __result = true;
            }
        }
    }
}
