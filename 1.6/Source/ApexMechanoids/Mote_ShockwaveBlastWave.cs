using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class Mote_ShockwaveBlastWave : Mote
    {
        private const float ImpactBandPadding = 0.75f;

        private float startScale = 1f;
        private float endScale = 8f;
        private int lifetimeTicks = 15;
        private float rotationPerTick;

        private Thing caster;
        private float gameplayRadius = 8f;
        private ThingDef throwFlyerDef;
        private int stunTicksOnImpact = 240;
        private int minThrowDistance = 2;
        private int maxThrowDistance = 6;
        private int empDamageAmount;
        private float processedRadius = -ImpactBandPadding;
        private List<int> processedPawnIds = new List<int>();
        private HashSet<int> processedPawnLookup;
        private FleckDef electricalSparkFleck;
        private FleckDef lightningGlowFleck;

        private float AgeProgress
        {
            get
            {
                int ageTicks = Find.TickManager.TicksGame - spawnedTick;
                return Mathf.Clamp01(ageTicks / (float)Mathf.Max(lifetimeTicks, 1));
            }
        }

        private float CurrentScale => Mathf.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, AgeProgress));

        private float CurrentWaveRadius => Mathf.Lerp(0f, gameplayRadius, Mathf.SmoothStep(0f, 1f, AgeProgress));

        private float CurrentAlpha
        {
            get
            {
                float ageProgress = AgeProgress;
                if (ageProgress <= 0.06f)
                {
                    return Mathf.Lerp(0f, 0.78f, ageProgress / 0.06f);
                }

                if (ageProgress <= 0.28f)
                {
                    return Mathf.Lerp(0.78f, 0.38f, Mathf.InverseLerp(0.06f, 0.28f, ageProgress));
                }

                if (ageProgress <= 0.7f)
                {
                    return Mathf.Lerp(0.38f, 0.12f, Mathf.InverseLerp(0.28f, 0.7f, ageProgress));
                }

                return Mathf.Lerp(0.12f, 0f, Mathf.InverseLerp(0.7f, 1f, ageProgress));
            }
        }

        public void Initialize(
            Vector3 position,
            float startScale,
            float endScale,
            int lifetimeTicks,
            Thing caster,
            float gameplayRadius,
            ThingDef throwFlyerDef,
            int stunTicksOnImpact,
            int minThrowDistance,
            int maxThrowDistance,
            int empDamageAmount)
        {
            exactPosition = position;
            exactRotation = Rand.Range(0f, 360f);
            rotationPerTick = Rand.Range(0.35f, 0.9f) * (Rand.Bool ? 1f : -1f);
            this.startScale = Mathf.Max(startScale, 0.05f);
            this.endScale = Mathf.Max(endScale, this.startScale);
            this.lifetimeTicks = Mathf.Max(lifetimeTicks, 1);
            this.caster = caster;
            this.gameplayRadius = Mathf.Max(gameplayRadius, 0.1f);
            this.throwFlyerDef = throwFlyerDef;
            this.stunTicksOnImpact = Mathf.Max(stunTicksOnImpact, 0);
            this.minThrowDistance = Mathf.Max(minThrowDistance, 1);
            this.maxThrowDistance = Mathf.Max(maxThrowDistance, this.minThrowDistance);
            this.empDamageAmount = Mathf.Max(empDamageAmount, 0);
            processedRadius = -ImpactBandPadding;
            EnsureProcessedLookup();
        }

        public override void Tick()
        {
            base.Tick();

            if (Destroyed || MapHeld == null)
            {
                return;
            }

            exactRotation += rotationPerTick;
            ProcessWaveFront();
            if (AgeProgress >= 1f)
            {
                Destroy();
            }
        }

        private void ProcessWaveFront()
        {
            float currentRadius = CurrentWaveRadius;
            if (currentRadius <= processedRadius)
            {
                return;
            }

            EnsureProcessedLookup();

            float minRadius = Mathf.Max(0f, processedRadius - ImpactBandPadding);
            float maxRadius = currentRadius + ImpactBandPadding;
            IntVec3 center = PositionHeld;
            EmitElectricalPulseFront(center, currentRadius);
            IReadOnlyList<Pawn> pawns = MapHeld.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!CanImpactPawn(pawn))
                {
                    continue;
                }

                int pawnId = pawn.thingIDNumber;
                if (processedPawnLookup.Contains(pawnId))
                {
                    continue;
                }

                float distance = pawn.PositionHeld.DistanceTo(center);
                if (distance > gameplayRadius || distance < minRadius || distance > maxRadius)
                {
                    continue;
                }

                processedPawnLookup.Add(pawnId);
                processedPawnIds.Add(pawnId);

                if (pawn.RaceProps?.IsMechanoid ?? false)
                {
                    ApplyEmpToMech(pawn);
                    continue;
                }

                ThrowPawn(pawn, center);
            }

            processedRadius = currentRadius;
        }

        private void EmitElectricalPulseFront(IntVec3 center, float currentRadius)
        {
            if (!center.InBounds(MapHeld))
            {
                return;
            }

            EnsureEffectFlecks();
            if (electricalSparkFleck == null)
            {
                return;
            }

            float alphaFactor = Mathf.Clamp01(CurrentAlpha / 0.78f);
            float radiusProgress = Mathf.Clamp01(currentRadius / Mathf.Max(gameplayRadius, 0.1f));
            float centerFactor = 1f - radiusProgress;
            float outerFadeFactor = Mathf.Lerp(1f, 0.22f, radiusProgress);
            int sparkCount = Mathf.RoundToInt(Mathf.Lerp(4f, 2f, radiusProgress));
            float ringRadius = Mathf.Max(currentRadius, 0.2f);
            Vector3 centerPos = center.ToVector3Shifted();

            for (int i = 0; i < sparkCount; i++)
            {
                float angle = Rand.Range(0f, 360f);
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                Vector3 pos = centerPos + radial * Mathf.Max(0.15f, ringRadius + Rand.Range(-0.32f, 0.32f));
                IntVec3 cell = pos.ToIntVec3();
                if (!cell.InBounds(MapHeld) || !cell.ShouldSpawnMotesAt(MapHeld))
                {
                    continue;
                }

                float minSparkScale = Mathf.Lerp(2.15f, 1.9f, radiusProgress);
                float maxSparkScale = Mathf.Lerp(3.9f, 2.8f, radiusProgress);
                float sparkScale = Rand.Range(minSparkScale, maxSparkScale) * (0.72f + alphaFactor * 0.52f);
                float minSparkAlpha = Mathf.Lerp(0.34f, 0.14f, radiusProgress);
                float maxSparkAlpha = Mathf.Lerp(0.78f, 0.36f, radiusProgress);
                float sparkAlpha = Rand.Range(minSparkAlpha, maxSparkAlpha) * (0.68f + alphaFactor * 0.34f) * outerFadeFactor;
                FleckCreationData sparkData = FleckMaker.GetDataStatic(pos, MapHeld, electricalSparkFleck, sparkScale);
                sparkData.rotation = Rand.Range(0f, 360f);
                sparkData.rotationRate = Rand.Range(-180f, 180f);
                sparkData.velocityAngle = angle + Rand.Range(-65f, 65f);
                sparkData.velocitySpeed = Rand.Range(0.05f, 0.2f);
                sparkData.instanceColor = new Color(1f, 1f, 1f, sparkAlpha);
                MapHeld.flecks.CreateFleck(sparkData);
            }

            int glowTickModulo = centerFactor > 0.55f ? 3 : 4;
            if (lightningGlowFleck != null && alphaFactor > 0.12f && Find.TickManager.TicksGame % glowTickModulo == 0)
            {
                float angle = Rand.Range(0f, 360f);
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                Vector3 pos = centerPos + radial * Mathf.Max(0.12f, ringRadius + Rand.Range(-0.18f, 0.18f));
                IntVec3 cell = pos.ToIntVec3();
                if (cell.InBounds(MapHeld) && cell.ShouldSpawnMotesAt(MapHeld))
                {
                    float glowScale = Rand.Range(Mathf.Lerp(0.82f, 0.5f, radiusProgress), Mathf.Lerp(1.35f, 0.82f, radiusProgress)) * (0.78f + alphaFactor * 0.3f);
                    float glowAlpha = Rand.Range(Mathf.Lerp(0.12f, 0.03f, radiusProgress), Mathf.Lerp(0.22f, 0.1f, radiusProgress)) * (0.68f + alphaFactor * 0.24f) * outerFadeFactor;
                    FleckCreationData glowData = FleckMaker.GetDataStatic(pos, MapHeld, lightningGlowFleck, glowScale);
                    glowData.rotation = Rand.Range(0f, 360f);
                    glowData.rotationRate = Rand.Range(-70f, 70f);
                    glowData.velocitySpeed = 0f;
                    glowData.instanceColor = new Color(1f, 1f, 1f, glowAlpha);
                    MapHeld.flecks.CreateFleck(glowData);
                }
            }
        }

        private void EnsureEffectFlecks()
        {
            if (electricalSparkFleck == null)
            {
                electricalSparkFleck = DefDatabase<FleckDef>.GetNamedSilentFail("ElectricalSpark");
            }

            if (lightningGlowFleck == null)
            {
                lightningGlowFleck = DefDatabase<FleckDef>.GetNamedSilentFail("LightningGlow");
            }
        }

        private bool CanImpactPawn(Pawn pawn)
        {
            if (pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }

            return pawn.MapHeld == MapHeld;
        }

        private void ApplyEmpToMech(Pawn pawn)
        {
            if (empDamageAmount <= 0)
            {
                return;
            }

            DamageInfo empDamage = new DamageInfo(DamageDefOf.EMP, empDamageAmount, 0f, -1f, caster);
            pawn.TakeDamage(empDamage);
        }

        private void ThrowPawn(Pawn pawn, IntVec3 center)
        {
            IntVec3 destination = FindThrowDestination(pawn, center);
            if (destination == pawn.PositionHeld || throwFlyerDef == null || pawn.ParentHolder is PawnFlyer)
            {
                StunPawn(pawn);
                return;
            }

            PawnFlyer_ShockwaveThrown flyer = PawnFlyer.MakeFlyer(throwFlyerDef, pawn, destination, null, null) as PawnFlyer_ShockwaveThrown;
            if (flyer == null)
            {
                StunPawn(pawn);
                return;
            }

            flyer.Initialize(stunTicksOnImpact, caster);
            GenSpawn.Spawn(flyer, pawn.PositionHeld, MapHeld, WipeMode.Vanish);
        }

        private IntVec3 FindThrowDestination(Pawn pawn, IntVec3 center)
        {
            Vector3 centerPos = center.ToVector3Shifted();
            Vector3 direction = (pawn.PositionHeld.ToVector3Shifted() - centerPos).Yto0();
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward;
            }

            direction.Normalize();

            float distance = pawn.PositionHeld.DistanceTo(center);
            float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(gameplayRadius, 0.1f));
            float baseThrowDistance = Mathf.Lerp(minThrowDistance, maxThrowDistance, closeness);
            float bodySizeFactor = 1f / Mathf.Clamp(pawn.BodySize, 0.25f, 8f);
            int throwDistance = Mathf.Clamp(Mathf.RoundToInt(baseThrowDistance * bodySizeFactor), 1, Mathf.CeilToInt(maxThrowDistance * 2f));

            IntVec3 bestCell = pawn.PositionHeld;
            Vector3 origin = pawn.PositionHeld.ToVector3Shifted();
            for (int step = 1; step <= throwDistance; step++)
            {
                IntVec3 cell = (origin + direction * step).ToIntVec3();
                if (!cell.InBounds(MapHeld))
                {
                    break;
                }

                if (CanLandIn(cell, pawn))
                {
                    bestCell = cell;
                    continue;
                }

                if (bestCell != pawn.PositionHeld)
                {
                    break;
                }
            }

            if (bestCell != pawn.PositionHeld)
            {
                return bestCell;
            }

            for (int radius = 1; radius <= 2; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.PositionHeld, radius, true))
                {
                    if (cell.InBounds(MapHeld) && CanLandIn(cell, pawn) && cell.DistanceTo(center) > pawn.PositionHeld.DistanceTo(center))
                    {
                        return cell;
                    }
                }
            }

            return pawn.PositionHeld;
        }

        private bool CanLandIn(IntVec3 cell, Pawn pawn)
        {
            if (!cell.Standable(MapHeld))
            {
                return false;
            }

            Pawn occupant = cell.GetFirstPawn(MapHeld);
            if (occupant != null && occupant != pawn)
            {
                return false;
            }

            return true;
        }

        private void StunPawn(Pawn pawn)
        {
            if (stunTicksOnImpact <= 0)
            {
                return;
            }

            if (pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(stunTicksOnImpact, caster, addBattleLog: false);
            }
        }

        private void EnsureProcessedLookup()
        {
            if (processedPawnIds == null)
            {
                processedPawnIds = new List<int>();
            }

            if (processedPawnLookup == null)
            {
                processedPawnLookup = new HashSet<int>(processedPawnIds);
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic graphic = def.graphicData?.Graphic;
            if (graphic == null)
            {
                return;
            }

            Vector2 drawSize = def.graphicData.drawSize;
            float scale = CurrentScale;
            Vector3 pos = drawLoc;
            pos.y = def.Altitude;

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(exactRotation, Vector3.up), new Vector3(drawSize.x * scale, 1f, drawSize.y * scale));
            Material material = graphic.MatAt(Rot4.North, this);
            float alpha = CurrentAlpha;
            if (alpha < 0.999f)
            {
                material = FadedMaterialPool.FadedVersionOf(material, alpha);
            }

            GenDraw.DrawMeshNowOrLater(graphic.MeshAt(Rot4.North), matrix, material, false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startScale, nameof(startScale), 1f);
            Scribe_Values.Look(ref endScale, nameof(endScale), 8f);
            Scribe_Values.Look(ref lifetimeTicks, nameof(lifetimeTicks), 15);
            Scribe_Values.Look(ref rotationPerTick, nameof(rotationPerTick), 0f);
            Scribe_Values.Look(ref gameplayRadius, nameof(gameplayRadius), 8f);
            Scribe_Values.Look(ref stunTicksOnImpact, nameof(stunTicksOnImpact), 240);
            Scribe_Values.Look(ref minThrowDistance, nameof(minThrowDistance), 2);
            Scribe_Values.Look(ref maxThrowDistance, nameof(maxThrowDistance), 6);
            Scribe_Values.Look(ref empDamageAmount, nameof(empDamageAmount), 0);
            Scribe_Values.Look(ref processedRadius, nameof(processedRadius), -ImpactBandPadding);
            Scribe_Collections.Look(ref processedPawnIds, nameof(processedPawnIds), LookMode.Value);
            Scribe_References.Look(ref caster, nameof(caster));
            Scribe_Defs.Look(ref throwFlyerDef, nameof(throwFlyerDef));
            Scribe_Defs.Look(ref electricalSparkFleck, nameof(electricalSparkFleck));
            Scribe_Defs.Look(ref lightningGlowFleck, nameof(lightningGlowFleck));

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                processedPawnLookup = null;
                EnsureProcessedLookup();
            }
        }
    }
}
