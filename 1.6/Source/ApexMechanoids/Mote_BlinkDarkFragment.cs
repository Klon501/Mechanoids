using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class Mote_BlinkDarkFragment : MoteThrown
    {
        private const float StartScale = 1f;
        private const float EndScale = 1.2f;
        private const float ExpandDurationSeconds = 0.12f;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            Scale = StartScale;
        }

        public override void Tick()
        {
            base.Tick();

            float progress = Mathf.Clamp01(AgeSecs / ExpandDurationSeconds);
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            Scale = Mathf.Lerp(StartScale, EndScale, easedProgress);
        }
    }
}
