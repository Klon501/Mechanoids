using UnityEngine;
using Verse;
using System.Text;
using RimWorld;
using System.Collections.Generic;
using System;

namespace ApexMechanoids
{
    public class SectionData
    {
        public string label;
        public Func<Vector3> getNorth, getEast, getSouth, getWest;
        public Action<Vector3> setNorth, setEast, setSouth, setWest;
        public Func<Vector2> getVector2;
        public Action<Vector2> setVector2;
        public Func<IntRange?> getIntRange;
        public Action<IntRange?> setIntRange;
        public Func<FloatRange?> getFloatRange;
        public Action<FloatRange?> setFloatRange;
        
        public SectionType type;
        public float vector2MinX, vector2MaxX, vector2MinY, vector2MaxY;
        public IntRange intRangeDefault;
        public float intRangeMin, intRangeMax;
        public FloatRange floatRangeDefault;
        public float floatRangeMin, floatRangeMax;

        public SectionData(string label, Func<Vector3> gn, Action<Vector3> sn, Func<Vector3> ge, Action<Vector3> se, Func<Vector3> gs, Action<Vector3> ss, Func<Vector3> gw, Action<Vector3> sw)
        { this.type = SectionType.Vector3Offset; this.label = label; this.getNorth = gn; this.setNorth = sn; this.getEast = ge; this.setEast = se; this.getSouth = gs; this.setSouth = ss; this.getWest = gw; this.setWest = sw; }
        public SectionData(string label, Func<Vector2> g, Action<Vector2> s, float minX, float maxX, float minY, float maxY)
        { this.type = SectionType.Vector2; this.label = label; this.getVector2 = g; this.setVector2 = s; this.vector2MinX = minX; this.vector2MaxX = maxX; this.vector2MinY = minY; this.vector2MaxY = maxY; }
        public SectionData(string label, Func<IntRange?> g, Action<IntRange?> s, IntRange? def, float min, float max)
        { this.type = SectionType.IntRange; this.label = label; this.getIntRange = g; this.setIntRange = s; this.intRangeDefault = def ?? new IntRange(30, 90); this.intRangeMin = min; this.intRangeMax = max; }
        public SectionData(string label, Func<FloatRange?> g, Action<FloatRange?> s, FloatRange? def, float min, float max)
        { this.type = SectionType.FloatRange; this.label = label; this.getFloatRange = g; this.setFloatRange = s; this.floatRangeDefault = def ?? new FloatRange(0.1f, 0.5f); this.floatRangeMin = min; this.floatRangeMax = max; }
    }

    public enum SectionType { Vector3Offset, Vector2, IntRange, FloatRange }

    public enum EditorTab { Main, North, South, East, West }
    
    [HotSwappable]
    public class Dialog_OffsetEditor : Window
    {
        private List<SectionData> sections;
        private Func<string> exportXmlCallback;
        private Vector2 scrollPosition = Vector2.zero;
        private float scrollViewHeight = 0f;
        private EditorTab currentTab = EditorTab.Main;
        private List<TabRecord> tabs;
        private float SliderHeight => 34f;

