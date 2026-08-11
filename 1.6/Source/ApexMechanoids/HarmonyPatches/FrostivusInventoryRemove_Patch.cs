using HarmonyLib;
using Verse;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.Notify_ItemRemoved))]
    internal static class FrostivusInventoryRemove_Patch
    {
        public static void Postfix(Pawn_InventoryTracker __instance, Thing item)
        {
            if (!FrostivusFoodPreservationUtility.IsFrostivus(__instance?.pawn))
            {
                return;
            }

            FrostivusUtility.RemoveDevouredHediff(item);
        }
    }
}
