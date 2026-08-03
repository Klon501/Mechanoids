using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_JavelinMissile : DefModExtension
    {
        /// <summary>How hard the missile can pull once its motor has settled, in degrees per tick.</summary>
        public float turnDegreesPerTick = 6f;

        /// <summary>Ticks the missile holds its launch heading before guidance engages.</summary>
        public int boostTicks = 12;

        /// <summary>Motor burn time. The missile is spent once this runs out.</summary>
        public int lifetimeTicks = 120;

        /// <summary>How close the missile has to get to the target to detonate on it.</summary>
        public float hitRadius = 0.5f;

        /// <summary>
        /// Range at which the missile counts as committed to its attack run. Once it has been this
        /// close and the range opens again, guidance shuts down instead of letting the missile come
        /// round for a second pass at a target it already overflew.
        /// </summary>
        public float terminalRadius = 3f;

        /// <summary>Extra fraction of base damage added for each previous hit on the same target.</summary>
        public float damageMultiplierPerStack = 0.25f;

        /// <summary>Ceiling on the escalating damage multiplier.</summary>
        public float maxDamageMultiplier = 2.5f;

        /// <summary>Hediff carrying the per-target hit count. Left null disables escalation.</summary>
        public HediffDef targetStackHediff;

        /// <summary>Damage the warhead blast deals to everything caught in it.</summary>
        public int blastDamageAmount = 14;

        /// <summary>Armour penetration for the blast, separate from the direct hit.</summary>
        public float blastArmorPenetration = 0.3f;

        /// <summary>Blast damage type. Falls back to the projectile's own damage def.</summary>
        public DamageDef blastDamageDef;

        /// <summary>
        /// Whether the pawn the missile actually struck also takes the blast. Off by default: that
        /// target already took the full escalating direct hit, so including it would charge the
        /// same missile twice and blur where the escalation shows up.
        /// </summary>
        public bool blastHitsPrimaryTarget;
    }

    /// <summary>
    /// A homing missile that leaves the launcher along the mech's cardinal facing, holds that
    /// heading through a short boost phase, then steers onto the target at a capped turn rate.
    ///
    /// Steering works by rewriting the base projectile's <c>origin</c>, <c>destination</c> and
    /// <c>ticksToImpact</c> each tick so the vanilla mover walks exactly the path the guidance
    /// produced. That keeps free-intercept collision, shield blocking and impact handling on the
    /// vanilla code path instead of reimplementing them.
    ///
    /// This derives from <see cref="Bullet"/> rather than <see cref="Projectile_Explosive"/> on
    /// purpose. <c>VerbProperties.CausesExplosion</c> treats any Projectile_Explosive subclass as an
    /// area weapon and then requires a matching <c>forcedMissRadius</c>, which scatters the aim
    /// point - the opposite of a guided missile. Landing as a single aimed hit also keeps the
    /// escalating damage attributable to one target.
    /// </summary>
    public class Projectile_JavelinMissile : Bullet
    {
        /// <summary>
        /// How far past the next step the destination is projected. Any value above one keeps
        /// <c>ticksToImpact</c> positive so the base mover never treats a steering update as an
        /// arrival, while the interpolation still lands on the exact guided position.
        /// </summary>
        private const int PathStretch = 8;

        /// <summary>
        /// How far behind the missile the interpolation origin is placed each tick.
        ///
        /// Vanilla gates every free-intercept collision on
        /// <c>VerbUtility.InterceptChanceFactorFromDistance(origin, cell)</c>, which returns zero
        /// inside 5 tiles of <c>origin</c> and reaches full strength at 12. Steering by rewriting
        /// <c>origin</c> to the missile's current position each tick therefore silently disabled
        /// collision entirely: the missile flew through walls, cover and bystanders and could only
        /// ever hit its intended target. Setting the origin back along the current heading keeps
        /// the position interpolation exactly the same - the missile still sits on the segment -
        /// while putting the intercept distance back into a range where vanilla collision runs.
        /// </summary>
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

        /// <summary>
        /// The missile always leaves along one of the four cardinal headings so it exits the
        /// launcher tube at the angle the sprite is actually drawn at. The shooter's own rotation
        /// is used rather than a recomputed bearing, so the missile matches what is on screen even
        /// if the pawn's facing lags the target by a tick.
        /// </summary>
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

        /// <summary>
        /// Detonates the warhead. This runs for every impact, including the missile burning out or
        /// losing its target, so a spent missile visibly explodes instead of quietly winking out.
        ///
        /// The blast is raised here rather than by deriving from <see cref="Projectile_Explosive"/>
        /// or adding <c>CompProperties_Explosive</c>, because either of those makes
        /// <c>VerbProperties.CausesExplosion</c> true and forces a scattered aim point on the verb.
        /// </summary>
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

        /// <summary>
        /// Reads the target's current hit count, then records this hit. The multiplier returned is
        /// the one for the hit being resolved now, so the first missile always lands for base
        /// damage and the escalation shows up from the second hit onward.
        /// </summary>
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