        public Dialog_OffsetEditor(string title, List<SectionData> sections, Func<string> exportXmlCallback = null)
        {
            this.sections = sections;
            this.exportXmlCallback = exportXmlCallback;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
            preventCameraMotion = false;
            this.forcePause = false;
            tabs = new List<TabRecord>
            {
                new TabRecord("Main", () => currentTab = EditorTab.Main, () => currentTab == EditorTab.Main),
                new TabRecord("North", () => currentTab = EditorTab.North, () => currentTab == EditorTab.North),
                new TabRecord("South", () => currentTab = EditorTab.South, () => currentTab == EditorTab.South),
                new TabRecord("East", () => currentTab = EditorTab.East, () => currentTab == EditorTab.East),
                new TabRecord("West", () => currentTab = EditorTab.West, () => currentTab == EditorTab.West)
            };
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect tabRect = new Rect(0, 35f, inRect.width, TabDrawer.TabHeight);
            TabDrawer.DrawTabs(tabRect, tabs);

            Rect scrollViewRect = new Rect(0, 75f, inRect.width, inRect.height - 115f);
            Rect viewRect = new Rect(0, 0, scrollViewRect.width - 20f, scrollViewHeight);

            Widgets.BeginScrollView(scrollViewRect, ref scrollPosition, viewRect);

            float currentY = 0f;

            for (int i = 0; i < sections.Count; i++)
            {
                SectionData section = sections[i];
                switch (section.type)
                {
                    case SectionType.Vector3Offset:
                        if (currentTab != EditorTab.Main)
                        {
                            currentY = DrawVector3OffsetSectionForTab(viewRect, currentY, section.label, section.getNorth(), section.getEast(), section.getSouth(), section.getWest(), (rot, val) => SetVector3OffsetForRotation(i, rot, val), () => ResetVector3Offsets(i), currentTab);
                        }
                        break;
                    case SectionType.Vector2:
                        if (currentTab == EditorTab.Main)
                        {
                            currentY = DrawVector2Sliders(viewRect, currentY, section.label, i);
                        }
                        break;
                    case SectionType.IntRange:
                        if (currentTab == EditorTab.Main)
                        {
                            currentY = DrawIntRangeSliders(viewRect, currentY, i, section.label, section.getIntRange(), section.intRangeDefault, section.intRangeMin, section.intRangeMax);
                        }
                        break;
                    case SectionType.FloatRange:
                        if (currentTab == EditorTab.Main)
                        {
                            currentY = DrawFloatRangeSliders(viewRect, currentY, i, section.label, section.getFloatRange(), section.floatRangeDefault, section.floatRangeMin, section.floatRangeMax);
                        }
                        break;
                }
            }

            scrollViewHeight = currentY + 50f;

            Widgets.EndScrollView();

            if (exportXmlCallback != null)
            {
                if (Widgets.ButtonText(new Rect(inRect.width/2f - 75f, inRect.height - 40f, 150f, 30f), "Copy XML"))
                {
                    string xml = exportXmlCallback();
                    GUIUtility.systemCopyBuffer = xml;
                    Messages.Message("XML copied to clipboard", MessageTypeDefOf.SilentInput, false);
                }
            }
        }

        private float DrawVector3OffsetSectionForTab(Rect viewRect, float currentY, string sectionLabel, Vector3 north, Vector3 east, Vector3 south, Vector3 west, System.Action<Rot4, Vector3> setOffset, System.Action resetAction, EditorTab tab)
        {
            Widgets.Label(new Rect(0, currentY, viewRect.width, 25f), sectionLabel);
            currentY += 25f;
            switch (tab)
            {
                case EditorTab.North:
                    currentY = DrawVector3SlidersForRotation(viewRect, currentY, Rot4.North, north, setOffset);
                    break;
                case EditorTab.South:
                    currentY = DrawVector3SlidersForRotation(viewRect, currentY, Rot4.South, south, setOffset);
                    break;
                case EditorTab.East:
                    currentY = DrawVector3SlidersForRotation(viewRect, currentY, Rot4.East, east, setOffset);
                    break;
                case EditorTab.West:
                    currentY = DrawVector3SlidersForRotation(viewRect, currentY, Rot4.West, west, setOffset);
                    break;
            }
            Rect resetButtonRect = new Rect(viewRect.width - 210f, currentY, 100f, 30f);
            if (Widgets.ButtonText(resetButtonRect, "Reset"))
            {
                resetAction();
            }
            currentY += SliderHeight;
            return currentY;
        }

        private float DrawVector3SlidersForRotation(Rect viewRect, float currentY, Rot4 rotation, Vector3 offset, System.Action<Rot4, Vector3> setOffset)
        {
            currentY = DrawXYZSliders(viewRect, currentY, ref offset);
            setOffset(rotation, offset);
            return currentY;
        }

