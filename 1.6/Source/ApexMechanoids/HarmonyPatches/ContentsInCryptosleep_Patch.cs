using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace ApexMechanoids
{
    [HarmonyLib.HarmonyPatch]
    internal static class ContentsInCryptosleep_Patch
    {
        // Prefix patch for ThingOwnerUtility.ContentsInCryptosleep
        // Runs before the original method and can return early
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.ContentsInCryptosleep))]
        public static bool ContentsInCryptosleepPrefix(IThingHolder holder, ref bool __result)
        {
            // Check our custom condition first
            if (IsCryptosleepContainer(holder))
            {
                __result = true;
                return false; // Skip original method
            }
            return true; // Continue with original method
        }

        // Prefix patch for ThingOwnerUtility.ContentsSuspended
        // Runs before the original method and can return early
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.ContentsSuspended))]
        public static bool ContentsSuspendedPrefix(IThingHolder holder, ref bool __result)
        {
            // Check our custom condition first
            if (IsCryptosleepContainer(holder))
            {
                __result = true;
                return false; // Skip original method
            }
            return true; // Continue with original method
        }

        // Helper method to check if the holder is our custom cryptosleep container
        public static bool IsCryptosleepContainer(IThingHolder holder)
        {
            return (holder as Pawn_InventoryTracker)?.pawn.def == ApexDefsOf.APM_Mech_Frostivus;
        }
    }
}
