using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("GetGizmos")]
    public static class AddRemoteMechCasketAbilities
    {
        [HarmonyPostfix]
        public static void GetGizmos(ref IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if(Utils.IsUplinkActiveFor(__instance, out Building_MechCommandCasket casket))
            {
                if(casket == null)
                {
                    return;
                }

                List<Gizmo> list = __result.ToList();

                CompRemoteMechCasketAbilities comp = casket.TryGetComp<CompRemoteMechCasketAbilities>();
                if (comp != null)
                {
                    foreach (Gizmo gizmo in comp.GetGizmos())
                    {
                        if (gizmo != null)
                        {
                            list.Add(gizmo);
                        }
                    }
                }

                IEnumerable<Gizmo> enumerable = list;
                __result = enumerable;
            }

        }
    }
}
