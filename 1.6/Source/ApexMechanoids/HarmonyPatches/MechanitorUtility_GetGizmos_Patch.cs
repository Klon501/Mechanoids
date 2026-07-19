using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;


namespace ApexMechanoids
{
    [HarmonyPatch(typeof(MechanitorUtility))]
    [HarmonyPatch("GetMechGizmos")]
    public static class OverwriteSelectOverseer
    {
        private static CachedTexture SelectOverseerIcon = new CachedTexture("UI/Icons/SelectOverseer");

        [HarmonyPostfix]
        public static void GetGizmos(ref IEnumerable<Gizmo> __result, Pawn mech)
        {

            List<Gizmo> templist = __result.ToList();

            List<Gizmo> list = new List<Gizmo>();

            Pawn overseer = mech.GetOverseer();

            if (Utils.IsUplinkActiveFor(overseer))
            {
                foreach (Gizmo gizmo in templist)
                {
                    if (gizmo != null)
                    {
                        if (gizmo.disabled && gizmo.ToString() == "Command(label=Select overseer, defaultDesc=No overseer.)")
                        {
                            Command_Action command_Action = new Command_Action
                            {
                                defaultLabel = "CommandSelectOverseer".Translate(),
                                defaultDesc = "CommandSelectOverseerDesc".Translate(),
                                icon = SelectOverseerIcon.Texture,
                                action = delegate
                                {
                                    Find.Selector.ClearSelection();
                                    Find.Selector.Select(overseer);
                                },
                                onHover = delegate
                                {
                                    if (overseer != null)
                                    {
                                        GenDraw.DrawArrowPointingAt(overseer.TrueCenter());
                                    }
                                },
                            };
                            list.Add(command_Action);
                        }
                        else
                        {
                            list.Add(gizmo);
                        } 
                    }
                }
            }

            

            IEnumerable<Gizmo> enumerable = list;
            __result = enumerable;


        }
    }
}
