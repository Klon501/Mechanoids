using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    // Fix NullReferenceException in CompProperties_Refuelable.SpecialDisplayStats
    [HarmonyPatch(typeof(CompProperties_Refuelable), nameof(CompProperties_Refuelable.SpecialDisplayStats))]
    public static class Patch_CompProperties_Refuelable_SpecialDisplayStats
    {
        public static bool Prefix(CompProperties_Refuelable __instance, ref IEnumerable<StatDrawEntry> __result)
        {
            // Skip original method and provide our own safe implementation
            var list = new List<StatDrawEntry>();
            
            try
            {
                // Safely check if fuelFilter exists and allows Uranium
                if (__instance?.fuelFilter != null)
                {
                    // Check if Uranium is allowed and add special note
                    if (__instance.fuelFilter.Allows(ThingDefOf.Uranium))
                    {
                        list.Add(new StatDrawEntry(StatCategoryDefOf.Basics,
                            "Can use uranium",
                            "Yes",
                            "This device can be fueled with uranium.",
                            0));
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ApexMechanoids] Error in CompProperties_Refuelable.SpecialDisplayStats: {ex}");
            }
            
            __result = list;
            return false; // Skip original method
        }
    }
}
