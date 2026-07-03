using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class CompAbilityEffect_Shockwave : CompAbilityEffect
    {
        public new CompProperties_Shockwave Props => (CompProperties_Shockwave)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !caster.Spawned)
            {
                return;
            }

            IntVec3 center = caster.PositionHeld;
            SpawnBurstVisuals(caster, map, center);
            SpawnEmitter(caster, map, center);
            bool waveSpawned = SpawnBlastWave(caster, map, center);
            if (!waveSpawned)
            {
                ThrowAffectedPawns(caster, map, center);
                ApplyEmpToMechs(caster, map, center);
            }
        }

        private void SpawnBurstVisuals(Pawn caster, Map map, IntVec3 center)
        {
            if (Props.flashThingDef != null && center.InBounds(map))
            {
                Thing flashThing = ThingMaker.MakeThing(Props.flashThingDef);
                GenSpawn.Spawn(flashThing, center, map, WipeMode.Vanish);
            }

            if (Props.burstEffecterDef != null)
            {
                Effecter effecter = Props.burstEffecterDef.Spawn(center, map);
                if (effecter != null)
                {
                    TargetInfo targetInfo = new TargetInfo(center, map);
                    effecter.EffectTick(targetInfo, TargetInfo.Invalid);
                    effecter.Cleanup();
                }
            }

            if (!Props.castSoundDefName.NullOrEmpty())
            {
                SoundDef castSound = DefDatabase<SoundDef>.GetNamedSilentFail(Props.castSoundDefName);
                castSound?.PlayOneShot(new TargetInfo(center, map));
            }
        }

        private void SpawnEmitter(Pawn caster, Map map, IntVec3 center)
        {
            if (Props.emitterDef == null)
            {
                return;
            }

            Mote_ShockwaveEmitter emitter = ThingMaker.MakeThing(Props.emitterDef) as Mote_ShockwaveEmitter;
            if (emitter == null)
            {
                return;
            }

            emitter.Initialize(caster.DrawPos, center, Props.radius, Props.ringIntervalTicks, Props.visualScale, Props.ringFleckDefName);
            GenSpawn.Spawn(emitter, center, map);
        }

        private bool SpawnBlastWave(Pawn caster, Map map, IntVec3 center)
        {
            if (Props.blastWaveDef == null || !center.InBounds(map))
            {
                return false;
            }

            Mote_ShockwaveBlastWave blastWave = ThingMaker.MakeThing(Props.blastWaveDef) as Mote_ShockwaveBlastWave;
            if (blastWave == null)
            {
                return false;
            }

            blastWave.Initialize(
                center.ToVector3Shifted(),
                Props.blastWaveStartScale,
                Props.blastWaveEndScale,
                Props.blastWaveLifetimeTicks,
                caster,
                Props.radius,
                Props.throwFlyerDef,
                Props.stunTicks,
                Props.minThrowDistance,
                Props.maxThrowDistance,
                Props.empDamageAmount);
            GenSpawn.Spawn(blastWave, center, map, WipeMode.Vanish);
            return true;
        }

        private void ThrowAffectedPawns(Pawn caster, Map map, IntVec3 center)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!ShouldThrowPawn(caster, pawn, center))
                {
                    continue;
                }

                IntVec3 destination = FindThrowDestination(pawn, center, map);
                if (destination == pawn.Position || Props.throwFlyerDef == null || pawn.ParentHolder is PawnFlyer)
                {
                    StunPawn(pawn, caster);
                    continue;
                }

                PawnFlyer_ShockwaveThrown flyer = PawnFlyer.MakeFlyer(Props.throwFlyerDef, pawn, destination, null, null) as PawnFlyer_ShockwaveThrown;
                if (flyer == null)
                {
                    StunPawn(pawn, caster);
                    continue;
                }

                flyer.Initialize(Props.stunTicks, caster);
                GenSpawn.Spawn(flyer, pawn.PositionHeld, map, WipeMode.Vanish);
            }
        }

        private void ApplyEmpToMechs(Pawn caster, Map map, IntVec3 center)
        {
            if (Props.empDamageAmount <= 0)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!ShouldEmpMech(caster, pawn, center))
                {
                    continue;
                }

                DamageInfo empDamage = new DamageInfo(DamageDefOf.EMP, Props.empDamageAmount, 0f, -1f, caster);
                pawn.TakeDamage(empDamage);
            }
        }

        private IntVec3 FindThrowDestination(Pawn pawn, IntVec3 center, Map map)
        {
            Vector3 centerPos = center.ToVector3Shifted();
            Vector3 direction = (pawn.Position.ToVector3Shifted() - centerPos).Yto0();
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward;
            }

            direction.Normalize();

            float distance = pawn.Position.DistanceTo(center);
            float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(Props.radius, 0.1f));
            float baseThrowDistance = Mathf.Lerp(Props.minThrowDistance, Props.maxThrowDistance, closeness);
            float bodySizeFactor = 1f / Mathf.Clamp(pawn.BodySize, 0.25f, 8f);
            int throwDistance = Mathf.Clamp(Mathf.RoundToInt(baseThrowDistance * bodySizeFactor), 1, Mathf.CeilToInt(Props.maxThrowDistance * 2f));

            IntVec3 bestCell = pawn.Position;
            Vector3 origin = pawn.Position.ToVector3Shifted();
            for (int step = 1; step <= throwDistance; step++)
            {
                IntVec3 cell = (origin + direction * step).ToIntVec3();
                if (!cell.InBounds(map))
                {
                    break;
                }

                if (CanLandIn(cell, map, pawn))
                {
                    bestCell = cell;
                    continue;
                }

                if (bestCell != pawn.Position)
                {
                    break;
                }
            }

            if (bestCell != pawn.Position)
            {
                return bestCell;
            }

            for (int radius = 1; radius <= 2; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
                {
                    if (cell.InBounds(map) && CanLandIn(cell, map, pawn) && cell.DistanceTo(center) > pawn.Position.DistanceTo(center))
                    {
                        return cell;
                    }
                }
            }

            return pawn.Position;
        }

        private static bool CanLandIn(IntVec3 cell, Map map, Pawn pawn)
        {
            if (!cell.Standable(map))
            {
                return false;
            }

            if (cell.GetFirstPawn(map) != null)
            {
                return false;
            }

            return true;
        }

        private void StunPawn(Pawn pawn, Thing instigator)
        {
            if (pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(Props.stunTicks, instigator, addBattleLog: false);
            }
        }

        private bool ShouldThrowPawn(Pawn caster, Pawn pawn, IntVec3 center)
        {
            if (pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }

            if (pawn.RaceProps?.IsMechanoid ?? false)
            {
                return false;
            }

            if (pawn.Position.DistanceTo(center) > Props.radius)
            {
                return false;
            }

            return true;
        }

        private bool ShouldEmpMech(Pawn caster, Pawn pawn, IntVec3 center)
        {
            if (pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }

            if (!(pawn.RaceProps?.IsMechanoid ?? false))
            {
                return false;
            }

            if (pawn.Position.DistanceTo(center) > Props.radius)
            {
                return false;
            }

            return true;
        }
    }

    public class CompProperties_Shockwave : CompProperties_AbilityEffect
    {
        public CompProperties_Shockwave()
        {
            compClass = typeof(CompAbilityEffect_Shockwave);
        }

        public ThingDef emitterDef;
        public ThingDef flashThingDef;
        public ThingDef blastWaveDef;
        public ThingDef throwFlyerDef;
        public EffecterDef burstEffecterDef;
        public DamageDef damageDef;
        public DamageDef pushDamageDef;
        public float radius = 8.9f;
        public int damageAmount = 72;
        public float armorPenetration = 1.4f;
        public int stunTicks = 240;
        public int pushDamageAmount = 1;
        public int ringIntervalTicks = 2;
        public float visualScale = 1.22f;
        public float blastWaveStartScale = 0.75f;
        public float blastWaveEndScale = 10.4f;
        public int blastWaveLifetimeTicks = 16;
        public int minThrowDistance = 2;
        public int maxThrowDistance = 6;
        public int empDamageAmount = 8;
        public string ringFleckDefName = "PsycastPsychicEffect";
        public string castSoundDefName = "PsycastPsychicPulse";
    }

    public class CompAbility_ShockwaveWarmup : AbilityComp
    {
        private Effecter chargeEffecter;
        private int ticksUntilNextSpark;

        public CompProperties_ShockwaveWarmup Props => (CompProperties_ShockwaveWarmup)props;

        public override void CompTick()
        {
            base.CompTick();

            Pawn caster = parent.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !caster.Spawned)
            {
                Cleanup();
                return;
            }

            if (!parent.wasCastingOnPrevTick)
            {
                Cleanup();
                return;
            }

            bool hasChargeEffecter = Props.chargeEffecterDef != null;
            bool hasChargeFlecks = !Props.chargeFleckDefName.NullOrEmpty();
            if (!hasChargeEffecter && !hasChargeFlecks)
            {
                return;
            }

            if (chargeEffecter == null && Props.chargeEffecterDef != null)
            {
                chargeEffecter = Props.chargeEffecterDef.Spawn(caster.PositionHeld, map);
            }

            if (chargeEffecter != null)
            {
                TargetInfo targetInfo = new TargetInfo(caster.PositionHeld, map);
                chargeEffecter.offset = caster.DrawPos - caster.PositionHeld.ToVector3Shifted();
                chargeEffecter.EffectTick(targetInfo, TargetInfo.Invalid);
                chargeEffecter.ticksLeft--;
            }

            if (!hasChargeFlecks)
            {
                return;
            }

            if (ticksUntilNextSpark > 0)
            {
                ticksUntilNextSpark--;
                return;
            }

            ticksUntilNextSpark = Mathf.Max(Props.sparkIntervalTicks, 1);
            SpawnChargeFlecks(caster, map);
        }

        private void SpawnChargeFlecks(Pawn caster, Map map)
        {
            FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(Props.chargeFleckDefName);
            if (fleckDef == null || !caster.PositionHeld.ShouldSpawnMotesAt(map))
            {
                return;
            }

            int burstCount = 3;
            for (int i = 0; i < burstCount; i++)
            {
                float angle = Rand.Range(0f, 360f);
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * (Vector3.forward * Rand.Range(0.22f, Props.sparkRadius));
                Vector3 pos = caster.DrawPos + radial;
                FleckCreationData data = FleckMaker.GetDataStatic(pos, map, fleckDef, Props.visualScale * Rand.Range(0.9f, 1.25f));
                data.rotation = angle;
                data.rotationRate = Rand.Range(-35f, 35f);
                data.velocityAngle = angle;
                data.velocitySpeed = Rand.Range(0.05f, 0.14f);
                map.flecks.CreateFleck(data);
            }
        }

        private void Cleanup()
        {
            ticksUntilNextSpark = 0;
            if (chargeEffecter != null)
            {
                chargeEffecter.Cleanup();
                chargeEffecter = null;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextSpark, nameof(ticksUntilNextSpark));
        }
    }

    public class CompProperties_ShockwaveWarmup : AbilityCompProperties
    {
        public CompProperties_ShockwaveWarmup()
        {
            compClass = typeof(CompAbility_ShockwaveWarmup);
        }

        public EffecterDef chargeEffecterDef;
        public string chargeFleckDefName = "PsycastPsychicEffect";
        public int sparkIntervalTicks = 5;
        public float sparkRadius = 1.25f;
        public float visualScale = 0.58f;
    }

    public class Mote_ShockwaveEmitter : Mote
    {
        private IntVec3 originCell;
        private float radius;
        private int ringIntervalTicks;
        private float visualScale;
        private string ringFleckDefName;
        private int ticksUntilNextRing;
        private int currentRing;
        private int maxRing;
        private FleckDef cachedRingFleck;

        public void Initialize(Vector3 exactPos, IntVec3 originCell, float radius, int ringIntervalTicks, float visualScale, string ringFleckDefName)
        {
            this.exactPosition = exactPos;
            this.originCell = originCell;
            this.radius = radius;
            this.ringIntervalTicks = Mathf.Max(ringIntervalTicks, 1);
            this.visualScale = visualScale;
            this.ringFleckDefName = ringFleckDefName;
            this.currentRing = 0;
            this.maxRing = Mathf.CeilToInt(radius);
            this.ticksUntilNextRing = 0;
        }

        public override void Tick()
        {
            base.Tick();

            if (Destroyed || MapHeld == null)
            {
                return;
            }

            if (ticksUntilNextRing > 0)
            {
                ticksUntilNextRing--;
                return;
            }

            EmitRing(currentRing);
            currentRing++;
            if (currentRing > maxRing)
            {
                Destroy();
                return;
            }

            ticksUntilNextRing = ringIntervalTicks;
        }

        private void EmitRing(int ringIndex)
        {
            if (!originCell.ShouldSpawnMotesAt(MapHeld))
            {
                return;
            }

            if (cachedRingFleck == null)
            {
                cachedRingFleck = DefDatabase<FleckDef>.GetNamedSilentFail(ringFleckDefName);
            }

            if (cachedRingFleck == null)
            {
                return;
            }

            if (ringIndex <= 0)
            {
                FleckCreationData centerData = FleckMaker.GetDataStatic(exactPosition, MapHeld, cachedRingFleck, visualScale * 1.35f);
                centerData.rotationRate = Rand.Range(-18f, 18f);
                centerData.velocitySpeed = 0f;
                MapHeld.flecks.CreateFleck(centerData);
                return;
            }

            float bandMin = ringIndex - 0.7f;
            float bandMax = ringIndex + 0.5f;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(originCell, ringIndex + 0.45f, true))
            {
                if (!cell.InBounds(MapHeld) || !cell.ShouldSpawnMotesAt(MapHeld))
                {
                    continue;
                }

                float distance = cell.DistanceTo(originCell);
                if (distance < bandMin || distance > bandMax)
                {
                    continue;
                }

                Vector3 pos = cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.14f, 0.14f), 0f, Rand.Range(-0.14f, 0.14f));
                float angle = (cell - originCell).AngleFlat;
                FleckCreationData data = FleckMaker.GetDataStatic(pos, MapHeld, cachedRingFleck, visualScale * Rand.Range(0.95f, 1.25f));
                data.rotation = angle;
                data.rotationRate = Rand.Range(-40f, 40f);
                data.velocityAngle = angle;
                data.velocitySpeed = Rand.Range(0.12f, 0.28f);
                MapHeld.flecks.CreateFleck(data);

                if (Rand.Chance(0.28f))
                {
                    FleckMaker.ThrowDustPuffThick(pos, MapHeld, Rand.Range(0.75f, 1.25f), Color.gray);
                }
            }
        }
    }
}
