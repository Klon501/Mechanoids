using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(WornGraphicData), nameof(WornGraphicData.BeltOffsetAt))]
    internal static class Patch_MechamancerPackChildOffset
    {
        private static WornGraphicData cachedData;
        private static bool initialized;

        private static WornGraphicData MechamancerPackWornGraphicData
        {
            get
            {
                if (!initialized)
                {
                    initialized = true;
                    cachedData = DefDatabase<ThingDef>.GetNamedSilentFail("APM_Apparel_MechamancerPack")
                        ?.apparel?.wornGraphicData;
                }
                return cachedData;
            }
        }

        public static void Postfix(WornGraphicData __instance, Rot4 facing, BodyTypeDef bodyType, ref Vector2 __result)
        {
            if (__instance != MechamancerPackWornGraphicData) return;
            if (bodyType != BodyTypeDefOf.Child) return;

            if (facing == Rot4.East)
                __result += new Vector2(0.10f, 0f);
            else if (facing == Rot4.West)
                __result += new Vector2(-0.10f, 0f);
        }
    }
}
