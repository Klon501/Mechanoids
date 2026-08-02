using System;

namespace ApexMechanoids
{
    /// <summary>
    /// Flight parameters for one javelin missile, in ticks and tiles.
    /// </summary>
    public struct JavelinFlightParams
    {
        public float speedPerTick;
        public float maxTurnPerTick;
        public int boostTicks;
        public float hitRadius;

        /// <summary>
        /// Range at which the missile counts as committed to its attack run. Once it has been this
        /// close and the range opens again it has had its pass, and guidance shuts down for good
        /// instead of letting the missile come round for another try.
        /// </summary>
        public float terminalRadius;

        /// <summary>Motor burn time. A spent missile is removed rather than loitering.</summary>
        public int lifetimeTicks;
    }

    /// <summary>
    /// Position, heading and guidance bookkeeping for a missile in flight.
    /// </summary>
    public struct JavelinFlightState
    {
        public float x;
        public float z;
        public float heading;
        public int ticksFlown;

        /// <summary>True once the missile has been inside the terminal radius at least once.</summary>
        public bool hasClosed;

        public bool guidanceLockedOut;
    }

    /// <summary>
    /// Pure flight maths for the javelin's homing missiles, kept free of Verse and Unity types so
    /// the trajectory can be simulated and checked outside the game.
    ///
    /// The missile leaves the launcher along the mech's cardinal facing, holds that heading for a
    /// boost phase, then steers toward the target at a capped turn rate. A target standing too
    /// close is overflown during the boost phase and cannot be recovered, which is where the
    /// weapon's minimum engagement range comes from.
    /// </summary>
    public static class JavelinMissileGuidance
    {
        public const float TwoPi = (float)(Math.PI * 2.0);

        /// <summary>Wraps an angle into (-pi, pi] so turn deltas take the short way around.</summary>
        public static float NormalizeAngle(float radians)
        {
            while (radians <= -(float)Math.PI)
            {
                radians += TwoPi;
            }
            while (radians > (float)Math.PI)
            {
                radians -= TwoPi;
            }

            return radians;
        }

        /// <summary>
        /// Heading for one of the four sprite facings, so a missile always leaves the launcher tube
        /// along a multiple of 90 degrees in world space. Rot4 order is north, east, south, west.
        /// </summary>
        public static float CardinalHeading(int rot4AsInt)
        {
            switch (((rot4AsInt % 4) + 4) % 4)
            {
                case 0: return (float)(Math.PI / 2.0);  // north, +z
                case 1: return 0f;                      // east, +x
                case 2: return (float)(-Math.PI / 2.0); // south, -z
                default: return (float)Math.PI;         // west, -x
            }
        }

        /// <summary>Turns <paramref name="heading"/> toward the target by at most maxTurnPerTick.</summary>
        public static float SteerHeading(float heading, float x, float z, float targetX, float targetZ, float maxTurnPerTick)
        {
            float desired = (float)Math.Atan2(targetZ - z, targetX - x);
            float delta = NormalizeAngle(desired - heading);

            if (delta > maxTurnPerTick)
            {
                delta = maxTurnPerTick;
            }
            else if (delta < -maxTurnPerTick)
            {
                delta = -maxTurnPerTick;
            }

            return NormalizeAngle(heading + delta);
        }

        public static float Distance(float x, float z, float targetX, float targetZ)
        {
            float dx = targetX - x;
            float dz = targetZ - z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static JavelinFlightState CreateState(float x, float z, float heading)
        {
            return new JavelinFlightState
            {
                x = x,
                z = z,
                heading = heading,
                ticksFlown = 0,
                hasClosed = false,
                guidanceLockedOut = false
            };
        }

        /// <summary>
        /// True once the missile has flown its boost phase and has not yet sailed past the target.
        /// </summary>
        public static bool GuidanceActive(JavelinFlightState state, JavelinFlightParams flightParams)
        {
            return !state.guidanceLockedOut && state.ticksFlown >= flightParams.boostTicks;
        }

        /// <summary>
        /// Advances the missile one tick toward the supplied target position.
        ///
        /// Once the missile has been inside the terminal radius and the range opens past it again,
        /// guidance locks out permanently. That turns an overflown target into an honest miss
        /// instead of a missile that loops around and eventually connects anyway.
        ///
        /// The range bookkeeping deliberately runs on every tick, including the boost phase. A
        /// target standing close enough to be overflown before guidance engages is exactly the case
        /// the client asked to miss, and gating this on guidance let such a missile treat its return
        /// leg as a fresh attack run and come back for a second pass.
        /// </summary>
        public static JavelinFlightState Step(JavelinFlightState state, float targetX, float targetZ, JavelinFlightParams flightParams)
        {
            float distance = Distance(state.x, state.z, targetX, targetZ);

            if (distance <= flightParams.terminalRadius)
            {
                state.hasClosed = true;
            }
            else if (state.hasClosed)
            {
                state.guidanceLockedOut = true;
            }

            bool guided = GuidanceActive(state, flightParams);

            if (guided)
            {
                state.heading = SteerHeading(state.heading, state.x, state.z, targetX, targetZ, flightParams.maxTurnPerTick);
            }

            state.x += (float)Math.Cos(state.heading) * flightParams.speedPerTick;
            state.z += (float)Math.Sin(state.heading) * flightParams.speedPerTick;
            state.ticksFlown++;
            return state;
        }

        /// <summary>True once the missile has burned through its motor and must be removed.</summary>
        public static bool IsExpired(JavelinFlightState state, JavelinFlightParams flightParams)
        {
            return flightParams.lifetimeTicks > 0 && state.ticksFlown >= flightParams.lifetimeTicks;
        }

        /// <summary>True when the missile is close enough to the target to detonate on it.</summary>
        public static bool HasReachedTarget(JavelinFlightState state, float targetX, float targetZ, JavelinFlightParams flightParams)
        {
            return Distance(state.x, state.z, targetX, targetZ) <= flightParams.hitRadius;
        }

        /// <summary>Escalating damage multiplier for a target that has already been hit stacks times.</summary>
        public static float DamageMultiplier(int stacks, float perStack, float maxMultiplier)
        {
            if (stacks < 0)
            {
                stacks = 0;
            }

            float multiplier = 1f + stacks * perStack;
            return multiplier > maxMultiplier ? maxMultiplier : multiplier;
        }
    }
}
