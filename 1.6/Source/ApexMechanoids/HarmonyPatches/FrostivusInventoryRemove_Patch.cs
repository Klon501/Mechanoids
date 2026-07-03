using HarmonyLib;
using Verse;

namespace ApexMechanoids
{
    // Removes the APM_Hediff_Devoured hediff when a pawn/corpse leaves a Frostivus inventory
    [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.Notify_ItemRemoved))]
    internal static class FrostivusInventoryRemove_Patch
    {
        public static void Postfix(Pawn_InventoryTracker __instance, Thing item)
        {
            if (__instance.pawn.def != ApexDefsOf.APM_Mech_Frostivus)
                return;

            if (item is Pawn removedPawn)
                FrostivusUtility.RemoveDevouredHediff(removedPawn);
            else if (item is Corpse corpse && corpse.InnerPawn != null)
                FrostivusUtility.RemoveDevouredHediff(corpse.InnerPawn);
        }
    }
}