        private float DrawVector2Sliders(Rect viewRect, float currentY, string sectionLabel, int sectionIndex)
        {
            Widgets.Label(new Rect(0, currentY, viewRect.width, 25f), sectionLabel);
            currentY += 25f;

            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                SectionData section = sections[sectionIndex];
                Vector2 size = section.getVector2();
                float sliderWidth = (viewRect.width) / 2f - 10f;

                Rect xLabelRect = new Rect(0, currentY, 20, SliderHeight);
                Widgets.Label(xLabelRect, "X");
                Rect xSliderRect = new Rect(25, currentY, sliderWidth - 25, SliderHeight);
                size.x = Widgets.HorizontalSlider(xSliderRect, size.x, section.vector2MinX, section.vector2MaxX, middleAlignment: false, label: size.x.ToString("F2"));

                float yOffset = viewRect.width / 2f;
                Rect yLabelRect = new Rect(yOffset, currentY, 20, SliderHeight);
                Widgets.Label(yLabelRect, "Y");
                Rect ySliderRect = new Rect(yOffset + 25, currentY, sliderWidth - 25, SliderHeight);
                size.y = Widgets.HorizontalSlider(ySliderRect, size.y, section.vector2MinY, section.vector2MaxY, middleAlignment: false, label: size.y.ToString("F2"));

                section.setVector2(size);
                currentY += SliderHeight;
            }

            return currentY;
        }

        private float DrawIntRangeSliders(Rect viewRect, float currentY, int sectionIndex, string label, IntRange? value, IntRange defaultValue, float minVal, float maxVal)
        {
            return DrawRangeSliders(viewRect, currentY, sectionIndex, label, value, defaultValue, minVal, maxVal, (v, min, max) => new IntRange(Mathf.RoundToInt(min), Mathf.RoundToInt(max)));
        }

        private float DrawFloatRangeSliders(Rect viewRect, float currentY, int sectionIndex, string label, FloatRange? value, FloatRange defaultValue, float minVal, float maxVal)
        {
            return DrawRangeSliders(viewRect, currentY, sectionIndex, label, value, defaultValue, minVal, maxVal, (v, min, max) => new FloatRange(min, max));
        }

        private float DrawRangeSliders<T>(Rect viewRect, float currentY, int sectionIndex, string label, T? value, T defaultValue, float minVal, float maxVal, System.Func<T, float, float, T> createRange) where T : struct
        {
            Widgets.Label(new Rect(0, currentY, viewRect.width, 25f), label);
            currentY += 25f;
            
            bool hasValue = value.HasValue;
            Widgets.CheckboxLabeled(new Rect(0, currentY, 200f, 24f), "Use " + label, ref hasValue);
            currentY += 30f;

            if (hasValue)
            {
                float sliderWidth = (viewRect.width) / 2f - 10f;

                Rect minLabelRect = new Rect(0, currentY, 40, SliderHeight);
                Widgets.Label(minLabelRect, "Min");
                Rect minSliderRect = new Rect(45, currentY, sliderWidth - 45, SliderHeight);
                float minValFloat = GetRangeMin(value ?? defaultValue);
                minValFloat = Widgets.HorizontalSlider(minSliderRect, minValFloat, minVal, maxVal, middleAlignment: false, label: minValFloat.ToString("F2"));

                float yOffset = viewRect.width / 2f;
                Rect maxLabelRect = new Rect(yOffset, currentY, 40, SliderHeight);
                Widgets.Label(maxLabelRect, "Max");
                Rect maxSliderRect = new Rect(yOffset + 45, currentY, sliderWidth - 45, SliderHeight);
                float maxValFloat = GetRangeMax(value ?? defaultValue);
                maxValFloat = Widgets.HorizontalSlider(maxSliderRect, maxValFloat, minVal, maxVal, middleAlignment: false, label: maxValFloat.ToString("F2"));

                if (minValFloat > maxValFloat)
                {
                    maxValFloat = minValFloat;
                }

                T val = createRange(value ?? defaultValue, minValFloat, maxValFloat);
                SetRangeValue(sectionIndex, val);
                currentY += SliderHeight;
            }
            else
            {
                SetRangeValueNull<T>(sectionIndex);
            }

            return currentY;
        }

