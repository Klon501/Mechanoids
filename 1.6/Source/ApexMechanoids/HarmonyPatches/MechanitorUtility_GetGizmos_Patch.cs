using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;


namespace ApexMechanoids
{
    [HarmonyPatch(typeof(MechanitorUtility))]
    [HarmonyPatch("GetMechGizmos")]
    public static class OverwriteSelectOverseer
    {
        private static CachedTexture SelectOverseerIcon = new CachedTexture("UI/Icons/SelectOverseer");

        private static List<Map> tmpAllMaps = new List<Map>();

        private static List<Pawn> tmpMechanitorsInCaskets = new List<Pawn>();

        [HarmonyPostfix]
        public static void GetGizmos(ref IEnumerable<Gizmo> __result, Pawn mech)
        {
            #region Get old gizmos and change availability of select overseer

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
            else
            {
                foreach (Gizmo gizmo in templist)
                {
                    list.Add(gizmo);
                }
            }

            #endregion



            #region Add Reconnect uncontrolled mech to mechanitor in command casket 

            if (Find.Selector.SingleSelectedThing == mech && mech.IsColonyMech)
            {
                if (mech.IsColonyMechRequiringMechanitor())    //connected but uncontrolled
                {
                    tmpAllMaps.Clear();
                    tmpMechanitorsInCaskets.Clear();

                    tmpAllMaps.AddRange(Find.Maps);
                    for (int i = 0; i < tmpAllMaps.Count; i++)
                    {
                        foreach (Pawn p in tmpAllMaps[i].mapPawns.FreeColonists)
                        {
                            if (p != null && !tmpMechanitorsInCaskets.Contains(p) && Utils.IsUplinkActiveFor(p))
                            {
                                tmpMechanitorsInCaskets.Add(p);
                            }
                        }
                    }

                    if (!tmpMechanitorsInCaskets.NullOrEmpty())
                    {
                        Command_Action command_Action_Reconnect = new Command_Action();
                        command_Action_Reconnect.defaultLabel = "APM.CommandCasket.Mech.Gizmo.Reconnect.Label".Translate().CapitalizeFirst();
                        command_Action_Reconnect.defaultDesc = "APM.CommandCasket.Mech.Gizmo.Reconnect.Desc".Translate().CapitalizeFirst();
                        command_Action_Reconnect.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/APM_ReconnectOnOtherMap");
                        command_Action_Reconnect.action = delegate
                        {
                            List<FloatMenuOption> floatlist = new List<FloatMenuOption>();
                            foreach (Pawn mechanitor in tmpMechanitorsInCaskets)
                            {
                                string label = mechanitor.LabelShortCap;

                                if (mech.GetStatValue(StatDefOf.BandwidthCost) > mechanitor.mechanitor.TotalBandwidth - mechanitor.mechanitor.UsedBandwidth)
                                {
                                    label += "APM.CommandCasket.Mech.Gizmo.Reconnect.Floatmenu".Translate();
                                }

                                floatlist.Add(new FloatMenuOption(label, delegate
                                {
                                    mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                                    mech.SetFaction(Faction.OfPlayer);
                                    mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                                }));
                            }

                            if (floatlist.Any())
                            {
                                Find.WindowStack.Add(new FloatMenu(floatlist));
                            }
                        };
                        list.Add(command_Action_Reconnect);
                    }
                }
            }

            #endregion

            IEnumerable<Gizmo> enumerable = list;
            __result = enumerable;


        }
    }
}
