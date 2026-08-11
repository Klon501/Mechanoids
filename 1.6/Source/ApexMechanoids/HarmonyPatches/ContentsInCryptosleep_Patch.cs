using HarmonyLib;
using Verse;

namespace ApexMechanoids
{
    [HarmonyPatch]
    internal static class ContentsInCryptosleep_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.ContentsInCryptosleep))]
        public static bool ContentsInCryptosleepPrefix(IThingHolder holder, ref bool __result)
        {
            if (FrostivusFoodPreservationUtility.IsFrostivusInventoryHolder(holder))
            {
                __result = true;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.ContentsSuspended))]
        public static bool ContentsSuspendedPrefix(IThingHolder holder, ref bool __result)
        {
            if (FrostivusFoodPreservationUtility.IsFrostivusInventoryHolder(holder))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}
