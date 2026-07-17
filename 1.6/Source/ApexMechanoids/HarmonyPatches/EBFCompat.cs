using HarmonyLib;
using RimWorld;
using System.Linq;
using System.Reflection;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    // Reverse-patch compat with the Enhanced Body Framework (EBF).
    // When EBF is loaded, Harmony swaps the bodies of these methods for the
    // corresponding EBF.EBFEndpoints methods so we read EBF-adjusted stats.
    // When EBF is absent, the reverse patch fails and the vanilla fallbacks
    // (defined below) are used instead.

    [HarmonyPatch]
    public class MaxHealthGetter
    {
        public static bool Prepare()
        {
            // detect whether the EBF is loaded
            return LoadedModManager.RunningMods.Any((ModContentPack pack) => pack.PackageId == "V1024.EBFramework");
        }

        public static MethodBase TargetMethod()
        {
            // the correct EBF endpoint method to get the updated statistics
            return AccessTools.Method("EBF.EBFEndpoints:GetMaxHealthWithEBF");
        }

        [HarmonyReversePatch]
        public static float GetMaxHealth(BodyPartRecord record, Pawn pawn)
        {
            // if EBF is loaded, then Harmony replaces the body with the EBF endpoint method
            // else, the reverse-patch fails and the body remains the vanilla GetMaxHealth().
            return record.def.GetMaxHealth(pawn);
        }
    }

    [HarmonyPatch]
    public class PartHealthGetter
    {
        public static bool Prepare()
        {
            return LoadedModManager.RunningMods.Any((ModContentPack pack) => pack.PackageId == "V1024.EBFramework");
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method("EBF.EBFEndpoints:GetPartHealthWithEBF");
        }

        [HarmonyReversePatch]
        public static float GetPartHealth(BodyPartRecord record, Pawn pawn)
        {
            return pawn.health.hediffSet.GetPartHealth(record);
        }
    }
}
