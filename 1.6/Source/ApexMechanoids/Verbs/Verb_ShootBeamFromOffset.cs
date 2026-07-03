using RimWorld;
using System.Globalization;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class VerbProperties_ShootBeamFromOffset : VerbProperties
    {
        public string beamOriginOffset;
        public string beamOriginFrontOffset;
        public string beamOriginBackOffset;

        public Vector2 ParsedBeamOriginOffsetFor(Rot4 casterRotation)
        {
            string value = OffsetStringFor(casterRotation);
            Vector2 offset = ParseOffset(value, casterRotation.GetHashCode());
            return casterRotation == Rot4.West ? new Vector2(offset.x, -offset.y) : offset;
        }

        private string OffsetStringFor(Rot4 casterRotation)
        {
            if (casterRotation == Rot4.South && !beamOriginFrontOffset.NullOrEmpty())
            {
                return beamOriginFrontOffset;
            }
            if (casterRotation == Rot4.North && !beamOriginBackOffset.NullOrEmpty())
            {
                return beamOriginBackOffset;
            }
            return beamOriginOffset;
        }

        private Vector2 ParseOffset(string value, int logSeed)
        {
            if (value.NullOrEmpty())
            {
                return Vector2.zero;
            }

            string[] parts = value.Trim(' ', '(', ')').Split(',');
            if (parts.Length != 2)
            {
                Log.ErrorOnce($"Invalid beam origin offset '{value}'. Expected format: forward,right", GetHashCode() ^ logSeed);
                return Vector2.zero;
            }

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float forward)
                || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float right))
            {
                Log.ErrorOnce($"Invalid beam origin offset '{value}'. Expected numeric values: forward,right", GetHashCode() ^ logSeed ^ 7919);
                return Vector2.zero;
            }

            return new Vector2(forward, right);
        }
    }

    public class Verb_ShootBeamFromOffset : Verb_ShootBeam
    {
        private VerbProperties_ShootBeamFromOffset OffsetProps => verbProps as VerbProperties_ShootBeamFromOffset;

        public override float? AimAngleOverride
        {
            get
            {
                if (state != VerbState.Bursting)
                {
                    return null;
                }

                Vector3 beamDirection = (InterpolatedPosition - caster.Position.ToVector3Shifted()).Yto0().normalized;
                return (InterpolatedPosition - BeamOrigin(beamDirection)).AngleFlat();
            }
        }

        public override void BurstingTick()
        {
            ticksToNextPathStep--;
            Vector3 targetPosition = InterpolatedPosition;
            IntVec3 targetCell = targetPosition.ToIntVec3();
            Vector3 casterCenter = caster.Position.ToVector3Shifted();
            Vector3 toTarget = InterpolatedPosition - casterCenter;
            float beamLength = toTarget.MagnitudeHorizontal();
            Vector3 beamDirection = toTarget.Yto0().normalized;
            IntVec3 blockedCell = GenSight.LastPointOnLineOfSight(caster.Position, targetCell, (IntVec3 c) => c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
            if (blockedCell.IsValid)
            {
                beamLength -= (targetCell - blockedCell).LengthHorizontal;
                targetPosition = casterCenter + beamDirection * beamLength;
                targetCell = targetPosition.ToIntVec3();
            }

            Vector3 origin = BeamOrigin(beamDirection);
            Vector3 originOffset = origin - casterCenter;
            Vector3 targetOffset = targetPosition - targetCell.ToVector3Shifted();
            if (mote != null)
            {
                mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map), new TargetInfo(targetCell, caster.Map), originOffset, targetOffset);
                mote.Maintain();
            }
            if (verbProps.beamGroundFleckDef != null && Rand.Chance(verbProps.beamFleckChancePerTick))
            {
                FleckMaker.Static(targetPosition, caster.Map, verbProps.beamGroundFleckDef);
            }
            if (endEffecter == null && verbProps.beamEndEffecterDef != null)
            {
                endEffecter = verbProps.beamEndEffecterDef.Spawn(targetCell, caster.Map, targetOffset);
            }
            if (endEffecter != null)
            {
                endEffecter.offset = targetOffset;
                endEffecter.EffectTick(new TargetInfo(targetCell, caster.Map), TargetInfo.Invalid);
                endEffecter.ticksLeft--;
            }
            if (verbProps.beamLineFleckDef != null)
            {
                float fleckSamples = beamLength;
                for (int i = 0; i < fleckSamples; i++)
                {
                    if (Rand.Chance(verbProps.beamLineFleckChanceCurve.Evaluate(i / fleckSamples)))
                    {
                        Vector3 fleckOffset = i * beamDirection - beamDirection * Rand.Value + beamDirection / 2f;
                        FleckMaker.Static(origin + fleckOffset, caster.Map, verbProps.beamLineFleckDef);
                    }
                }
            }

            sustainer?.Maintain();
        }

        private Vector3 BeamOrigin(Vector3 beamDirection)
        {
            VerbProperties_ShootBeamFromOffset offsetProps = OffsetProps;
            Vector2 originOffset = offsetProps?.ParsedBeamOriginOffsetFor(caster.Rotation) ?? Vector2.zero;
            float forwardOffset = verbProps.beamStartOffset + originOffset.x;
            float rightOffset = originOffset.y;
            Vector3 right = beamDirection.RotatedBy(90f);
            return caster.Position.ToVector3Shifted() + beamDirection * forwardOffset + right * rightOffset;
        }
    }
}
