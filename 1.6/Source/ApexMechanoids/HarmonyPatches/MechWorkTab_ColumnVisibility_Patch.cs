using HarmonyLib;
using RimWorld;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), nameof(PawnColumnWorker_WorkPriority.VisibleCurrently), MethodType.Getter)]
    internal static class MechWorkTab_ColumnVisibility_Patch
    {
        private static void Postfix(PawnColumnWorker_WorkPriority __instance, ref bool __result)
        {
            if (__result || !MechWorkTabCompat.HasGeneratedColumns)
            {
                return;
            }

            if (MechWorkTabCompat.IsGeneratedColumn(__instance.def))
            {
                __result = true;
            }
        }
    }
}
