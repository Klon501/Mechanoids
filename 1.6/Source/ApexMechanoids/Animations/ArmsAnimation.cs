using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace ApexMechanoids
{
    [HotSwappable]
    public class ArmsAnimation
    {
        private ArmsAnimationConfig config;
        private int ticks;
        private List<int> armTicks;
        private List<int> armIntervals;
        private List<Graphic> armGraphics;
        private List<int> randomAnimTicks;
        private List<int> randomAnimDuration;
        private List<float> randomAnimReach;
        private List<float> randomAnimStartVerticalOffset;
        private List<float> randomAnimVerticalReach;
        private List<bool> randomAnimExtending;
        private List<int> stopTicksRemaining; // Tracks how many ticks remaining for each arm to stay stopped
        private List<bool> isArmStopped; // Tracks whether each arm is currently stopped

        public ArmsAnimation(ArmsAnimationConfig cfg)
        {
            this.config = cfg;

            armGraphics = new List<Graphic>();
            armTicks = new List<int>();
            armIntervals = new List<int>();
            randomAnimTicks = new List<int>();
            randomAnimDuration = new List<int>();
            randomAnimReach = new List<float>();
            randomAnimStartVerticalOffset = new List<float>();
            randomAnimVerticalReach = new List<float>();
            randomAnimExtending = new List<bool>();
            stopTicksRemaining = new List<int>(); // Initialize the new list
            isArmStopped = new List<bool>(); // Initialize the new list
            
            foreach (var armCfg in cfg.arms)
            {
                if (armCfg.graphicData != null)
                {
                    Graphic graphic = armCfg.graphicData.Graphic;
                    armGraphics.Add(graphic);
                }
                armTicks.Add(0);
                int interval = armCfg.randomInterval.HasValue ? Rand.RangeInclusive(armCfg.randomInterval.Value.min, armCfg.randomInterval.Value.max) : 60;
                armIntervals.Add(interval);
                randomAnimTicks.Add(0);
                randomAnimDuration.Add(0);
                randomAnimReach.Add(0f);
                randomAnimStartVerticalOffset.Add(0f);
                randomAnimVerticalReach.Add(0f);
                randomAnimExtending.Add(false);
                stopTicksRemaining.Add(0);
                isArmStopped.Add(false);
            }
        }

        public void Update(bool repairing)
        {
            if (repairing)
            {
                if (ticks < config.extendTicks) ticks++;
            }
            else
            {
                // Faster retraction by using speed multiplier if defined
                int retractionSpeed = 1;
                if (config.arms.Count > 0 && config.arms[0].fastRetractionSpeed > 1)
                {
                    retractionSpeed = config.arms[0].fastRetractionSpeed; // Use first arm's setting as default
                }
                
                if (ticks > 0) 
                {
                    ticks = Mathf.Max(0, ticks - retractionSpeed);
                }
            }

            bool isRepairingAndExtended = ticks == config.extendTicks && repairing;

            for (int i = 0; i < armTicks.Count; i++)
            {
                // Handle random stops
                if (isRepairingAndExtended && !isArmStopped[i] && config.arms[i].randomStopChance > 0f && 
                    Rand.Chance(config.arms[i].randomStopChance))
                {
                    // Start a random stop
                    isArmStopped[i] = true;
                    stopTicksRemaining[i] = Rand.RangeInclusive(
                        config.arms[i].randomStopDurationMin,
                        config.arms[i].randomStopDurationMax
                    );
                }
                
                // Decrement stop timer if arm is stopped
                if (isArmStopped[i])
                {
                    stopTicksRemaining[i]--;
                    if (stopTicksRemaining[i] <= 0)
                    {
                        isArmStopped[i] = false;
                        stopTicksRemaining[i] = 0;
                    }
                }
                
                // Update arm movement only if not stopped
                if (!isArmStopped[i])
                {
                    if (config.arms[i].randomInterval.HasValue && isRepairingAndExtended)
                    {
                        armTicks[i]--;
                        if (armTicks[i] <= 0)
                        {
                            armTicks[i] = armIntervals[i];
                            armIntervals[i] = Rand.RangeInclusive(
                                config.arms[i].randomInterval.Value.min,
                                config.arms[i].randomInterval.Value.max
                            );
                        }
                    }

                    if (isRepairingAndExtended && config.arms[i].randomInterval.HasValue)
                    {
                        if (randomAnimDuration[i] == 0)
                        {
                            StartRandomAnimation(i);
                        }

                        randomAnimTicks[i]++;
                        if (randomAnimTicks[i] >= randomAnimDuration[i])
                        {
                            randomAnimStartVerticalOffset[i] = randomAnimVerticalReach[i];
                            StartRandomAnimation(i);
                        }
                    }
                    else
                    {
                        randomAnimDuration[i] = 0;
                        randomAnimTicks[i] = 0;
                        randomAnimReach[i] = 0f;
                        randomAnimStartVerticalOffset[i] = 0f;
                        randomAnimVerticalReach[i] = 0f;
                    }
                }
            }
        }

        private void StartRandomAnimation(int armIndex)
        {
            randomAnimDuration[armIndex] = Mathf.Max(30, armIntervals[armIndex]);
            randomAnimTicks[armIndex] = 0;

            if (config.arms[armIndex].randomReach.HasValue)
            {
                randomAnimReach[armIndex] = Rand.Range(config.arms[armIndex].randomReach.Value.min, config.arms[armIndex].randomReach.Value.max);
            }
            else
            {
                randomAnimReach[armIndex] = 0.2f;
            }

            randomAnimVerticalReach[armIndex] = RandomVerticalOffset(armIndex);
        }

        private float RandomVerticalOffset(int armIndex)
        {
            if (!config.arms[armIndex].randomVerticalReach.HasValue)
            {
                return 0f;
            }

            FloatRange range = config.arms[armIndex].randomVerticalReach.Value;
            float amount = Rand.Range(range.min, range.max);
            return Rand.Value < 0.5f ? -amount : amount;
        }

        private (Vector3 originOffset, Vector3 destinationOffset) GetArmOffsets(int armIndex)
        {
            float reach = config.arms[armIndex].maxReach;
            
            if (armIndex == 0)
            {
                return (new Vector3(-reach, 0f, 0f), new Vector3(-reach * 0.5f, 0f, 0f));
            }
            else
            {
                return (new Vector3(reach, 0f, 0f), new Vector3(reach * 0.5f, 0f, 0f));
            }
        }

        private Vector3 GetDrawOffset(int armIndex, Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0:
                    return config.arms[armIndex].drawOffsetNorth;
                case 1:
                    return config.arms[armIndex].drawOffsetEast;
                case 2:
                    return config.arms[armIndex].drawOffsetSouth;
                case 3:
                    return config.arms[armIndex].drawOffsetWest;
                default:
                    return Vector3.zero;
            }
        }

        private Vector3 GetSurfaceMotionAxis(int armIndex, Rot4 rot)
        {
            if (rot.IsHorizontal)
            {
                return Vector3.zero;
            }

            float x = GetDrawOffset(armIndex, rot).x;
            if (Mathf.Abs(x) > 0.001f)
            {
                return x > 0f ? Vector3.left : Vector3.right;
            }

            return armIndex == 0 ? Vector3.right : Vector3.left;
        }

        private Vector3 GetRandomWorkOffset(int armIndex, Rot4 rot)
        {
            if (randomAnimDuration[armIndex] <= 0)
            {
                return Vector3.zero;
            }

            float cycleProgress = Mathf.Clamp01((float)randomAnimTicks[armIndex] / randomAnimDuration[armIndex]);
            const float extendEnd = 0.25f;
            const float workEnd = 0.50f;
            const float retractEnd = 0.75f;

            float reachProgress = 0f;
            float verticalOffset = randomAnimStartVerticalOffset[armIndex];

            if (cycleProgress < extendEnd)
            {
                reachProgress = Smooth01(cycleProgress / extendEnd);
            }
            else if (cycleProgress < workEnd)
            {
                float workProgress = (cycleProgress - extendEnd) / (workEnd - extendEnd);
                reachProgress = 1f + Mathf.Sin(workProgress * Mathf.PI * 4f) * 0.08f;
            }
            else if (cycleProgress < retractEnd)
            {
                reachProgress = Smooth01(1f - (cycleProgress - workEnd) / (retractEnd - workEnd));
            }
            else
            {
                float moveProgress = Smooth01((cycleProgress - retractEnd) / (1f - retractEnd));
                verticalOffset = Mathf.Lerp(randomAnimStartVerticalOffset[armIndex], randomAnimVerticalReach[armIndex], moveProgress);
            }

            Vector3 surfaceOffset = GetSurfaceMotionAxis(armIndex, rot) * randomAnimReach[armIndex] * reachProgress;
            Vector3 vertical = new Vector3(0f, 0f, verticalOffset);
            return surfaceOffset + vertical;
        }

        private float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        public void Draw(Vector3 drawLoc, Rot4 rot)
        {
            float progress = (float)ticks / config.extendTicks;

            for (int i = 0; i < config.arms.Count && i < armGraphics.Count; i++)
            {
                var graphic = armGraphics[i];

                var (originOffset, destOffset) = GetArmOffsets(i);
                Vector3 interpolatedOffset = Vector3.Lerp(originOffset, destOffset, progress);
                Vector3 worldOffset = interpolatedOffset.RotatedBy(rot);
                Vector3 armPos = drawLoc + worldOffset;
                armPos.y = AltitudeLayer.BuildingBelowTop.AltitudeFor();

                if (randomAnimDuration[i] > 0)
                {
                    armPos += GetRandomWorkOffset(i, rot);
                }

                armPos += GetDrawOffset(i, rot);

                Material material = graphic.MatAt(rot);
                Mesh mesh = graphic.MeshAt(rot);
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(armPos, graphic.QuatFromRot(rot), Vector3.one), material, 0);
            }
        }

        public void RegenerateArmGraphic(int armIndex)
        {
            if (armIndex >= 0 && armIndex < config.arms.Count && armIndex < armGraphics.Count)
            {
                var armCfg = config.arms[armIndex];
                if (armCfg.graphicData != null)
                {
                    Graphic graphic = armCfg.graphicData.Graphic;
                    armGraphics[armIndex] = graphic;
                }
                
                // Ensure lists have entries for this arm index
                while (armIndex >= stopTicksRemaining.Count)
                {
                    stopTicksRemaining.Add(0);
                    isArmStopped.Add(false);
                }
            }
        }
    }
}
