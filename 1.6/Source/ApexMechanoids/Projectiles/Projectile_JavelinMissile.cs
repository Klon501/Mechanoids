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

        // The missile flies over pawns, chunks and low cover for the whole flight and is stopped only
        // by something that fills its cell outright. See JavelinObstacleRules for why vanilla's own
        // free-intercept roll is the wrong shape for a guided weapon.
        public bool solidObstaclesBlock = true;

        // Grace distance out of the tube before a solid obstacle can stop the missile, so a launcher
        // standing next to its own wall does not detonate the shot on the doorframe.
        public float solidObstacleArmTiles = 5f;

        // Aim preview. During the warmup the launcher draws the path the missile will actually fly,
        // simulated from the same guidance the projectile uses, because a missile that leaves
        // sideways and curves back reads as a malfunction if the player cannot see where it is going.
        public bool drawAimPreview = true;

        // One drawn segment per this many flight ticks. Lower is smoother and costs more segments.
        public int aimPreviewStrideTicks = 4;
        public int aimPreviewMaxPoints = 96;

        // Exhaust animation, drawn over the projectile's own graphic. Held off until the boost phase
        // ends so the missile coasts out of the tube dark and only lights up once the seeker has it.
        // Leave the path null to draw no trail at all.
        public string trailTexPath;
        public int trailFrameCount = 4;
        public int trailTicksPerFrame = 3;
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
    // The trail materials are cached in static fields, which RimWorld's startup check flags on any
    // type without this attribute: it cannot tell that they are only ever built during a draw call,
    // which is already on the main thread.
    [StaticConstructorOnStartup]
    public class Projectile_JavelinMissile : Bullet
    {
        // Any value above one keeps ticksToImpact positive, so the base mover never mistakes a
        // steering update for an arrival while the interpolation still lands on the guided position.
        private const int PathStretch = 8;

        // Vanilla gates free-intercept collision on InterceptChanceFactorFromDistance(origin, cell),
        // which returns zero inside 5 tiles of origin. Rewriting origin to the missile's current
        // position each tick therefore keeps that roll switched off for the whole flight, which is
        // what stops bystanders and low cover soaking a guided missile. What the roll would have
        // caught - walls and rock - is handled explicitly by TryBlockOnSolidObstacle instead.
        // Shield belts are unaffected either way: CompProjectileInterceptor is checked at the top of
        // CheckForFreeInterceptBetween, before the distance gate.

        // Sampling step for that obstacle walk, matching vanilla's own 0.2-tile intercept walk.
        private const float ObstacleScanStep = 0.2f;

        private static Material[] trailMaterials;
        private static string trailMaterialsPath;

        private JavelinFlightState flight;
        private bool flightInitialized;
        private float pendingDamageMultiplier = 1f;

        // Where the shot left the tube, kept because origin is rewritten every tick and the obstacle
        // grace distance has to be measured from the launcher rather than from the missile.
        private Vector3 launchOrigin;

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

            launchOrigin = origin;
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
            // after it subtracts delta, so movement, shield interception and impact all stay on the
            // base code path. Pinning origin to the missile's own position leaves the interpolation
            // identical while the intercept distance reads as zero, which is what keeps vanilla's
            // free-intercept roll from letting a bystander soak the shot.
            origin = previousPosition;
            destination = previousPosition + step * PathStretch;
            ticksToImpact = delta * PathStretch;

            base.TickInterval(delta);

            if (Destroyed || !Spawned)
            {
                return;
            }

            // Checked before the arrival test so a wall between the missile and its target stops it
            // on the wall rather than teleporting the hit through.
            if (TryBlockOnSolidObstacle(previousPosition, newPosition))
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

        /// <summary>
        /// Walks the cells the missile crossed this tick and detonates it on the first thing it
        /// physically cannot pass. Returns true if the missile is gone.
        /// </summary>
        private bool TryBlockOnSolidObstacle(Vector3 from, Vector3 to)
        {
            DefModExtension_JavelinMissile props = Props;
            Map map = Map;
            if (map == null || props == null || !props.solidObstaclesBlock)
            {
                return false;
            }

            Vector3 segment = (to - from).Yto0();
            float length = segment.magnitude;
            if (length < 0.0001f)
            {
                return false;
            }

            Thing target = intendedTarget.Thing ?? usedTarget.Thing;
            Vector3 stride = segment / length * ObstacleScanStep;
            int steps = Mathf.CeilToInt(length / ObstacleScanStep);
            IntVec3 lastCell = from.ToIntVec3();
            Vector3 probe = from;

            for (int i = 0; i < steps; i++)
            {
                // The last probe lands exactly on the end of the segment rather than overshooting it,
                // so the walk never reports a cell the missile has not actually entered yet.
                probe = i == steps - 1 ? to : probe + stride;

                IntVec3 cell = probe.ToIntVec3();
                if (cell == lastCell || !cell.InBounds(map))
                {
                    continue;
                }

                lastCell = cell;
                if (TryBlockInCell(cell, map, target, props))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBlockInCell(IntVec3 cell, Map map, Thing target, DefModExtension_JavelinMissile props)
        {
            float tilesFromLaunch = (cell.ToVector3Shifted() - launchOrigin).Yto0().magnitude;
            List<Thing> things = cell.GetThingList(map);

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!CanHit(thing))
                {
                    continue;
                }

                bool openDoor = thing is Building_Door door && door.Open;
                bool blocksFully = thing.def.Fillage == FillCategory.Full;
                if (!JavelinObstacleRules.Blocks(blocksFully, openDoor, thing == target, tilesFromLaunch, props.solidObstacleArmTiles))
                {
                    continue;
                }

                Position = cell;
                Impact(thing);
                return true;
            }

            return false;
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

        // The exhaust is a separate layer over the projectile's own graphic rather than a swapped
        // texture, so the rocket body art is shared by both phases and only the plume animates.
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            Material trail = CurrentTrailMaterial();
            if (trail == null)
            {
                return;
            }

            // A hair below the projectile so the plume sits under the rocket instead of z-fighting
            // with it; the two sprites are drawn on the same quad and overlap at the tail.
            Vector3 trailLoc = drawLoc;
            trailLoc.y -= 0.01f;
            Graphics.DrawMesh(MeshPool.GridPlane(def.graphicData.drawSize), trailLoc, ExactRotation, trail, 0);
        }

        private Material CurrentTrailMaterial()
        {
            DefModExtension_JavelinMissile props = Props;
            if (!flightInitialized || props?.trailTexPath == null || props.trailFrameCount < 1)
            {
                return null;
            }

            // The whole point of the client's request: nothing burns while the missile is still
            // coasting out of the tube on its launch heading.
            int ticksSinceBoost = flight.ticksFlown - props.boostTicks;
            if (ticksSinceBoost < 0)
            {
                return null;
            }

            int ticksPerFrame = Mathf.Max(1, props.trailTicksPerFrame);
            int frame = ticksSinceBoost / ticksPerFrame % props.trailFrameCount;
            return TrailMaterial(props.trailTexPath, props.trailFrameCount, frame);
        }

        // Frames are separate textures rather than one strip sliced by UV offsets: mod textures load
        // with a full mip chain, and a projectile is drawn small enough to sample a low mip, which
        // bleeds neighbouring frames into each other across a strip.
        private static Material TrailMaterial(string texPath, int frameCount, int frame)
        {
            if (trailMaterials == null || trailMaterialsPath != texPath || trailMaterials.Length != frameCount)
            {
                trailMaterials = new Material[frameCount];
                trailMaterialsPath = texPath;
            }

            if (trailMaterials[frame] == null)
            {
                trailMaterials[frame] = MaterialPool.MatFrom(texPath + (frame + 1), ShaderDatabase.TransparentPostLight);
            }

            return trailMaterials[frame];
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref flightInitialized, nameof(flightInitialized));
            Scribe_Values.Look(ref launchOrigin, nameof(launchOrigin));
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
