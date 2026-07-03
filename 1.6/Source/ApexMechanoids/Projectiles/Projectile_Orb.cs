using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_Orb : DefModExtension
    {
        public DamageDef arcDamageDef;
        public int arcDamageAmount = 12;
        public float arcArmorPenetration = 0.18f;
        public float arcRadius = 4.9f;
        public int arcIntervalTicks = 12;
        public float arcStartDistance = 5.5f;
        public int maxArcTargets = 3;
        public ThingDef arcLightningMoteDef;
        public List<ThingDef> arcLightningMoteDefs;
        public float arcGlowSize = 0.75f;
        public float activationFlashScale = 0.8f;
        public EffecterDef impactEffecterDef;
        public float impactFlashScale = 2.1f;
        public int totalArcStrikeBudget = 60;
        public int maxLifetimeTicks = 500;
        public float orbSpinDegreesPerTick = 2.4f;
        public float orbPulseAmplitude = 0.12f;
        public float orbPulseTicks = 24f;
        public float orbActivatedScaleMultiplier = 1.12f;
        public float orbMinAlpha = 0.7f;
        public float orbMaxAlpha = 1f;
        public Color orbColor = Color.white;
        public List<string> orbFramePaths;
    }

    public class Projectile_Orb : Projectile
    {
        private static readonly List<Thing> livingTargets = new List<Thing>();
        private static readonly List<Thing> otherTargets = new List<Thing>();

        private int ticksUntilNextArc;
        private int remainingArcStrikes = -1;
        private int lifetimeTicks;
        private bool plasmaActivated;
        private bool finalDestinationInitialized;
        private Material[] orbFrameMaterials;
        private Dictionary<int, Material[]> orbFrameFadedMaterials;
        private Vector3 launchOrigin;
        private Vector3 finalDestination;

        private DefModExtension_Orb Props => def.GetModExtension<DefModExtension_Orb>();

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire, Thing equipment, ThingDef targetCoverDef)
        {
            LocalTargetInfo launchTarget = usedTarget;
            if (TryCalculateInitialLaunchTarget(origin, usedTarget.IsValid ? usedTarget : intendedTarget, equipment?.def, out LocalTargetInfo retargetedLaunchTarget))
            {
                launchTarget = retargetedLaunchTarget;
            }

            base.Launch(launcher, origin, launchTarget, launchTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            launchOrigin = origin;
            finalDestination = destination;
            finalDestinationInitialized = true;
        }

        public override void Tick()
        {
            base.Tick();

            if (Destroyed || Map == null)
            {
                return;
            }

            DefModExtension_Orb props = Props;
            if (props == null || props.arcIntervalTicks <= 0)
            {
                return;
            }

            lifetimeTicks++;
            if (props.maxLifetimeTicks > 0 && lifetimeTicks >= props.maxLifetimeTicks)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            EnsureArcBudgetInitialized(props);
            if (remainingArcStrikes <= 0)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            if (!plasmaActivated)
            {
                if (!ShouldActivatePlasma(props))
                {
                    return;
                }

                plasmaActivated = true;
                ticksUntilNextArc = props.arcIntervalTicks;
                SpawnActivationVisuals(props);
            }

            ticksUntilNextArc--;
            if (ticksUntilNextArc > 0)
            {
                return;
            }

            FireArcPulse(props);
            if (remainingArcStrikes <= 0)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            ticksUntilNextArc = props.arcIntervalTicks;
        }

        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (blockedByShield)
            {
                DestroyWithImpactVisuals();
                return;
            }

            if (ShouldPassThrough(hitThing))
            {
                if (!ContinueFlightToFinalDestination(ExactPosition))
                {
                    DestroyWithImpactVisuals();
                }

                return;
            }

            DestroyWithImpactVisuals();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilNextArc, nameof(ticksUntilNextArc));
            Scribe_Values.Look(ref remainingArcStrikes, nameof(remainingArcStrikes), -1);
            Scribe_Values.Look(ref lifetimeTicks, nameof(lifetimeTicks));
            Scribe_Values.Look(ref plasmaActivated, nameof(plasmaActivated));
            Scribe_Values.Look(ref finalDestinationInitialized, nameof(finalDestinationInitialized));
            Scribe_Values.Look(ref launchOrigin, nameof(launchOrigin));
            Scribe_Values.Look(ref finalDestination, nameof(finalDestination));
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DefModExtension_Orb props = Props;
            if (props == null)
            {
                return;
            }

            Vector2 baseSize = def.graphicData?.drawSize ?? Vector2.one;
            int elapsed = Mathf.Max(Find.TickManager.TicksGame - spawnedTick, 0);
            float pulseTicks = Mathf.Max(1f, props.orbPulseTicks);

            float primaryRotation = elapsed * props.orbSpinDegreesPerTick;
            float primaryPulse = 1f + Mathf.Sin(elapsed / pulseTicks * Mathf.PI * 2f) * props.orbPulseAmplitude;
            float primaryAlpha = Mathf.Lerp(Mathf.Max(0.78f, props.orbMinAlpha), props.orbMaxAlpha, Mathf.PerlinNoise(spawnedTick * 0.173f, elapsed * 0.185f));
            float scaleMultiplier = 1f;

            if (plasmaActivated)
            {
                scaleMultiplier *= props.orbActivatedScaleMultiplier;
            }

            Vector3 pos = drawLoc;
            pos.y = def.Altitude;
            DrawOrbLayer(pos, baseSize, GetPrimaryFrameIndex(elapsed), primaryRotation, primaryPulse, primaryAlpha, scaleMultiplier);
        }

        private bool ShouldActivatePlasma(DefModExtension_Orb props)
        {
            Vector3 start = finalDestinationInitialized ? launchOrigin : origin;
            start.y = 0f;
            Vector3 current = ExactPosition;
            current.y = 0f;
            return Vector3.Distance(start, current) >= props.arcStartDistance;
        }

        private void FireArcPulse(DefModExtension_Orb props)
        {
            IntVec3 centerCell = ExactPosition.ToIntVec3();
            if (!centerCell.InBounds(Map))
            {
                return;
            }

            CollectTargets(centerCell, props.arcRadius);
            if (livingTargets.Count == 0 && otherTargets.Count == 0)
            {
                return;
            }

            int hitsRemaining = props.maxArcTargets > 0 ? props.maxArcTargets : int.MaxValue;
            hitsRemaining = StrikeTargetsFromList(livingTargets, props, hitsRemaining);
            if (hitsRemaining <= 0)
            {
                return;
            }

            StrikeTargetsFromList(otherTargets, props, hitsRemaining);
        }

        private void CollectTargets(IntVec3 centerCell, float radius)
        {
            livingTargets.Clear();
            otherTargets.Clear();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centerCell, radius, true))
            {
                if (!cell.InBounds(Map))
                {
                    continue;
                }

                List<Thing> things = Map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (!IsValidArcTarget(thing, centerCell, radius))
                    {
                        continue;
                    }

                    if (thing is Pawn pawn && !pawn.Dead)
                    {
                        AddDistinct(livingTargets, pawn);
                    }
                    else
                    {
                        AddDistinct(otherTargets, thing);
                    }
                }
            }

            SortTargetsByDistance(livingTargets, centerCell);
            SortTargetsByDistance(otherTargets, centerCell);
        }

        private bool IsValidArcTarget(Thing thing, IntVec3 centerCell, float radius)
        {
            if (thing == null || thing == launcher || thing == this || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            if (thing is Pawn pawn)
            {
                return pawn.Spawned && !pawn.Dead;
            }

            if (!thing.def.useHitPoints || thing.HitPoints <= 0)
            {
                return false;
            }

            if (thing.PositionHeld.DistanceTo(centerCell) > radius)
            {
                return false;
            }

            return true;
        }

        private void StrikeTarget(Thing target, DefModExtension_Orb props)
        {
            SpawnLightningOverlay(target, props);
            ApplyArcDamage(target, props, 1f);
            ApplyCollateralDamage(target, props);
            remainingArcStrikes--;
        }

        private void SpawnLightningOverlay(Thing target, DefModExtension_Orb props)
        {
            ThingDef moteDef = RandomLightningMote(props);
            if (moteDef == null)
            {
                return;
            }

            IntVec3 targetCell = target.PositionHeld;
            if (!targetCell.InBounds(Map))
            {
                return;
            }

            Vector3 targetCenter = targetCell.ToVector3Shifted();
            Vector3 targetOffset = target.DrawPos - targetCenter;
            MoteMaker.MakeInteractionOverlay(moteDef, this, new TargetInfo(targetCell, Map), Vector3.zero, targetOffset);
            FleckMaker.Static(target.DrawPos, Map, FleckDefOf.ExplosionFlash, props.arcGlowSize * 0.4f);
            FleckMaker.ThrowLightningGlow(ExactPosition, Map, props.arcGlowSize * 0.7f);
            FleckMaker.ThrowLightningGlow(target.DrawPos, Map, props.arcGlowSize);
        }

        private ThingDef RandomLightningMote(DefModExtension_Orb props)
        {
            if (props.arcLightningMoteDef != null)
            {
                return props.arcLightningMoteDef;
            }

            if (props.arcLightningMoteDefs == null || props.arcLightningMoteDefs.Count == 0)
            {
                return null;
            }

            return props.arcLightningMoteDefs.RandomElement();
        }

        private void SpawnActivationVisuals(DefModExtension_Orb props)
        {
            FleckMaker.ThrowLightningGlow(ExactPosition, Map, props.activationFlashScale);
            if (Rand.Chance(0.6f))
            {
                FleckMaker.Static(ExactPosition, Map, FleckDefOf.ExplosionFlash, props.activationFlashScale * 0.7f);
            }
        }

        private void SpawnImpactVisuals()
        {
            DefModExtension_Orb props = Props;
            if (Map == null || props == null)
            {
                return;
            }

            if (props.impactEffecterDef != null)
            {
                Effecter effecter = props.impactEffecterDef.Spawn(PositionHeld, Map);
                if (effecter != null)
                {
                    effecter.EffectTick(new TargetInfo(PositionHeld, Map), TargetInfo.Invalid);
                    effecter.Cleanup();
                }
            }

            FleckMaker.Static(ExactPosition, Map, FleckDefOf.ExplosionFlash, props.impactFlashScale);
            FleckMaker.ThrowLightningGlow(ExactPosition, Map, props.impactFlashScale * 0.55f);
        }

        private void DestroyWithImpactVisuals()
        {
            if (Destroyed)
            {
                return;
            }

            SpawnImpactVisuals();
            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
            Destroy(DestroyMode.Vanish);
        }

        private bool ShouldPassThrough(Thing hitThing)
        {
            if (hitThing == null || hitThing.Destroyed)
            {
                return false;
            }

            if (hitThing is Pawn || hitThing.def.category == ThingCategory.Plant)
            {
                return true;
            }

            return hitThing.def.Fillage != FillCategory.Full && hitThing.def.passability != Traversability.Impassable;
        }

        private bool TryCalculateInitialLaunchTarget(Vector3 startPosition, LocalTargetInfo requestedTarget, ThingDef launchEquipmentDef, out LocalTargetInfo launchTarget)
        {
            launchTarget = requestedTarget;
            if (!requestedTarget.IsValid)
            {
                return false;
            }

            Vector3 currentDirection = requestedTarget.CenterVector3 - startPosition;
            currentDirection.y = 0f;
            currentDirection = currentDirection.normalized;
            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float configuredTravelDistance = GetConfiguredTravelDistance(startPosition, requestedTarget.CenterVector3, launchEquipmentDef);
            if (configuredTravelDistance <= 0.01f)
            {
                return false;
            }

            float distanceToMapEdge = GetDistanceToMapEdge(startPosition, currentDirection);
            float finalTravelDistance = Mathf.Min(configuredTravelDistance, distanceToMapEdge);
            if (finalTravelDistance <= 0.01f)
            {
                return false;
            }

            Vector3 computedDestination = startPosition + currentDirection * finalTravelDistance;
            computedDestination.x = Mathf.Clamp(computedDestination.x, 0.5f, Map.Size.x - 0.5f);
            computedDestination.z = Mathf.Clamp(computedDestination.z, 0.5f, Map.Size.z - 0.5f);
            computedDestination.y = requestedTarget.CenterVector3.y;
            launchTarget = new LocalTargetInfo(computedDestination.ToIntVec3());
            return true;
        }

        private bool ContinueFlightToFinalDestination(Vector3 startPosition)
        {
            if (!finalDestinationInitialized)
            {
                return false;
            }

            float speedPerTick = GetCurrentSpeedPerTick(startPosition);
            if (speedPerTick <= 0.0001f)
            {
                return false;
            }

            float remainingDistance = FlatDistance(startPosition, finalDestination);
            if (remainingDistance <= 0.01f)
            {
                return false;
            }

            landed = false;
            origin = startPosition;
            destination = finalDestination;
            ticksToImpact = Mathf.Max(1, Mathf.CeilToInt(remainingDistance / speedPerTick));
            return true;
        }

        private float GetCurrentSpeedPerTick(Vector3 currentPosition)
        {
            float remainingDistance = FlatDistance(currentPosition, destination);
            if (ticksToImpact > 0 && remainingDistance > 0.0001f)
            {
                return remainingDistance / ticksToImpact;
            }

            float originalDistance = FlatDistance(origin, destination);
            if (StartingTicksToImpact > 0.0001f && originalDistance > 0.0001f)
            {
                return originalDistance / StartingTicksToImpact;
            }

            return 0f;
        }

        private float GetConfiguredTravelDistance(Vector3 startPosition, Vector3 requestedDestination, ThingDef launchEquipmentDef)
        {
            if (launchEquipmentDef != null)
            {
                List<VerbProperties> verbs = launchEquipmentDef.Verbs;
                if (verbs != null && verbs.Count > 0 && verbs[0] != null && verbs[0].range > 0.01f)
                {
                    return verbs[0].range;
                }
            }

            return FlatDistance(startPosition, requestedDestination);
        }

        private float GetDistanceToMapEdge(Vector3 startPosition, Vector3 normalizedDirection)
        {
            if (Map == null || normalizedDirection.sqrMagnitude < 0.0001f)
            {
                return float.MaxValue;
            }

            float minX = 0.5f;
            float maxX = Map.Size.x - 0.5f;
            float minZ = 0.5f;
            float maxZ = Map.Size.z - 0.5f;
            float bestT = float.MaxValue;

            if (Mathf.Abs(normalizedDirection.x) > 0.0001f)
            {
                TryTakeCloserEdgeDistance(ref bestT, (minX - startPosition.x) / normalizedDirection.x);
                TryTakeCloserEdgeDistance(ref bestT, (maxX - startPosition.x) / normalizedDirection.x);
            }

            if (Mathf.Abs(normalizedDirection.z) > 0.0001f)
            {
                TryTakeCloserEdgeDistance(ref bestT, (minZ - startPosition.z) / normalizedDirection.z);
                TryTakeCloserEdgeDistance(ref bestT, (maxZ - startPosition.z) / normalizedDirection.z);
            }

            return bestT;
        }

        private static void TryTakeCloserEdgeDistance(ref float bestT, float candidateT)
        {
            if (candidateT > 0.01f && candidateT < bestT)
            {
                bestT = candidateT;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void SortTargetsByDistance(List<Thing> list, IntVec3 centerCell)
        {
            if (list.Count <= 1)
            {
                return;
            }

            list.Sort((a, b) => a.PositionHeld.DistanceTo(centerCell).CompareTo(b.PositionHeld.DistanceTo(centerCell)));
        }

        private static void AddDistinct(List<Thing> list, Thing thing)
        {
            if (!list.Contains(thing))
            {
                list.Add(thing);
            }
        }

        private int StrikeTargetsFromList(List<Thing> targets, DefModExtension_Orb props, int hitsRemaining)
        {
            if (hitsRemaining <= 0 || targets == null || targets.Count == 0)
            {
                return hitsRemaining;
            }

            for (int i = 0; i < targets.Count && hitsRemaining > 0; i++)
            {
                Thing target = targets[i];
                if (target == null || target.Destroyed || !target.Spawned)
                {
                    continue;
                }

                StrikeTarget(target, props);
                hitsRemaining--;
                if (remainingArcStrikes <= 0)
                {
                    break;
                }
            }

            return hitsRemaining;
        }

        private void EnsureArcBudgetInitialized(DefModExtension_Orb props)
        {
            if (remainingArcStrikes >= 0)
            {
                return;
            }

            remainingArcStrikes = Mathf.Max(1, props.totalArcStrikeBudget);
        }

        private Material GetOrbMaterial(int index)
        {
            IList<string> orbFramePaths = GetOrbFramePaths();
            if (orbFramePaths.Count == 0)
            {
                return null;
            }

            index = Mathf.Clamp(index, 0, orbFramePaths.Count - 1);
            if (orbFrameMaterials == null || orbFrameMaterials.Length != orbFramePaths.Count)
            {
                orbFrameMaterials = new Material[orbFramePaths.Count];
            }
            if (orbFrameMaterials[index] == null)
            {
                Material baseMaterial = MaterialPool.MatFrom(orbFramePaths[index], ShaderDatabase.MoteGlow);
                Material tintedMaterial = new Material(baseMaterial);
                tintedMaterial.color = GetOrbColor();
                orbFrameMaterials[index] = tintedMaterial;
            }

            return orbFrameMaterials[index];
        }

        private Material GetOrbMaterial(int index, float alpha)
        {
            Material baseMaterial = GetOrbMaterial(index);
            if (baseMaterial == null)
            {
                return null;
            }

            int alphaStep = Mathf.Clamp(Mathf.RoundToInt(alpha * 32f), 0, 32);
            if (alphaStep >= 32)
            {
                return baseMaterial;
            }

            IList<string> orbFramePaths = GetOrbFramePaths();
            if (orbFrameFadedMaterials == null)
            {
                orbFrameFadedMaterials = new Dictionary<int, Material[]>();
            }

            if (!orbFrameFadedMaterials.TryGetValue(alphaStep, out Material[] fadedMaterials) || fadedMaterials == null || fadedMaterials.Length != orbFramePaths.Count)
            {
                fadedMaterials = new Material[orbFramePaths.Count];
                orbFrameFadedMaterials[alphaStep] = fadedMaterials;
            }

            if (fadedMaterials[index] == null)
            {
                Material fadedMaterial = new Material(baseMaterial);
                Color color = GetOrbColor();
                color.a *= alphaStep / 32f;
                fadedMaterial.color = color;
                fadedMaterials[index] = fadedMaterial;
            }

            return fadedMaterials[index];
        }

        private int GetPrimaryFrameIndex(int elapsed)
        {
            return GetFrameIndex(elapsed, 4, 0);
        }

        private int GetFrameIndex(int elapsed, int ticksPerFrame, int phaseOffset)
        {
            int frameCount = GetOrbFramePaths().Count;
            if (frameCount == 0)
            {
                return 0;
            }

            return ((elapsed + phaseOffset) / Mathf.Max(1, ticksPerFrame)) % frameCount;
        }

        private IList<string> GetOrbFramePaths()
        {
            if (Props != null && Props.orbFramePaths != null && Props.orbFramePaths.Count > 0)
            {
                return Props.orbFramePaths;
            }

            return new string[0];
        }

        private Color GetOrbColor()
        {
            Color color = Props != null ? Props.orbColor : Color.white;
            if (color.r > 1f || color.g > 1f || color.b > 1f || color.a > 1f)
            {
                color = new Color(color.r / 255f, color.g / 255f, color.b / 255f, color.a > 1f ? color.a / 255f : color.a);
            }

            if (color.a <= 0f)
            {
                color.a = 1f;
            }

            return color;
        }

        private void DrawOrbLayer(Vector3 pos, Vector2 baseSize, int frameIndex, float rotation, float pulse, float alpha, float baseScaleMultiplier)
        {
            Material material = GetOrbMaterial(frameIndex, alpha);
            if (material == null)
            {
                return;
            }

            float finalScale = pulse * baseScaleMultiplier;
            Vector3 scale = new Vector3(baseSize.x * finalScale, 1f, baseSize.y * finalScale);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(rotation, Vector3.up), scale);
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, matrix, material, false);
        }

        private void ApplyArcDamage(Thing target, DefModExtension_Orb props, float damageFactor)
        {
            if (target == null || target.Destroyed || !target.Spawned)
            {
                return;
            }

            Vector3 sourcePos = ExactPosition;
            Vector3 targetPos = target.DrawPos;
            float angle = (targetPos - sourcePos).AngleFlat();
            float amount = Mathf.Max(1f, props.arcDamageAmount * damageFactor);
            DamageDef damageDef = props.arcDamageDef ?? DamageDefOf.Burn;
            DamageInfo damageInfo = new DamageInfo(damageDef, amount, props.arcArmorPenetration, angle, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing);
            target.TakeDamage(damageInfo);

            if (target is Pawn pawn && pawn.Spawned && !pawn.Dead)
            {
                pawn.TryAttachFire(Rand.Range(0.2f, 0.45f), launcher);
            }
            else if (Rand.Chance(0.35f))
            {
                FireUtility.TryStartFireIn(target.PositionHeld, Map, Rand.Range(0.2f, 0.5f), launcher);
            }
        }

        private void ApplyCollateralDamage(Thing primaryTarget, DefModExtension_Orb props)
        {
            if (primaryTarget == null || Map == null)
            {
                return;
            }

            List<Thing> things = Map.thingGrid.ThingsListAtFast(primaryTarget.PositionHeld);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing == primaryTarget || thing == launcher || thing == this || thing.Destroyed || !thing.Spawned)
                {
                    continue;
                }

                if (!(thing is Pawn) && (!thing.def.useHitPoints || thing.HitPoints <= 0))
                {
                    continue;
                }

                ApplyArcDamage(thing, props, 0.5f);
            }
        }
    }
}
