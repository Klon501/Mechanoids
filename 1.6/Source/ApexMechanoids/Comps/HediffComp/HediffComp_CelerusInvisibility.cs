using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class HediffCompProperties_CelerusInvisibility : HediffCompProperties_Invisibility
    {
        public HediffCompProperties_CelerusInvisibility()
        {
            compClass = typeof(HediffComp_CelerusInvisibility);
            affectedByDisruptor = false;
        }
    }

    public class HediffComp_CelerusInvisibility : HediffComp_Invisibility
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            BecomeInvisible(instant: true);
            UpdatePawnVisibilityState();
        }

        public override void CompPostPostRemoved()
        {
            UpdatePawnVisibilityState();
        }

        private void UpdatePawnVisibilityState()
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                return;
            }

            if (pawn.Spawned && pawn.Map != null)
            {
                pawn.Map.attackTargetsCache.UpdateTarget(pawn);
            }

            if (pawn.RaceProps.Humanlike)
            {
                PortraitsCache.SetDirty(pawn);
                GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            }

            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }
}
