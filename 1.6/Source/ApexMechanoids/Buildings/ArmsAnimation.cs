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
                            randomAnimDuration[i] = armIntervals[i];
                            randomAnimTicks[i] = 0;
                            if (config.arms[i].randomReach.HasValue)
                            {
                                randomAnimReach[i] = Rand.Range(config.arms[i].randomReach.Value.min, config.arms[i].randomReach.Value.max);
                            }
                            else
                            {
                                randomAnimReach[i] = 0.2f;
                            }
                        }

                        randomAnimTicks[i]++;
                        if (randomAnimTicks[i] >= randomAnimDuration[i])
                        {
                            randomAnimDuration[i] = 0;
                        }
                    }
                    else
                    {
                        randomAnimDuration[i] = 0;
                        randomAnimTicks[i] = 0;
                    }
                }
            }
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
                    float animProgress = (float)randomAnimTicks[i] / randomAnimDuration[i];
                    float smoothProgress = Mathf.PingPong(animProgress * 2f, 1f);
                    Vector3 randomOffset = Vector3.Lerp(Vector3.zero, worldOffset * randomAnimReach[i], smoothProgress);
                    armPos += randomOffset;
                }

                Vector3 drawOffset = Vector3.zero;
                switch (rot.AsInt)
                {
                    case 0:
                        drawOffset = config.arms[i].drawOffsetNorth;
                        break;
                    case 1:
                        drawOffset = config.arms[i].drawOffsetEast;
                        break;
                    case 2:
                        drawOffset = config.arms[i].drawOffsetSouth;
                        break;
                    case 3:
                        drawOffset = config.arms[i].drawOffsetWest;
                        break;
                }
                armPos += drawOffset;

                if (graphic is Graphic_Multi multiGraphic)
                {
                    Material material = multiGraphic.MatAt(rot);
                    Mesh mesh = multiGraphic.MeshAt(rot);
                    Quaternion finalRotation = rot.AsQuat;
                    if (rot.IsHorizontal)
                    {
                        if (i == 0)
                        {
                            finalRotation *= Quaternion.AngleAxis(-90, Vector3.up);
                        }
                        else
                        {
                            finalRotation *= Quaternion.AngleAxis(90, Vector3.up);
                        }
                    }
                    Graphics.DrawMesh(mesh, Matrix4x4.TRS(armPos, finalRotation, Vector3.one), material, 0);
                }
                else
                {
                    graphic.Draw(armPos, rot, null);
                }
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
