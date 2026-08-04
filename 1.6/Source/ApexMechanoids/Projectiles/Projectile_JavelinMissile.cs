using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_JavelinMissile : DefModExtension
    {
        public float turnDegreesPerTick = 6f;
        public int boostTicks = 12;
        public int lifetimeTicks = 120;
        public float hitRadius = 0.5f;

        // Once the missile has been this close and the range opens again, guidance shuts down
        // instead of letting it come round for a second pass at a target it already overflew.
        public float terminalRadius = 3f;

        public float damageMultiplierPerStack = 0.25f;
        public float maxDamageMultiplier = 2.5f;

        // Leave null to disable the escalating damage entirely.
        public HediffDef targetStackHediff;

        public int blastDamageAmount = 14;
        public float blastArmorPenetration = 0.3f;

        // Falls back to the projectile's own damage def when null.
        public DamageDef blastDamageDef;

        // Off by default: the struck pawn already took the full escalating direct hit, so including
        // it in the blast would charge the same missile twice.
        public bool blastHitsPrimaryTarget;
    }

    /// <summary>
    /// A homing missile that leaves the launcher along the mech's cardinal facing, holds that
    /// heading through a short boost phase, then steers onto the target at a capped turn rate.
    /// Steering rewrites the base projectile's origin, destination and ticksToImpact each tick so
    /// collision, shield blocking and impact all stay on the vanilla code path.
    /// </summary>
    // Derives from Bullet rather than Projectile_Explosive on purpose: VerbProperties.CausesExplosion
    // treats any Projectile_Explosive subclass as an area weapon and then requires a matching
    // forcedMissRadius, which scatters the aim point and cannot coexist with a guided missile. The
    // warhead is detonated from Impact instead.
    public class Projectile_JavelinMissile : Bullet
    {
        // Any value above one keeps ticksToImpact positive, so the base mover never mistakes a
        // steering update for an arrival while the interpolation still lands on the guided position.
        private const int PathStretch = 8;

        // Vanilla gates free-intercept collision on InterceptChanceFactorFromDistance(origin, cell),
        // which returns zero inside 5 tiles of origin and reaches full strength at 12. Rewriting
        // origin to the missile's current position each tick therefore disabled collision outright:
        // the missile flew through walls, cover and bystanders and could only hit its intended
        // target. Setting origin back along the heading leaves the interpolation identical, since
        // the missile still sits on the segment, while restoring a live intercept distance.
        private const float InterceptOriginBackset = 13f;

        private JavelinFlightState flight;
        private bool flightInitialized;
        private float pendingDamageMultiplier = 1f;

        private DefModExtension_JavelinMissile Props => def.GetModExtension<DefModExtension_JavelinMissile>();

        private JavelinFlightParams FlightParams
        {
            get
            {
                DefModExtension_JavelinMissile props = Props;
                return new JavelinFlightParams
                {
                    speedPerTick = def.projectile.SpeedTilesPerTick,
                    maxTurnPerTick = props.turnDegreesPerTick * Mathf.Deg2Rad,
                    boostTicks = props.boostTicks,
                    hitRadius = props.hitRadius,
                    terminalRadius = props.terminalRadius,
                    lifetimeTicks = props.lifetimeTicks
                };
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire, Thing equipment, ThingDef targetCoverDef)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            if (Props == null)
            {
                return;
            }

            flight = JavelinMissileGuidance.CreateState(origin.x, origin.z, ResolveLaunchHeading(launcher, origin));
            flightInitialized = true;
        }

        // Uses the shooter's own rotation rather than a recomputed bearing, so the missile leaves at
        // the angle the sprite is actually drawn at even if the pawn's facing lags by a tick.
        private float ResolveLaunchHeading(Thing launcher, Vector3 origin)
        {
            if (launcher is Pawn pawn)
            {
                return JavelinMissileGuidance.CardinalHeading(pawn.Rotation.AsInt);
            }

            Vector3 toTarget = (destination - origin).Yto0();
            return toTarget.sqrMagnitude < 0.0001f
                ? JavelinMissileGuidance.CardinalHeading(Rot4.North.AsInt)
                : JavelinMissileGuidance.CardinalHeading(Rot4.FromAngleFlat(toTarget.AngleFlat()).AsInt);
        }

        private Vector3 CurrentTargetPosition()
        {
            Thing target = intendedTarget.Thing ?? usedTarget.Thing;
            if (target != null && target.Spawned)
            {
                return target.DrawPos;
            }

            return usedTarget.IsValid ? usedTarget.Cell.ToVector3Shifted() : destination;
        }

        public override void TickInterval(int delta)
        {
            if (!flightInitialized || landed)
            {
                base.TickInterval(delta);
                return;
            }

            JavelinFlightParams flightParams = FlightParams;
            Vector3 targetPosition = CurrentTargetPosition();
            Vector3 previousPosition = new Vector3(flight.x, 0f, flight.z);

            for (int i = 0; i < delta; i++)
            {
                flight = JavelinMissileGuidance.Step(flight, targetPosition.x, targetPosition.z, flightParams);
            }

            Vector3 newPosition = new Vector3(flight.x, 0f, flight.z);
            Vector3 step = newPosition - previousPosition;

            if (step.sqrMagnitude < 0.000001f)
            {
                base.TickInterval(delta);
                return;
            }

            // Hand the vanilla mover a straight segment that interpolates onto the guided position
            // after it subtracts delta, so collision and impact stay on the base code path. The
            // segment starts behind the missile so vanilla's intercept-distance gate stays live;
            // ticksToImpact is unaffected by the backset, because the extra length behind the
            // missile is exactly the distance already covered.
            Vector3 heading = step.normalized;
            origin = previousPosition - heading * InterceptOriginBackset;
            destination = previousPosition + step * PathStretch;
            ticksToImpact = delta * PathStretch;

            base.TickInterval(delta);

            if (Destroyed || !Spawned)
            {
                return;
            }

            if (JavelinMissileGuidance.HasReachedTarget(flight, targetPosition.x, targetPosition.z, flightParams))
            {
                Position = newPosition.ToIntVec3();
                Impact(intendedTarget.Thing ?? usedTarget.Thing);
                return;
            }

            if (JavelinMissileGuidance.IsExpired(flight, flightParams))
            {
                // Out of fuel short of the target: detonate where it died rather than vanishing,
                // so an overflown shot still reads as a missile that went past and blew up.
                Impact(null);
            }
        }

        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // Captured before the base call, which destroys the projectile and clears its map.
            Map map = Map;
            IntVec3 blastCell = hitThing != null && hitThing.Spawned ? hitThing.Position : Position;

            if (!blockedByShield)
            {
                pendingDamageMultiplier = ResolveAndAdvanceStack(hitThing);
            }

            base.Impact(hitThing, blockedByShield);
            TryDetonate(map, blastCell, hitThing);
        }

        // Runs for every impact, including burning out or losing the target, so a spent missile
        // visibly explodes instead of quietly winking out.
        private void TryDetonate(Map map, IntVec3 cell, Thing hitThing)
        {
            DefModExtension_JavelinMissile props = Props;
            float radius = def.projectile.explosionRadius;
            if (map == null || props == null || radius <= 0f || !cell.InBounds(map))
            {
                return;
            }

            List<Thing> ignoredThings = null;
            if (!props.blastHitsPrimaryTarget && hitThing != null)
            {
                ignoredThings = new List<Thing> { hitThing };
            }

            GenExplosion.DoExplosion(
                cell,
                map,
                radius,
                props.blastDamageDef ?? def.projectile.damageDef,
                launcher,
                props.blastDamageAmount,
                props.blastArmorPenetration,
                def.projectile.soundExplode,
                equipmentDef,
                def,
                intendedTarget.Thing,
                damageFalloff: true,
                ignoredThings: ignoredThings);
        }

        // Returns the multiplier for the hit being resolved now, then records it, so the first
        // missile lands for base damage and the escalation shows from the second hit onward.
        private float ResolveAndAdvanceStack(Thing hitThing)
        {
            DefModExtension_JavelinMissile props = Props;
            if (props?.targetStackHediff == null)
            {
                return 1f;
            }

            Pawn target = (hitThing ?? intendedTarget.Thing) as Pawn;
            if (target == null || target.Dead || target.health == null)
            {
                return 1f;
            }

            Hediff_JavelinMissileLock hediff = target.health.hediffSet.GetFirstHediffOfDef(props.targetStackHediff) as Hediff_JavelinMissileLock;
            if (hediff == null)
            {
                // First missile to land on this target. The new hediff is created already holding
                // this hit, so it must not be registered again here.
                hediff = (Hediff_JavelinMissileLock)HediffMaker.MakeHediff(props.targetStackHediff, target);
                target.health.AddHediff(hediff);
                return JavelinMissileGuidance.DamageMultiplier(0, props.damageMultiplierPerStack, props.maxDamageMultiplier);
            }

            float multiplier = JavelinMissileGuidance.DamageMultiplier(hediff.Stacks, props.damageMultiplierPerStack, props.maxDamageMultiplier);
            hediff.RegisterHit();
            return multiplier;
        }

        public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * pendingDamageMultiplier);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref flightInitialized, nameof(flightInitialized));
            Scribe_Values.Look(ref pendingDamageMultiplier, nameof(pendingDamageMultiplier), 1f);
            Scribe_Values.Look(ref flight.x, "flightX");
            Scribe_Values.Look(ref flight.z, "flightZ");
            Scribe_Values.Look(ref flight.heading, "flightHeading");
            Scribe_Values.Look(ref flight.ticksFlown, "flightTicksFlown");
            Scribe_Values.Look(ref flight.hasClosed, "flightHasClosed");
            Scribe_Values.Look(ref flight.guidanceLockedOut, "flightGuidanceLockedOut");
        }
    }
}
