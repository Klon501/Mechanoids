using RimWorld;
using System.Drawing;
using UnityEngine;
using Verse;
using Verse.Sound;


namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    public class CommandCasketMechGizmo : Gizmo
    {
        private CompRemoteMechCasketAbilities abilityComp;

        private Thing thing;

        private Pawn mech;

        private float BaseWidth => 30f + (Spacing * 2);      

        private const float AbilityWidth = 65f; // same as mainRect.width

        private const float AbilityWidthSmall = (AbilityWidth - Spacing) / 2;

        private const float Spacing = 5f;

        private Building_MechCommandCasket thingAsCasket => (Building_MechCommandCasket)thing;

        public CommandCasketMechGizmo(Thing thing, CompRemoteMechCasketAbilities abilityComp, Pawn mech)
        {
            this.abilityComp = abilityComp;
            this.thing = thing;
            this.mech = mech;
        }

        public override float GetWidth(float maxWidth)  // total should be 75, or 75 + (80 * x) to fall into vanillas pattern
        {
            float width = BaseWidth;

            if(abilityComp.User == null)
            {
                return width;
            }

            if (abilityComp.HasImplantRepair())
            {
                width += AbilityWidthSmall + Spacing;
            }
            if (abilityComp.HasImplantShield())
            {
                width += AbilityWidthSmall + Spacing;
            }

            return width;
        }

        UnityEngine.Color BackgroundColor = new UnityEngine.Color(0.10f, 0.15f, 0.17f);

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect backgroundRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);

            Rect mainRect = backgroundRect.ContractedBy(5f);

            Widgets.DrawWindowBackground(backgroundRect);
            Widgets.DrawBoxSolid(backgroundRect.ContractedBy(1f), BackgroundColor);

            Text.Font = GameFont.Tiny;


            

            if (abilityComp.User != null)
            {
                Rect connectRect = new Rect(mainRect.x, mainRect.y, mainRect.height / 2 - Spacing / 2, 1f);
                connectRect.height = connectRect.width;

                DrawVanillalikeGizmoHighlight(connectRect);
                if (Mouse.IsOver(connectRect))
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
                    {
                        Find.Targeter.BeginTargeting(abilityComp.RemoteConnectTargetingParameters(), abilityComp.StartToConnect, abilityComp.Highlight, abilityComp.CanRemoteConnect);
                    }

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                    {
                        abilityComp.EndActionWithSound();
                    }
                }
                GUI.DrawTexture(connectRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Connect));
                TooltipHandler.TipRegion(connectRect, "APM.CommandCasket.Gizmo.Connect.Desc".Translate().CapitalizeFirst());
  


                Rect disconnectRect = new Rect(connectRect.x, connectRect.y + connectRect.height + Spacing, connectRect.width, connectRect.height);

                DrawVanillalikeGizmoHighlight(disconnectRect);
                if (Mouse.IsOver(disconnectRect))
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
                    {
                        Find.Targeter.BeginTargeting(abilityComp.RemoteDisconnectTargetingParameters(), abilityComp.StartToDisconnect, abilityComp.Highlight, abilityComp.CanRemoteDisconnect);
                    }

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                    {
                        abilityComp.EndActionWithSound();
                    }
                }
                GUI.DrawTexture(disconnectRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Disconnect));
                TooltipHandler.TipRegion(disconnectRect, "APM.CommandCasket.Gizmo.Disconnect.Desc".Translate().CapitalizeFirst());


                Rect abilityRect = new Rect(connectRect.x + connectRect.width + Spacing, connectRect.y, connectRect.height, connectRect.height);
                Rect abilitySelfRect = new Rect(abilityRect.x, abilityRect.y + abilityRect.height + Spacing, abilityRect.height, abilityRect.height);

                if (abilityComp.HasImplantRepair())
                {
                    DrawVanillalikeGizmoHighlight(abilityRect);
                    GUI.DrawTexture(abilityRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Repair));

                    if (Mouse.IsOver(abilityRect))
                    {
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
                        {
                            Find.Targeter.BeginTargeting(abilityComp.RemoteRepairTargetingParameters(), abilityComp.StartToRepair, abilityComp.Highlight, abilityComp.CanRemoteRepair);
                        }

                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                        {
                            abilityComp.EndActionWithSound();
                        }
                    }
                    TooltipHandler.TipRegion(abilityRect, "APM.CommandCasket.Gizmo.Repair.Desc".Translate().CapitalizeFirst());


                    DrawVanillalikeGizmoHighlight(abilitySelfRect);
                    GUI.DrawTexture(abilitySelfRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Repair));

                    if (Mouse.IsOver(abilitySelfRect))
                    {
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
                        {
                            abilityComp.ForceSetTarget(mech, out LocalTargetInfo target);
                            abilityComp.StartToRepair(target);
                        }

                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                        {
                            abilityComp.EndActionWithSound();
                        }
                    }

                    TooltipHandler.TipRegion(abilitySelfRect, "APM.CommandCasket.Gizmo.RepairSelf.Desc".Translate().CapitalizeFirst());
                    //DrawVanillalikeLabel(abilitySelfRect, "APM.CommandCasket.Gizmo.Repair.Label".Translate().CapitalizeFirst());

                    abilityRect.x += abilityRect.width + Spacing;
                    abilitySelfRect.x += abilitySelfRect.width + Spacing;
                }

                if (abilityComp.HasImplantShield())
                {
                    DrawVanillalikeGizmoHighlight(abilityRect);
                    GUI.DrawTexture(abilityRect, abilityComp.GetShieldTexture());

                    if (Mouse.IsOver(abilityRect))
                    {
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && abilityComp.TicksForShieldcooldown == 0) //left click
                        {
                            Find.Targeter.BeginTargeting(abilityComp.RemoteShieldTargetingParameters(), abilityComp.StartToShield, abilityComp.Highlight, abilityComp.CanRemoteShield);
                        }

                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                        {
                            abilityComp.EndActionWithSound();
                        }
                    }

                    if (abilityComp.TicksForShieldcooldown > 0)
                    {
                        string topRightLabel = (int)(abilityComp.TicksForShieldcooldown / 60) + "s";

                        Vector2 vector2 = Text.CalcSize(topRightLabel);
                        Rect position;
                        Rect cooldownRect = (position = new Rect(abilityRect.xMax - vector2.x - 2f, abilityRect.y + 3f, vector2.x, vector2.y));
                        position.x -= 2f;
                        position.width += 3f;
                        Text.Anchor = TextAnchor.UpperRight;
                        GUI.DrawTexture(position, TexUI.GrayTextBG);
                        Widgets.Label(cooldownRect, topRightLabel);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    //DrawVanillalikeLabel(abilityRect, abilityComp.GetShieldGizmoLabel());
                    TooltipHandler.TipRegion(abilityRect, "APM.CommandCasket.Gizmo.Shield.Desc".Translate().CapitalizeFirst());



                    DrawVanillalikeGizmoHighlight(abilitySelfRect);
                    GUI.DrawTexture(abilitySelfRect, abilityComp.GetShieldTexture());

                    if (Mouse.IsOver(abilitySelfRect))
                    {
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && abilityComp.TicksForShieldcooldown == 0) //left click
                        {
                            abilityComp.ForceSetTarget(mech, out LocalTargetInfo target);
                            abilityComp.StartToShield(target);
                        }

                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                        {
                            abilityComp.EndActionWithSound();
                        }
                    }

                    if (abilityComp.TicksForShieldcooldown > 0)
                    {
                        string topRightLabel = (int)(abilityComp.TicksForShieldcooldown / 60) + "s";

                        Vector2 vector2 = Text.CalcSize(topRightLabel);
                        Rect position;
                        Rect cooldownRect = (position = new Rect(abilitySelfRect.xMax - vector2.x - 2f, abilitySelfRect.y + 3f, vector2.x, vector2.y));
                        position.x -= 2f;
                        position.width += 3f;
                        Text.Anchor = TextAnchor.UpperRight;
                        GUI.DrawTexture(position, TexUI.GrayTextBG);
                        Widgets.Label(cooldownRect, topRightLabel);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    //DrawVanillalikeLabel(abilitySelfRect, abilityComp.GetShieldGizmoLabel());
                    TooltipHandler.TipRegion(abilitySelfRect, "APM.CommandCasket.Gizmo.ShieldSelf.Desc".Translate().CapitalizeFirst());

                }
            }

            Text.Font = GameFont.Medium;

            return new GizmoResult(GizmoState.Clear);
        }

        private void DrawVanillalikeGizmoHighlight(Rect rect)
        {
            UnityEngine.Color color = GUI.color;
            GUI.color = GenUI.MouseoverColor;
            Widgets.DrawHighlightIfMouseover(rect);
            GUI.color = color;
        }

        private void DrawVanillalikeLabel(Rect abilityRect, string label)
        {
            if (!label.NullOrEmpty())
            {
                abilityRect.y += Spacing; 
                float labelHeight = Text.CalcHeight(label, abilityRect.width + 0.1f);
                Rect labelRect = new Rect(abilityRect.x, abilityRect.yMax - labelHeight + 12f, abilityRect.width, labelHeight);
                GUI.DrawTexture(labelRect, TexUI.GrayTextBG);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

    }


}

