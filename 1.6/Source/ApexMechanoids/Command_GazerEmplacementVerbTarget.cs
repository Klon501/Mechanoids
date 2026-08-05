using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class Command_GazerEmplacementVerbTarget : Command_VerbTarget
    {
        private static readonly Color ProgressBarBackground = new Color(0.14f, 0.14f, 0.14f, 0.95f);
        public Building_GazerEmplacement emplacement;

        public override string TopRightLabel
        {
            get { return null; }
        }

        public override void GizmoUpdateOnMouseover()
        {
            if (emplacement != null)
            {
                emplacement.DrawFiringArc();
            }
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            float width = GetWidth(maxWidth);
            Rect rect = new Rect(topLeft.x, topLeft.y, width, 75f);
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

            if (emplacement != null)
            {
                float fillPercent;
                Color fillColor;
                if (emplacement.TryGetCommandProgress(out fillPercent, out fillColor))
                {
                    Rect barRect = new Rect(rect.x + 6f, rect.yMax - 8f, rect.width - 12f, 4f);
                    Widgets.DrawBoxSolid(barRect, ProgressBarBackground);
                    if (fillPercent > 0f)
                    {
                        Rect filledRect = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(fillPercent), barRect.height);
                        Widgets.DrawBoxSolid(filledRect, fillColor);
                    }
                }
            }

            return result;
        }

        public override void ProcessInput(Event ev)
        {
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            if (emplacement == null || verb == null)
            {
                return;
            }

            Find.Targeter.BeginTargeting(
                verb.targetParams,
                delegate(LocalTargetInfo target)
                {
                    string failReason;
                    if (!emplacement.TryOrderShot(target, out failReason) && !failReason.NullOrEmpty())
                    {
                        Messages.Message(failReason, emplacement, MessageTypeDefOf.RejectInput, false);
                    }
                },
                delegate(LocalTargetInfo target)
                {
                    emplacement.DrawVerbTargetingPreview(target);
                },
                delegate(LocalTargetInfo target)
                {
                    string failReason;
                    return emplacement.CanAttackTargetForVerb(target, out failReason);
                },
                null,
                null,
                verb.UIIcon,
                true,
                delegate(LocalTargetInfo target)
                {
                    verb.OnGUI(target);
                },
                null);
        }
    }
}