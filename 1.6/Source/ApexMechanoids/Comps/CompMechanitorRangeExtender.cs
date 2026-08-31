using System.Collections.Generic;
using RimWorld;
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

        public float currentRange;

        public float SquaredDistance => GetEffectiveSquaredDistance();

        private float GetEffectiveSquaredDistance()
        {
            float range = GetEffectiveRange();
            if (range <= 0f) return 0f;
            return range * range;
        }

        public float GetEffectiveRange()
        {
            Pawn pawn = Pawn;
            Pawn overseer = pawn?.GetOverseer();
            if (overseer == null)
            {
                currentRange = 0f;
                return 0f;
            }

            currentRange = overseer.MapHeld == pawn.MapHeld ? Props.maxRange : Props.minRange;
            return currentRange;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned) return;
            if (!AnySelectedDraftedMechOfSameOverseer(pawn)) return;
            float range = GetEffectiveRange();
            if (range > 0f)
            {
                GenDraw.DrawRadiusRing(parent.Position, range, Color.cyan);
            }
        }

        private static bool AnySelectedDraftedMechOfSameOverseer(Pawn pawn)
        {
            List<Pawn> selectedPawns = Find.Selector.SelectedPawns;
            if (selectedPawns.Count == 0) return false;
            Pawn overseer = pawn.GetOverseer();
            if (overseer == null) return false;
            for (int i = 0; i < selectedPawns.Count; i++)
            {
                Pawn selected = selectedPawns[i];
                if (selected.Drafted && selected.GetOverseer() == overseer)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
