using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ApexMechanoids
{
    public class Ability_PulseWave : Ability
    {
        public Ability_PulseWave()
        {
        }

        public Ability_PulseWave(Pawn pawn)
            : base(pawn)
        {
        }

        public Ability_PulseWave(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public Ability_PulseWave(Pawn pawn, Precept sourcePrecept, AbilityDef def)
            : base(pawn, sourcePrecept, def)
        {
        }

        public override Job GetJob(LocalTargetInfo target, LocalTargetInfo destination)
        {
            LocalTargetInfo selfTarget = pawn != null ? new LocalTargetInfo(pawn.Position) : target;
            return base.GetJob(selfTarget, selfTarget);
        }
    }

    public class Verb_CastPulseWave : Verb_CastAbility
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (CasterIsPawn)
            {
                LocalTargetInfo selfTarget = new LocalTargetInfo(CasterPawn.Position);
                Job currentJob = CasterPawn.CurJob;
                if (currentJob?.ability == ability)
                {
                    currentJob.targetA = selfTarget;
                    currentJob.targetB = selfTarget;
                }

                return base.TryStartCastOn(selfTarget, selfTarget, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            }

            return base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            if (IsSelfCenteredTarget(targ))
            {
                return true;
            }

            return base.CanHitTarget(targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            if (IsSelfCenteredTarget(targ))
            {
                return true;
            }

            return base.CanHitTargetFrom(root, targ);
        }

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            PulseWaveUtility.TryApply(CasterPawn, PulseWaveUtility.GetProps(ability?.def));
        }

        private bool IsSelfCenteredTarget(LocalTargetInfo target)
        {
            return CasterIsPawn
                && target.IsValid
                && (target.Cell == CasterPawn.Position || target.Thing == CasterPawn);
        }
    }

    public class CompAbilityEffect_PulseWave : CompAbilityEffect
    {
        public new CompProperties_PulseWave Props => (CompProperties_PulseWave)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            PulseWaveUtility.TryApply(parent.pawn, Props);
        }

        // Manual player casts are never blocked by target availability.
        // The hostile-in-radius gate lives in AICanTargetNow, which only the AI paths use.
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return target.Pawn == null || base.CanApplyOn(target, dest);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return PulseWaveUtility.HasHostileAffectedPawnInRadius(parent?.pawn, Props);
        }
    }

    internal static class PulseWaveUtility
    {
        private static int lastCasterThingId = -1;
        private static int lastApplyTick = -1;

        public static CompProperties_PulseWave GetProps(AbilityDef abilityDef)
        {
            if (abilityDef?.comps == null)
            {
                return null;
            }

            for (int i = 0; i < abilityDef.comps.Count; i++)
            {
                if (abilityDef.comps[i] is CompProperties_PulseWave pulseProps)
                {
                    return pulseProps;
                }
            }

            return null;
        }

        public static void TryApply(Pawn caster, CompProperties_PulseWave props)
        {
            Map map = caster?.MapHeld;
            if (caster == null || props == null || map == null || !caster.Spawned)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (lastApplyTick == currentTick && lastCasterThingId == caster.thingIDNumber)
            {
                return;
            }

            lastApplyTick = currentTick;
            lastCasterThingId = caster.thingIDNumber;

            SpawnCasterFlash(caster, map, props);
            SpawnEmitter(caster, map, props);
            PlayExplicitCastSound(caster, map, props);
        }

        public static bool HasHostileAffectedPawnInRadius(Pawn caster, CompProperties_PulseWave props)
        {
            Map map = caster?.MapHeld;
            if (caster == null || props == null || map == null || caster.Dead || caster.Downed || !caster.Spawned || !caster.Awake())
            {
                return false;
            }

            float radius = props.radius;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (IsValidHostileAffectedPawn(caster, pawn) && pawn.Position.DistanceTo(caster.Position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidHostileAffectedPawn(Pawn caster, Pawn pawn)
        {
            return IsAffectedPawn(caster, pawn)
                && !pawn.Downed
                && !(pawn.ParentHolder is PawnFlyer)
                && !pawn.IsPsychologicallyInvisible()
                && pawn.HostileTo(caster);
        }

        public static bool IsAffectedPawn(Pawn caster, Pawn pawn)
        {
            if (caster == null || pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned || pawn.MapHeld != caster.MapHeld)
            {
                return false;
            }

            if (!(pawn.RaceProps?.IsFlesh ?? false))
            {
                return false;
            }

            return pawn.Faction != Faction.OfMechanoids;
        }

        private static void SpawnCasterFlash(Pawn caster, Map map, CompProperties_PulseWave props)
        {
            if (props.blindFlashThingDef == null || !caster.PositionHeld.InBounds(map))
            {
                return;
            }

            Thing flashThing = ThingMaker.MakeThing(props.blindFlashThingDef);
            GenSpawn.Spawn(flashThing, caster.PositionHeld, map, WipeMode.Vanish);
        }

        private static void SpawnEmitter(Pawn caster, Map map, CompProperties_PulseWave props)
        {
            if (props.emitterDef == null)
            {
                return;
            }

            Mote_PulseWaveEmitter emitter = ThingMaker.MakeThing(props.emitterDef) as Mote_PulseWaveEmitter;
            if (emitter == null)
            {
                return;
            }

            emitter.Initialize(caster, props);
            GenSpawn.Spawn(emitter, caster.PositionHeld, map);
        }

        private static void PlayExplicitCastSound(Pawn caster, Map map, CompProperties_PulseWave props)
        {
            if (props.castSoundDefName.NullOrEmpty())
            {
                return;
            }

            SoundDef castSound = DefDatabase<SoundDef>.GetNamedSilentFail(props.castSoundDefName);
            castSound?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
        }
    }

    public class CompProperties_PulseWave : CompProperties_AbilityEffect
    {
        public CompProperties_PulseWave()
        {
            compClass = typeof(CompAbilityEffect_PulseWave);
        }

        public ThingDef emitterDef;
        public ThingDef blindFlashThingDef;
        public HediffDef blindHediffDef;
        public float radius = 6.9f;
        public int ringIntervalTicks = 2;
        public int stunTicks = 60;
        public int blindTicks = 480;
        public float visualScale = 0.72f;
        public string fleckDefName = "PsycastPsychicEffect";
        public string castSoundDefName;
    }

    public class Mote_PulseWaveEmitter : Mote
    {
        private Pawn caster;
        private HediffDef blindHediffDef;
        private float radius;
        private int ringIntervalTicks;
        private int stunTicks;
        private int blindTicks;
        private float visualScale;
        private string fleckDefName;
        private int ticksUntilNextRing;
        private int currentRing;
        private int maxRing;
        private readonly HashSet<int> affectedPawnIds = new HashSet<int>();
        private FleckDef cachedFleckDef;

        public void Initialize(Pawn caster, CompProperties_PulseWave props)
        {
            this.caster = caster;
            blindHediffDef = props.blindHediffDef;
            radius = props.radius;
            ringIntervalTicks = Mathf.Max(props.ringIntervalTicks, 1);
            stunTicks = Mathf.Max(props.stunTicks, 1);
            blindTicks = Mathf.Max(props.blindTicks, 1);
            visualScale = props.visualScale;
            fleckDefName = props.fleckDefName;
            currentRing = 0;
            maxRing = Mathf.CeilToInt(radius);
            ticksUntilNextRing = 0;
            exactPosition = caster.DrawPos;
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
            SpawnRingFlecks(ringIndex);
            AffectNewPawns(ringIndex);
        }

        private void SpawnRingFlecks(int ringIndex)
        {
            if (!Position.ShouldSpawnMotesAt(MapHeld))
            {
                return;
            }

            if (cachedFleckDef == null)
            {
                cachedFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(fleckDefName);
            }

            FleckDef fleckDef = cachedFleckDef;
            if (fleckDef == null)
            {
                return;
            }

            if (ringIndex <= 0)
            {
                FleckCreationData centerData = FleckMaker.GetDataStatic(DrawPos, MapHeld, fleckDef, visualScale * 1.05f);
                centerData.rotationRate = Rand.Range(-18f, 18f);
                centerData.velocitySpeed = 0f;
                MapHeld.flecks.CreateFleck(centerData);
                return;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, ringIndex + 0.35f, true))
            {
                if (!cell.InBounds(MapHeld) || !cell.ShouldSpawnMotesAt(MapHeld))
                {
                    continue;
                }

                float distance = cell.DistanceTo(Position);
                if (distance < ringIndex - 0.7f || distance > ringIndex + 0.45f)
                {
                    continue;
                }

                Vector3 pos = cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.18f, 0.18f), 0f, Rand.Range(-0.18f, 0.18f));
                float outwardAngle = (cell - Position).AngleFlat;
                FleckCreationData data = FleckMaker.GetDataStatic(pos, MapHeld, fleckDef, visualScale * Rand.Range(0.88f, 1.18f));
                data.rotation = outwardAngle;
                data.rotationRate = Rand.Range(-32f, 32f);
                data.velocityAngle = outwardAngle;
                data.velocitySpeed = Rand.Range(0.05f, 0.14f);
                MapHeld.flecks.CreateFleck(data);
            }
        }

        private void AffectNewPawns(int ringIndex)
        {
            if (caster == null)
            {
                return;
            }

            float ringRadius = ringIndex + 0.35f;
            IReadOnlyList<Pawn> pawns = MapHeld.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!ShouldAffectPawn(pawn) || affectedPawnIds.Contains(pawn.thingIDNumber))
                {
                    continue;
                }

                if (pawn.Position.DistanceTo(Position) > ringRadius)
                {
                    continue;
                }

                affectedPawnIds.Add(pawn.thingIDNumber);
                ApplyWaveToPawn(pawn);
            }
        }

        private bool ShouldAffectPawn(Pawn pawn)
        {
            return PulseWaveUtility.IsAffectedPawn(caster, pawn);
        }

        private void ApplyWaveToPawn(Pawn pawn)
        {
            float stunSeconds = stunTicks / 60f;
            AbilityDef vanillaStun = DefDatabase<AbilityDef>.GetNamedSilentFail("Stun");
            if (vanillaStun != null)
            {
                stunSeconds = vanillaStun.GetStatValueAbstract(StatDefOf.Ability_Duration, caster);
                stunSeconds *= pawn.GetStatValue(StatDefOf.PsychicSensitivity);
                stunSeconds *= 2f;
            }

            if (pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(stunSeconds.SecondsToTicks(), caster, addBattleLog: false);
            }

            if (blindHediffDef != null && pawn.health != null)
            {
                Hediff existing = pawn.health.hediffSet?.GetFirstHediffOfDef(blindHediffDef);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                Hediff blind = HediffMaker.MakeHediff(blindHediffDef, pawn);
                pawn.health.AddHediff(blind);
            }

            if (cachedFleckDef != null && pawn.Position.ShouldSpawnMotesAt(MapHeld))
            {
                FleckCreationData data = FleckMaker.GetDataStatic(pawn.DrawPos, MapHeld, cachedFleckDef, visualScale * 0.95f);
                data.rotationRate = Rand.Range(-16f, 16f);
                data.velocitySpeed = 0f;
                MapHeld.flecks.CreateFleck(data);
            }
        }
    }
}
