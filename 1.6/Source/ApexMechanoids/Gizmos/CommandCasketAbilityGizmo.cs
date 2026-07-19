using RimWorld;
using System.Drawing;
using UnityEngine;
using Verse;
using Verse.Sound;


namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    public class CommandCasketAbilityGizmo : Gizmo
    {
        private CompRemoteMechCasketAbilities abilityComp;

        private Thing thing;

        private const float BaseWidth = 75f;

        private const float AbilityWidth = 65f; // same as mainRect.width

        private const float AbilityWidthSmall = (AbilityWidth - Spacing) / 2;

        private const float Spacing = 5f;



        private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new UnityEngine.Color(1f, 0.20f, 0.19f));

        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new UnityEngine.Color(0.03f, 0.035f, 0.05f));

        private static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        private Building_MechCommandCasket thingAsCasket => (Building_MechCommandCasket)thing;

        public CommandCasketAbilityGizmo(Thing thing, CompRemoteMechCasketAbilities abilityComp)
        {
            this.abilityComp = abilityComp;
            this.thing = thing;
        }

        public override float GetWidth(float maxWidth)  // total should be 75, or 75 + (80 * x) to fall into vanillas pattern
        {
            float width = BaseWidth;

            if(abilityComp.User == null)
            {
                return width;
            }
            width += AbilityWidthSmall + Spacing; // connect /disconnect
            width += AbilityWidth + Spacing; //cancel

            if (abilityComp.HasImplantRepair())
            {
                width += AbilityWidth + Spacing;
            }
            if (abilityComp.HasImplantShield())
            {
                width += AbilityWidth + Spacing;
            }
            width -= Spacing;  

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

            Rect mainLabelRect = new Rect(mainRect.x, mainRect.y, mainRect.height, mainRect.height);
            Rect thingRect = new Rect(mainRect.x + 2.5f, mainRect.y + 2.5f, 60f, 40f);
            GUI.DrawTexture(thingRect, thing.def.uiIcon);
            if(thing is Building_MechCommandCasket casket)
            {
                Rect barRect = new Rect(thingRect.x, thingRect.y + thingRect.height + 7.5f, thingRect.width - 1f, 12f);
                barRect.y -= 4f; // offset to not conflict with the label

                Widgets.FillableBar(barRect, casket.NutritionPercent, BarTex, EmptyBarTex, true);
            }
            
            DrawVanillalikeLabel(mainLabelRect, abilityComp.Props.labelShort.CapitalizeFirst());
            //DrawVanillalikeLabel(mainLabelRect, thing.def.label.CapitalizeFirst());


            if (abilityComp.User != null)
            {

                Rect connectRect = new Rect(mainRect.x + thingRect.width + Spacing, mainRect.y, mainRect.height / 2 - Spacing / 2, mainRect.height / 2 - 1f);

                DrawVanillalikeGizmoHighlight(connectRect);
                if (Widgets.ButtonInvisible(connectRect) && Event.current.button == 0)
                {
                    Find.Targeter.BeginTargeting(abilityComp.RemoteConnectTargetingParameters(), abilityComp.StartToConnect, abilityComp.Highlight, abilityComp.CanRemoteConnect);
                }
                GUI.DrawTexture(connectRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Connect));
                TooltipHandler.TipRegion(connectRect, "APM.CommandCasket.Gizmo.Connect.Desc".Translate().CapitalizeFirst());
                CancelAction(connectRect);

                Rect disconnectRect = new Rect(connectRect.x, connectRect.y + connectRect.height + 2f, connectRect.width, connectRect.height);

                DrawVanillalikeGizmoHighlight(disconnectRect);
                if (Widgets.ButtonInvisible(disconnectRect) && Event.current.button == 0)
                {
                    Find.Targeter.BeginTargeting(abilityComp.RemoteDisconnectTargetingParameters(), abilityComp.StartToDisconnect, abilityComp.Highlight, abilityComp.CanRemoteDisconnect);
                }
                GUI.DrawTexture(disconnectRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Disconnect));
                TooltipHandler.TipRegion(disconnectRect, "APM.CommandCasket.Gizmo.Disconnect.Desc".Translate().CapitalizeFirst());
                CancelAction(disconnectRect);

                Rect abilityRect = new Rect(connectRect.x + connectRect.width + Spacing, mainRect.y, mainRect.height, mainRect.height);
                if (abilityComp.HasImplantRepair())
                {
                    DrawVanillalikeGizmoHighlight(abilityRect);
                    GUI.DrawTexture(abilityRect, ContentFinder<Texture2D>.Get(abilityComp.Props.textpath_Repair));

                    if (Widgets.ButtonInvisible(abilityRect) && Event.current.button == 0)
                    {
                        Find.Targeter.BeginTargeting(abilityComp.RemoteRepairTargetingParameters(), abilityComp.StartToRepair, abilityComp.Highlight, abilityComp.CanRemoteRepair);
                    }
                    TooltipHandler.TipRegion(abilityRect, "APM.CommandCasket.Gizmo.Repair.Desc".Translate().CapitalizeFirst());
                    DrawVanillalikeLabel(abilityRect, "APM.CommandCasket.Gizmo.Repair.Label".Translate().CapitalizeFirst());

                    abilityRect.x += abilityRect.width + Spacing;

                    CancelAction(abilityRect);
                }

                if (abilityComp.HasImplantShield())
                {
                    DrawVanillalikeGizmoHighlight(abilityRect);
                    GUI.DrawTexture(abilityRect, abilityComp.GetShieldTexture());

                    if (Widgets.ButtonInvisible(abilityRect) && Event.current.button == 0) 
                    {
                        if (abilityComp.TicksForShieldcooldown == 0)
                        {
                            Find.Targeter.BeginTargeting(abilityComp.RemoteShieldTargetingParameters(), abilityComp.StartToShield, abilityComp.Highlight, abilityComp.CanRemoteShield);
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
                    DrawVanillalikeLabel(abilityRect, abilityComp.GetShieldGizmoLabel());
                    TooltipHandler.TipRegion(abilityRect, "APM.CommandCasket.Gizmo.Shield.Desc".Translate().CapitalizeFirst());
                    abilityRect.x += abilityRect.width + Spacing;
                    CancelAction(abilityRect);
                }

                Rect cancelRect = new Rect(abilityRect.x, mainRect.y, mainRect.height, mainRect.height);
                DrawVanillalikeGizmoHighlight(cancelRect);
                GUI.DrawTexture(cancelRect, CancelLoadingIcon);

                if (Widgets.ButtonInvisible(cancelRect) && Event.current.button == 0)
                {
                    if (thingAsCasket != null)
                    {
                        thingAsCasket.Finish();
                        thingAsCasket.innerContainer.TryDropAll(thingAsCasket.InteractionCell, thingAsCasket.Map, ThingPlaceMode.Near);
                        abilityComp.EndAction();
                    }
                }
                CancelAction(abilityRect);
                DrawVanillalikeLabel(abilityRect, "APM.CommandCasket.Gizmo.CancelLink.Label".Translate().CapitalizeFirst());
                TooltipHandler.TipRegion(cancelRect, "APM.CommandCasket.Gizmo.CancelLink.Desc".Translate().CapitalizeFirst());
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

        private void CancelAction(Rect rect)
        {
            if (Mouse.IsOver(rect))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1)  //right click
                {
                    abilityComp.EndAction();

                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }

            }
        }
    }


}

