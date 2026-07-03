using System;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
    [HotSwappable]
    public class PlatformAnimation
    {
        private PlatformAnimationConfig config;
        private int ticks;
        private Graphic graphic;
        
        // Variables for random slowdowns
        private int slowdownTicksRemaining = 0;
        private bool isSlowedDown = false;
        
        public PlatformAnimation(PlatformAnimationConfig cfg)
        {
            this.config = cfg;
            if (cfg.graphicData != null)
            {
                graphic = cfg.graphicData.Graphic;
            }
        }

        public void Update(bool repairing)
        {
            if (repairing)
            {
                // Handle random slowdowns
                if (isSlowedDown && slowdownTicksRemaining > 0)
                {
                    slowdownTicksRemaining--;
                }
                else if (slowdownTicksRemaining <= 0)
                {
                    // End slowdown period
                    isSlowedDown = false;
                    
                    // Check if we should start a new slowdown period
                    if (config.randomSlowdownFrequency > 0 && Rand.Chance(1f / config.randomSlowdownFrequency))
                    {
                        isSlowedDown = true;
                        // Ensure minTicks doesn't exceed maxTicks to avoid RangeInclusive errors
                        int minTicks = Mathf.Min(config.randomSlowdownMinTicks, config.randomSlowdownMaxTicks);
                        int maxTicks = Mathf.Max(config.randomSlowdownMinTicks, config.randomSlowdownMaxTicks);
                        int selectedTicks = Rand.RangeInclusive(minTicks, maxTicks);
                        // Adjust by 1 to make actual slowdown duration equal to selected value (N ticks instead of N+1)
                        slowdownTicksRemaining = selectedTicks > 0 ? selectedTicks - 1 : 0;
                    }
                }
                
                // Increment ticks, but slower during slowdown periods
                if (!isSlowedDown)
                {
                    ticks++;
                }
            }
            else
            {
                // Reset slowdown when not repairing
                isSlowedDown = false;
                slowdownTicksRemaining = 0;
                ticks = 0; // Also reset ticks when not repairing
            }
        }

        public void Draw(Vector3 drawLoc, Rot4 rot)
        {
            if (graphic == null) return;
            
            float angle = (float)ticks / config.ticksToMove * 2f * (float)Math.PI;
            float offsetAmount = Mathf.Sin(angle) * (config.animationEnd - config.animationStart);
            Vector3 offset = new Vector3(0, AltitudeLayer.BuildingOnTop.AltitudeFor(), offsetAmount);
            Vector3 worldOffset = offset.RotatedBy(rot);
            Material material = graphic.MatAt(rot);
            Mesh mesh = graphic.MeshAt(rot);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(drawLoc + worldOffset, rot.AsQuat, Vector3.one), material, 0);
        }
    }
}
