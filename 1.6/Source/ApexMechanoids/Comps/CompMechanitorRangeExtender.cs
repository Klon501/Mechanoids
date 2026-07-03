using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_MechanitorRangeExtender : Verse.CompProperties
    {
        public float maxRange;
        public float minRange;

        public CompProperties_MechanitorRangeExtender() => compClass = typeof(CompMechanitorRangeExtender);
    }

    public class CompMechanitorRangeExtender : ThingComp
    {
        public CompProperties_MechanitorRangeExtender Props => (CompProperties_MechanitorRangeExtender)props;
        private Pawn Pawn => parent as Pawn;

        private float cachedDistance;

        public float currentRange;

        public float SquaredDistance => GetEffectiveSquaredDistance();

        private float GetEffectiveSquaredDistance()
        {
            float range = GetEffectiveRange();
            if (range <= 0f) return 0f;
            if (cachedDistance == 0f) cachedDistance = Mathf.Pow(range, 2f);
            return cachedDistance;
        }

        public float GetEffectiveRange()
        {
            Pawn overseer = Pawn.GetOverseer();
            if (overseer == null) return 0f;
            if (overseer.MapHeld == Pawn.MapHeld) return Props.maxRange;
            return Props.minRange;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Pawn.Drafted) return;
            float range = GetEffectiveRange();
            cachedDistance = 0f; // invalidate cache each draw tick
            currentRange = range;
            if (range > 0f)
            {
                GenDraw.DrawRadiusRing(parent.Position, range, Color.cyan);
            }
        }
    }
}
