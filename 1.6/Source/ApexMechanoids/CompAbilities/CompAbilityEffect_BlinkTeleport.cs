using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public static class BlinkVisualUtility
    {
        private const float DefaultMoteAlpha = 0.8f;
        private const float DarkFragmentAlpha = 0.8f;

        public static void SpawnWarmupStart(Pawn caster, CompProperties_BlinkWarmupVisuals props)
        {
            if (!CanSpawnAt(caster))
            {
                return;
            }

            Vector3 center = caster.DrawPos;
            SpawnStaticMote(caster.MapHeld, props.ringMoteDef, center, Rand.Range(0.66f, 0.82f), Rand.Range(0f, 360f));
            SpawnStaticMote(caster.MapHeld, props.sparkleMoteDef, center + RandomFlatOffset(0.04f, 0.16f), Rand.Range(0.74f, 0.96f), Rand.Range(0f, 360f));
            SpawnStaticMote(caster.MapHeld, props.sparkleMoteDef, center + RandomFlatOffset(0.04f, 0.18f), Rand.Range(0.68f, 0.9f), Rand.Range(0f, 360f));
        }

        public static void SpawnWarmupPulse(Pawn caster, CompProperties_BlinkWarmupVisuals props)
        {
            if (!CanSpawnAt(caster))
            {
                return;
            }

            Vector3 center = caster.DrawPos;
            SpawnStaticMote(caster.MapHeld, props.sparkleMoteDef, center + RandomFlatOffset(0.05f, 0.22f), Rand.Range(0.62f, 0.84f), Rand.Range(0f, 360f));
            for (int i = 0; i < props.sparklesPerPulse - 1; i++)
            {
                SpawnStaticMote(caster.MapHeld, props.sparkleMoteDef, center + RandomFlatOffset(0.04f, 0.2f), Rand.Range(0.56f, 0.78f), Rand.Range(0f, 360f));
            }
        }

        public static void SpawnDepartureBurst(Map map, IntVec3 cell, CompProperties_BlinkTeleport props)
        {
            if (!CanSpawnAt(map, cell))
            {
                return;
            }

            Vector3 center = cell.ToVector3Shifted();
            SpawnStaticMote(map, props.ringMoteDef, center, Rand.Range(1.18f, 1.34f), Rand.Range(0f, 360f));
            SpawnStaticMote(map, props.starAMoteDef, center + RandomFlatOffset(0.02f, 0.16f), Rand.Range(1.04f, 1.3f), Rand.Range(0f, 360f));
            SpawnStaticMote(map, props.starBMoteDef, center + RandomFlatOffset(0.02f, 0.18f), Rand.Range(0.96f, 1.22f), Rand.Range(0f, 360f));

            for (int i = 0; i < 4; i++)
            {
                SpawnStaticMote(map, props.sparkleMoteDef, center + RandomFlatOffset(0.03f, 0.3f), Rand.Range(0.84f, 1.18f), Rand.Range(0f, 360f));
            }

            int fragmentCount = props.darkFragmentCount.RandomInRange;
            for (int i = 0; i < fragmentCount; i++)
            {
                float angle = Rand.Range(0f, 360f);
                SpawnStaticMote(map, props.darkFragmentMoteDefs.RandomElement(), center + FlatOffset(angle, Rand.Range(0.03f, 0.12f)), 1f, Rand.Range(0f, 360f), DarkFragmentAlpha);
            }
        }

        public static void SpawnArrivalBurst(Map map, IntVec3 cell, CompProperties_BlinkTeleport props)
        {
            if (!CanSpawnAt(map, cell))
            {
                return;
            }

            Vector3 center = cell.ToVector3Shifted();
            SpawnStaticMote(map, props.arrivalPillarMoteDef, center, Rand.Range(1.24f, 1.5f), 0f);
            SpawnStaticMote(map, props.arrivalRayMoteDef, center + RandomFlatOffset(0.02f, 0.08f), Rand.Range(1.16f, 1.34f), Rand.Range(-8f, 8f));
            SpawnStaticMote(map, props.arrivalRayMoteDef, center + RandomFlatOffset(0.02f, 0.08f), Rand.Range(1.1f, 1.28f), Rand.Range(172f, 188f));
            SpawnStaticMote(map, props.ringMoteDef, center, Rand.Range(1.08f, 1.24f), Rand.Range(0f, 360f));
            SpawnStaticMote(map, props.starAMoteDef, center + RandomFlatOffset(0.01f, 0.12f), Rand.Range(1.06f, 1.24f), Rand.Range(0f, 360f));
            SpawnStaticMote(map, props.starBMoteDef, center + RandomFlatOffset(0.01f, 0.12f), Rand.Range(1.0f, 1.18f), Rand.Range(0f, 360f));

            for (int i = 0; i < 6; i++)
            {
                SpawnStaticMote(map, props.sparkleMoteDef, center + RandomFlatOffset(0.02f, 0.28f), Rand.Range(0.84f, 1.18f), Rand.Range(0f, 360f));
            }
        }

        private static bool CanSpawnAt(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.MapHeld != null && CanSpawnAt(pawn.MapHeld, pawn.PositionHeld);
        }

        private static bool CanSpawnAt(Map map, IntVec3 cell)
        {
            return map != null && cell.InBounds(map) && cell.ShouldSpawnMotesAt(map);
        }

        private static Mote SpawnStaticMote(Map map, ThingDef moteDef, Vector3 position, float scale, float rotation, float alpha = DefaultMoteAlpha)
        {
            if (map == null || moteDef == null)
            {
                return null;
            }

            IntVec3 cell = position.ToIntVec3();
            if (!CanSpawnAt(map, cell))
            {
                return null;
            }

            Mote mote = MoteMaker.MakeStaticMote(position, map, moteDef, scale);
            if (mote != null)
            {
                mote.exactRotation = rotation;
                mote.instanceColor = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }

            return mote;
        }

        private static Vector3 RandomFlatOffset(float minRadius, float maxRadius)
        {
            float angle = Rand.Range(0f, 360f);
            float radius = Rand.Range(minRadius, maxRadius);
            return FlatOffset(angle, radius);
        }

        private static Vector3 FlatOffset(float angle, float radius)
        {
            Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            return radial * radius;
        }
    }

    public class CompAbility_BlinkWarmupVisuals : AbilityComp
    {
        private bool castingLastTick;
        private int ticksUntilNextPulse;

        public CompProperties_BlinkWarmupVisuals Props => (CompProperties_BlinkWarmupVisuals)props;

        public override void CompTick()
        {
            base.CompTick();

            if (parent?.pawn == null || !parent.pawn.Spawned || parent.pawn.MapHeld == null || !parent.Casting)
            {
                castingLastTick = false;
                ticksUntilNextPulse = 0;
                return;
            }

            if (!castingLastTick)
            {
                castingLastTick = true;
                ticksUntilNextPulse = Mathf.Max(Props.spawnIntervalTicks, 1);
                BlinkVisualUtility.SpawnWarmupStart(parent.pawn, Props);
                return;
            }

            if (ticksUntilNextPulse > 0)
            {
                ticksUntilNextPulse--;
                return;
            }

            BlinkVisualUtility.SpawnWarmupPulse(parent.pawn, Props);
            ticksUntilNextPulse = Mathf.Max(Props.spawnIntervalTicks, 1);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref castingLastTick, nameof(castingLastTick));
            Scribe_Values.Look(ref ticksUntilNextPulse, nameof(ticksUntilNextPulse));
        }
    }

    public class CompProperties_BlinkWarmupVisuals : AbilityCompProperties
    {
        public CompProperties_BlinkWarmupVisuals()
        {
            compClass = typeof(CompAbility_BlinkWarmupVisuals);
        }

        public ThingDef pillarMoteDef;
        public ThingDef ringMoteDef;
        public ThingDef rayMoteDef;
        public ThingDef sparkleMoteDef;
        public int spawnIntervalTicks = 4;
        public int sparklesPerPulse = 2;
    }

    public class CompAbilityEffect_BlinkTeleport : CompAbilityEffect
    {
        public new CompProperties_BlinkTeleport Props => (CompProperties_BlinkTeleport)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (caster == null || map == null || !caster.Spawned || !target.IsValid)
            {
                return;
            }

            if (!IsValidDestination(caster, target, out _))
            {
                return;
            }

            IntVec3 destinationCell = target.Cell;
            IntVec3 originCell = caster.PositionHeld;
            caster.TryGetComp<CompCanBeDormant>()?.WakeUp();
            BlinkVisualUtility.SpawnDepartureBurst(map, originCell, Props);

            caster.Position = destinationCell;
            if ((caster.Faction == Faction.OfPlayer || caster.IsPlayerControlled) && caster.Position.Fogged(caster.Map))
            {
                FloodFillerFog.FloodUnfog(caster.Position, caster.Map);
            }

            if (Props.stunTicks.max > 0)
            {
                caster.stances?.stunner?.StunFor(Props.stunTicks.RandomInRange, parent.pawn, addBattleLog: false, showMote: false);
            }

            caster.Notify_Teleported();
            CompAbilityEffect_Teleport.SendSkipUsedSignal(caster.Position, caster);

            BlinkVisualUtility.SpawnArrivalBurst(map, destinationCell, Props);

            if (Props.clamorType != null)
            {
                GenClamor.DoClamor(parent.pawn, originCell, Props.clamorRadius, Props.clamorType);
            }

            if (Props.destClamorType != null)
            {
                GenClamor.DoClamor(parent.pawn, destinationCell, Props.destClamorRadius, Props.destClamorType);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return false;
            }

            if (!target.IsValid || !target.Cell.InBounds(caster.Map))
            {
                return false;
            }

            if (!IsValidDestination(caster, target, out string failureReason))
            {
                if (throwMessages && !failureReason.NullOrEmpty())
                {
                    Messages.Message(failureReason, caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            return caster != null && caster.Map != null && IsValidDestination(caster, target, out _);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null)
            {
                return null;
            }

            return IsValidDestination(caster, target, out string failureReason) ? null : failureReason;
        }

        private bool IsValidDestination(Pawn caster, LocalTargetInfo target, out string failureReason)
        {
            failureReason = null;
            Map map = caster.Map;
            IntVec3 cell = target.Cell;

            if (!cell.InBounds(map))
            {
                failureReason = "OutOfBounds".Translate();
                return false;
            }

            if (Props.range > 0f && cell.DistanceTo(caster.PositionHeld) > Props.range)
            {
                failureReason = "OutOfRange".Translate();
                return false;
            }

            if (Props.requiresLineOfSight && !GenSight.LineOfSight(caster.PositionHeld, cell, map))
            {
                failureReason = "AbilityNoLineOfSight".Translate();
                return false;
            }

            Building_Door door = cell.GetDoor(map);
            if (door != null && !door.CanPhysicallyPass(caster))
            {
                failureReason = "CannotUseAbility".Translate();
                return false;
            }

            if (cell.Impassable(map) || !cell.WalkableBy(map, caster))
            {
                failureReason = "CannotUseAbility".Translate();
                return false;
            }

            if (!CompAbilityEffect_WithDest.CanTeleportThingTo(target, map))
            {
                failureReason = "CannotUseAbility".Translate();
                return false;
            }

            return true;
        }
    }

    public class CompProperties_BlinkTeleport : CompProperties_AbilityTeleport
    {
        public CompProperties_BlinkTeleport()
        {
            compClass = typeof(CompAbilityEffect_BlinkTeleport);
        }

        public ThingDef ringMoteDef;
        public ThingDef starAMoteDef;
        public ThingDef starBMoteDef;
        public ThingDef sparkleMoteDef;
        public ThingDef arrivalPillarMoteDef;
        public ThingDef arrivalRayMoteDef;
        public List<ThingDef> darkFragmentMoteDefs = new List<ThingDef>();
        public IntRange darkFragmentCount = new IntRange(1, 1);
    }
}
