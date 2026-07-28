using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNodeProperties_SatelliteHover : PawnRenderNodeProperties
    {
        public float hoverAmplitude = 0.055f;
        public int hoverPeriodTicks = 120;
        public float hoverPhaseOffset;
        public bool seedPhaseByPawn = true;

        public PawnRenderNodeProperties_SatelliteHover()
        {
            nodeClass = typeof(PawnRenderNode_AnimalPart_Body);
            workerClass = typeof(PawnRenderNodeWorker_SatelliteHover);
        }
    }

    public class PawnRenderNodeProperties_SatelliteCarriedHover : PawnRenderNodeProperties_Carried
    {
        public float hoverAmplitude = 0.055f;
        public int hoverPeriodTicks = 120;
        public float hoverPhaseOffset;
        public bool seedPhaseByPawn = true;

        public PawnRenderNodeProperties_SatelliteCarriedHover()
        {
            workerClass = typeof(PawnRenderNodeWorker_SatelliteCarriedHover);
        }
    }

    internal static class SatelliteHoverUtility
    {
        public static bool CanHover(PawnDrawParms parms)
        {
            return !parms.Portrait && !parms.dead && parms.pawn != null && !parms.pawn.Dead;
        }

        public static float HoverOffset(Pawn pawn, float amplitude, int periodTicks, float phaseOffset, bool seedPhaseByPawn)
        {
            int period = Mathf.Max(periodTicks, 1);
            int seedOffset = seedPhaseByPawn && pawn != null ? pawn.thingIDNumber % period : 0;
            float phase = ((Find.TickManager.TicksGame + seedOffset) % period) / (float)period * Mathf.PI * 2f + phaseOffset;
            return Mathf.Sin(phase) * amplitude;
        }
    }

    public class PawnRenderNodeWorker_SatelliteHover : PawnRenderNodeWorker_AnimalBody
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNodeProperties_SatelliteHover props = node.Props as PawnRenderNodeProperties_SatelliteHover;
            if (!SatelliteHoverUtility.CanHover(parms) || props == null)
            {
                return offset;
            }

            offset.z += SatelliteHoverUtility.HoverOffset(parms.pawn, props.hoverAmplitude, props.hoverPeriodTicks, props.hoverPhaseOffset, props.seedPhaseByPawn);
            return offset;
        }
    }

    public class PawnRenderNodeWorker_SatelliteCarriedHover : PawnRenderNodeWorker_Carried
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNodeProperties_SatelliteCarriedHover props = node.Props as PawnRenderNodeProperties_SatelliteCarriedHover;
            if (parms.Portrait || props == null)
            {
                return offset;
            }

            offset.z += SatelliteHoverUtility.HoverOffset(parms.pawn, props.hoverAmplitude, props.hoverPeriodTicks, props.hoverPhaseOffset, props.seedPhaseByPawn);
            return offset;
        }
    }
}
