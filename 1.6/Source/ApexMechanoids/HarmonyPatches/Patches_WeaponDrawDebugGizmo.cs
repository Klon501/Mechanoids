using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class AddWeaponDrawDebugGizmo
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if (!Prefs.DevMode) return;

            ThingWithComps weapon = __instance.equipment?.Primary;
            if (weapon?.def?.weaponTags == null) return;

            bool isAPM = false;
            foreach (string tag in weapon.def.weaponTags)
            {
                if (tag != null && tag.StartsWith("APM_"))
                {
                    isAPM = true;
                    break;
                }
            }
            if (!isAPM) return;

            List<Gizmo> list = __result.ToList();
            list.Add(new Gizmo_WeaponDrawDebug(__instance));
            __result = list;
        }
    }
}
