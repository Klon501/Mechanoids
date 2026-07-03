using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    [HarmonyLib.HarmonyPatch(typeof(CompRottable), nameof(CompRottable.Active), HarmonyLib.MethodType.Getter)] //Or we can patch ShouldTakeRotDamage()
    internal static class CompRottable_Patch
    {
        public static void Postfix(CompRottable __instance, ref bool __result)
        {
            if (__result && (__instance.parent.ParentHolder as Pawn_InventoryTracker)?.pawn?.def == ApexDefsOf.APM_Mech_Frostivus)
            {
                __result = false;
            }
        }
    }
}
