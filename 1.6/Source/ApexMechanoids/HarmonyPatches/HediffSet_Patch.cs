using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class HediffSet_Patch
    {
        [HarmonyLib.HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetRandomNotMissingPart))]
        internal static class GetRandomNotMissingPart
        {
            private static void Postfix(HediffSet __instance, ref BodyPartRecord __result)
            {
                if (!DamageWorker_AddInjury_Patch.pickShield)
                {
                    return;
                }

                var nonMissingBodyPart = Utils.GetNonMissingBodyPart(__instance.pawn, ApexDefsOf.APM_AegisShield, DamageWorker_AddInjury_Patch.whichShield);
                if (nonMissingBodyPart != null)
                {
                    __result = nonMissingBodyPart;
                }
            }
        }
    }
}