        private float GetRangeMin<T>(T range) where T : struct
        {
            if (range is IntRange intRange)
            {
                return intRange.min;
            }
            if (range is FloatRange floatRange)
            {
                return floatRange.min;
            }
            return 0f;
        }

        private float GetRangeMax<T>(T range) where T : struct
        {
            if (range is IntRange intRange)
            {
                return intRange.max;
            }
            if (range is FloatRange floatRange)
            {
                return floatRange.max;
            }
            return 0f;
        }

        private float DrawXYZSliders(Rect viewRect, float currentY, ref Vector3 offset)
        {
            int sliderCount = 3;
            float columnWidth = viewRect.width / sliderCount;
            float spacing = columnWidth / 10f;
            float labelWidth = columnWidth / 10f;
            float resetButtonWidth = columnWidth / 10f;
            float sliderWidth = columnWidth - labelWidth - resetButtonWidth - spacing * 2;

            string[] labels = new string[] { "X", "Y", "Z" };
            float[] values = new float[] { offset.x, offset.y, offset.z };

            for (int i = 0; i < sliderCount; i++)
            {
                float x = i * columnWidth;
                float labelX = x;
                float sliderX = x + labelWidth + spacing;
                float resetButtonX = x + labelWidth + spacing + sliderWidth + spacing;

                Widgets.Label(new Rect(labelX, currentY, labelWidth, SliderHeight), labels[i]);
                Rect sliderRect = new Rect(sliderX, currentY, sliderWidth, SliderHeight);
                Rect resetButtonRect = new Rect(resetButtonX, currentY, resetButtonWidth, SliderHeight);
                Widgets.HorizontalSlider(sliderRect, ref values[i], new FloatRange(-1f, 1.5f), values[i].ToString("F2"));
                if (Widgets.ButtonText(resetButtonRect, "R"))
                {
                    values[i] = 0f;
                }
            }

            offset.x = values[0];
            offset.y = values[1];
            offset.z = values[2];

            currentY += SliderHeight;
            return currentY;
        }

        private void SetVector3OffsetForRotation(int sectionIndex, Rot4 rotation, Vector3 offset)
        {
            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                SectionData section = sections[sectionIndex];
                switch (rotation.AsInt)
                {
                    case 0:
                        section.setNorth(offset);
                        break;
                    case 1:
                        section.setEast(offset);
                        break;
                    case 2:
                        section.setSouth(offset);
                        break;
                    case 3:
                        section.setWest(offset);
                        break;
                }
            }
        }

        private void ResetVector3Offsets(int sectionIndex)
        {
            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                SectionData section = sections[sectionIndex];
                section.setNorth(Vector3.zero);
                section.setEast(Vector3.zero);
                section.setSouth(Vector3.zero);
                section.setWest(Vector3.zero);
            }
        }

        private void SetRangeValue<T>(int sectionIndex, T value) where T : struct
        {
            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                SectionData section = sections[sectionIndex];
                if (value is IntRange intRange)
                {
                    section.setIntRange(intRange);
                }
                else if (value is FloatRange floatRange)
                {
                    section.setFloatRange(floatRange);
                }
            }
        }

        private void SetRangeValueNull<T>(int sectionIndex) where T : struct
        {
            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                SectionData section = sections[sectionIndex];
                if (typeof(T) == typeof(IntRange))
                {
                    section.setIntRange(null);
                }
                else if (typeof(T) == typeof(FloatRange))
                {
                    section.setFloatRange(null);
                }
            }
        }
    }
}
