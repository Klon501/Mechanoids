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

        // Range at which the missile counts as committed to its attack run. Once it has been this
        // close and the range opens again, guidance shuts down instead of coming round for a retry.
        public float terminalRadius;

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
        public bool hasClosed;
        public bool guidanceLockedOut;
    }

    /// <summary>
    /// Pure flight maths for the javelin's homing missiles, kept free of Verse and Unity types so
    /// the trajectory can be checked outside the game. The missile leaves along the mech's cardinal
    /// facing, holds that heading through a boost phase, then steers at a capped turn rate, which
    /// is where the weapon's minimum engagement range comes from.
    /// </summary>
    public static class JavelinMissileGuidance
    {
        public const float TwoPi = (float)(Math.PI * 2.0);

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

        // Rot4 order is north, east, south, west, so the missile always leaves the launcher tube
        // along a multiple of 90 degrees in world space.
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

        public static bool GuidanceActive(JavelinFlightState state, JavelinFlightParams flightParams)
        {
            return !state.guidanceLockedOut && state.ticksFlown >= flightParams.boostTicks;
        }

        public static JavelinFlightState Step(JavelinFlightState state, float targetX, float targetZ, JavelinFlightParams flightParams)
        {
            // This runs every tick, including the boost phase. A target close enough to be overflown
            // before guidance engages is meant to be missed, and gating the check on guidance let
            // such a missile treat its return leg as a fresh attack run and come back for a second
            // pass at something it had already flown past.
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

        public static bool IsExpired(JavelinFlightState state, JavelinFlightParams flightParams)
        {
            return flightParams.lifetimeTicks > 0 && state.ticksFlown >= flightParams.lifetimeTicks;
        }

        public static bool HasReachedTarget(JavelinFlightState state, float targetX, float targetZ, JavelinFlightParams flightParams)
        {
            return Distance(state.x, state.z, targetX, targetZ) <= flightParams.hitRadius;
        }

        /// <summary>
        /// Flies the whole shot without a map and writes the path into the caller's buffers, so the
        /// aim preview can draw the exact trajectory the missile will take instead of a second guess
        /// at it. Returns the number of points written; the last one is always where the flight ends,
        /// whether that is the target or wherever the motor burned out.
        /// </summary>
        public static int SamplePath(JavelinFlightState state, float targetX, float targetZ, JavelinFlightParams flightParams, int strideTicks, float[] xs, float[] zs)
        {
            if (xs == null || zs == null)
            {
                return 0;
            }

            int capacity = xs.Length < zs.Length ? xs.Length : zs.Length;
            if (capacity < 1)
            {
                return 0;
            }

            if (strideTicks < 1)
            {
                strideTicks = 1;
            }

            int count = 0;
            xs[count] = state.x;
            zs[count] = state.z;
            count++;

            while (count < capacity)
            {
                bool ended = false;
                for (int i = 0; i < strideTicks; i++)
                {
                    state = Step(state, targetX, targetZ, flightParams);
                    if (HasReachedTarget(state, targetX, targetZ, flightParams) || IsExpired(state, flightParams))
                    {
                        ended = true;
                        break;
                    }
                }

                xs[count] = state.x;
                zs[count] = state.z;
                count++;

                if (ended)
                {
                    break;
                }
            }

            return count;
        }

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
