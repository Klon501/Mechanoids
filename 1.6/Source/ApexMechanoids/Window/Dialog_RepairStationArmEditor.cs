using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    [HotSwappable]
    public class Dialog_RepairStationArmEditor : Window
    {
        private readonly Building_RepairStation station;
        private int selectedArmIndex;
        private Rot4 selectedRotation;
        private float step = 0.01f;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(720f, 560f);

        public Dialog_RepairStationArmEditor(Building_RepairStation station)
        {
            this.station = station;
            selectedRotation = station.Rotation;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
            preventCameraMotion = false;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (station == null || station.Destroyed || station.Config?.ArmsAnimation == null || station.Config.ArmsAnimation.arms.Count == 0)
            {
                Widgets.Label(inRect, "APM_RepairStation_ArmEditor_NoStation".Translate());
                return;
            }

            selectedArmIndex = Mathf.Clamp(selectedArmIndex, 0, station.Config.ArmsAnimation.arms.Count - 1);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "APM_RepairStation_ArmEditor_Title".Translate());
            Text.Font = GameFont.Small;

            Rect controlsRect = new Rect(0f, 36f, inRect.width, 120f);
            DrawHeaderControls(controlsRect);

            Rect scrollRect = new Rect(0f, 166f, inRect.width, inRect.height - 216f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, 310f);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            DrawArmEditor(viewRect);
            Widgets.EndScrollView();

            DrawFooter(new Rect(0f, inRect.height - 40f, inRect.width, 36f));
        }

        private void DrawHeaderControls(Rect rect)
        {
            float y = rect.y;
            Widgets.Label(new Rect(rect.x, y, 280f, 24f), "APM_RepairStation_ArmEditor_CurrentRotation".Translate(station.Rotation.ToString()));
            Widgets.Label(new Rect(rect.x + 300f, y, 380f, 24f), PreviewStatusText());
            y += 30f;

            Widgets.Label(new Rect(rect.x, y, 80f, 28f), "APM_RepairStation_ArmEditor_Arm".Translate());
            float buttonX = rect.x + 90f;
            for (int i = 0; i < station.Config.ArmsAnimation.arms.Count; i++)
            {
                if (Widgets.ButtonText(new Rect(buttonX, y, 72f, 28f), "Arm " + i))
                {
                    selectedArmIndex = i;
                }
                if (i == selectedArmIndex)
                {
                    Widgets.DrawHighlightSelected(new Rect(buttonX, y, 72f, 28f));
                }
                buttonX += 78f;
            }

            y += 36f;
            Widgets.Label(new Rect(rect.x, y, 80f, 28f), "APM_RepairStation_ArmEditor_View".Translate());
            DrawRotationButton(new Rect(rect.x + 90f, y, 72f, 28f), Rot4.North);
            DrawRotationButton(new Rect(rect.x + 168f, y, 72f, 28f), Rot4.East);
            DrawRotationButton(new Rect(rect.x + 246f, y, 72f, 28f), Rot4.South);
            DrawRotationButton(new Rect(rect.x + 324f, y, 72f, 28f), Rot4.West);
            if (Widgets.ButtonText(new Rect(rect.x + 416f, y, 150f, 28f), "APM_RepairStation_ArmEditor_UseCurrent".Translate()))
            {
                selectedRotation = station.Rotation;
            }

            y += 36f;
            Widgets.Label(new Rect(rect.x, y, 80f, 28f), "APM_RepairStation_ArmEditor_Step".Translate());
            step = Widgets.HorizontalSlider(new Rect(rect.x + 90f, y, 260f, 28f), step, 0.001f, 0.1f, middleAlignment: false, label: step.ToString("F3"));
        }

        private string PreviewStatusText()
        {
            if (selectedRotation == station.Rotation)
            {
                return "APM_RepairStation_ArmEditor_LivePreviewOn".Translate();
            }

            return "APM_RepairStation_ArmEditor_LivePreviewOff".Translate(selectedRotation.ToString());
        }

        private void DrawRotationButton(Rect rect, Rot4 rot)
        {
            if (Widgets.ButtonText(rect, rot.ToString()))
            {
                selectedRotation = rot;
            }
            if (selectedRotation == rot)
            {
                Widgets.DrawHighlightSelected(rect);
            }
        }

        private void DrawArmEditor(Rect rect)
        {
            ArmConfig arm = station.Config.ArmsAnimation.arms[selectedArmIndex];
            Vector3 offset = GetOffset(arm, selectedRotation);

            float y = rect.y;
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "APM_RepairStation_ArmEditor_SelectedOffset".Translate(selectedRotation.ToString(), selectedArmIndex));
            y += 34f;

            offset.x = DrawAxis(rect, ref y, "X", offset.x, -2f, 2f);
            offset.y = DrawAxis(rect, ref y, "Y", offset.y, -0.5f, 0.5f);
            offset.z = DrawAxis(rect, ref y, "Z", offset.z, -2f, 2f);
            SetOffset(arm, selectedRotation, offset);

            y += 10f;
            if (Widgets.ButtonText(new Rect(rect.x, y, 160f, 30f), "APM_RepairStation_ArmEditor_ResetView".Translate()))
            {
                SetOffset(arm, selectedRotation, Vector3.zero);
            }
            if (Widgets.ButtonText(new Rect(rect.x + 170f, y, 180f, 30f), "APM_RepairStation_ArmEditor_CopyArmXml".Translate()))
            {
                CopyToClipboard(ArmToXml(arm, selectedArmIndex));
            }
            if (Widgets.ButtonText(new Rect(rect.x + 360f, y, 180f, 30f), "APM_RepairStation_ArmEditor_CopyAllXml".Translate()))
            {
                CopyToClipboard(AllArmsXml());
            }

            MarkDirty();
        }

        private float DrawAxis(Rect rect, ref float y, string label, float value, float min, float max)
        {
            Widgets.Label(new Rect(rect.x, y, 24f, 28f), label);
            if (Widgets.ButtonText(new Rect(rect.x + 30f, y, 32f, 28f), "-"))
            {
                value -= step;
            }
            value = Widgets.HorizontalSlider(new Rect(rect.x + 70f, y, rect.width - 220f, 28f), value, min, max, middleAlignment: false, label: value.ToString("F3"));
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 140f, y, 32f, 28f), "+"))
            {
                value += step;
            }
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 100f, y, 48f, 28f), "0"))
            {
                value = 0f;
            }
            Widgets.Label(new Rect(rect.x + rect.width - 48f, y, 48f, 28f), value.ToString("F3"));
            y += 36f;
            return Mathf.Clamp(value, min, max);
        }

        private void DrawFooter(Rect rect)
        {
            Widgets.Label(rect, "APM_RepairStation_ArmEditor_Footer".Translate());
        }

        private Vector3 GetOffset(ArmConfig arm, Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0:
                    return arm.drawOffsetNorth;
                case 1:
                    return arm.drawOffsetEast;
                case 2:
                    return arm.drawOffsetSouth;
                case 3:
                    return arm.drawOffsetWest;
                default:
                    return Vector3.zero;
            }
        }

        private void SetOffset(ArmConfig arm, Rot4 rot, Vector3 offset)
        {
            switch (rot.AsInt)
            {
                case 0:
                    arm.drawOffsetNorth = offset;
                    break;
                case 1:
                    arm.drawOffsetEast = offset;
                    break;
                case 2:
                    arm.drawOffsetSouth = offset;
                    break;
                case 3:
                    arm.drawOffsetWest = offset;
                    break;
            }
        }

        private void MarkDirty()
        {
            if (station.Spawned)
            {
                station.DirtyMapMesh(station.Map);
            }
        }

        private void CopyToClipboard(string xml)
        {
            GUIUtility.systemCopyBuffer = xml;
            Messages.Message("APM_RepairStation_ArmEditor_Copied".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        private string AllArmsXml()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<arms>");
            for (int i = 0; i < station.Config.ArmsAnimation.arms.Count; i++)
            {
                sb.Append(ArmToXml(station.Config.ArmsAnimation.arms[i], i));
            }
            sb.AppendLine("</arms>");
            return sb.ToString();
        }

        private string ArmToXml(ArmConfig arm, int armIndex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\t<li>");
            sb.AppendLine("\t\t<graphicData>");
            sb.AppendLine("\t\t\t<texPath>" + arm.graphicData.texPath + "</texPath>");
            sb.AppendLine("\t\t\t<graphicClass>" + arm.graphicData.graphicClass + "</graphicClass>");
            sb.AppendLine("\t\t\t<drawSize>(" + arm.graphicData.drawSize.x.ToString("F3") + "," + arm.graphicData.drawSize.y.ToString("F3") + ")</drawSize>");
            sb.AppendLine("\t\t</graphicData>");
            sb.AppendLine("\t\t<maxReach>" + arm.maxReach.ToString("F3") + "</maxReach>");
            sb.AppendLine("\t\t<drawOffsetNorth>" + VectorToXml(arm.drawOffsetNorth) + "</drawOffsetNorth>");
            sb.AppendLine("\t\t<drawOffsetEast>" + VectorToXml(arm.drawOffsetEast) + "</drawOffsetEast>");
            sb.AppendLine("\t\t<drawOffsetSouth>" + VectorToXml(arm.drawOffsetSouth) + "</drawOffsetSouth>");
            sb.AppendLine("\t\t<drawOffsetWest>" + VectorToXml(arm.drawOffsetWest) + "</drawOffsetWest>");
            if (arm.randomInterval.HasValue)
            {
                sb.AppendLine("\t\t<randomInterval>" + arm.randomInterval.Value.min + "~" + arm.randomInterval.Value.max + "</randomInterval>");
            }
            if (arm.randomReach.HasValue)
            {
                sb.AppendLine("\t\t<randomReach>" + RangeToXml(arm.randomReach.Value) + "</randomReach>");
            }
            if (arm.randomVerticalReach.HasValue)
            {
                sb.AppendLine("\t\t<randomVerticalReach>" + RangeToXml(arm.randomVerticalReach.Value) + "</randomVerticalReach>");
            }
            sb.AppendLine("\t</li>");
            return sb.ToString();
        }

        private string VectorToXml(Vector3 vector)
        {
            return "(" + vector.x.ToString("F3") + "," + vector.y.ToString("F3") + "," + vector.z.ToString("F3") + ")";
        }

        private string RangeToXml(FloatRange range)
        {
            return range.min.ToString("F3") + "~" + range.max.ToString("F3");
        }
    }
}
