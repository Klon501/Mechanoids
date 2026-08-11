using System;

namespace ApexMechanoids
{
    /// <summary>
    /// Tuning for dodging a guided missile by moving against its approach.
    /// </summary>
    public struct JavelinEvasionParams
    {
        // Chance at a perfect head-on run at or above the reference speed.
        public float maxChance;

        // Below this much difference between where the missile is going and where the target is
        // going, the target counts as running with the missile and cannot shake it. A target fleeing
        // straight down the missile's own flight line is the easiest possible intercept.
        public float minAngleDegrees;

        // Speed at which the full angle bonus applies, in tiles per tick. Slower things get less of
        // it, so a heavy mech cannot sidestep a missile the way a running colonist can.
        public float referenceSpeedPerTick;
    }

    /// <summary>
    /// How likely a moving target is to shake a missile in its terminal dive, kept free of Verse
    /// types so the curve can be checked outside the game.
    ///
    /// The missile turns at a fixed rate, so what beats it is angle, not distance: a target running
    /// across or into the incoming missile forces a correction it cannot make in the last few tiles,
    /// while a target running away along the missile's own heading is simply chased down.
    /// </summary>
    public static class JavelinEvasion
    {
        public static float EvasionChance(float headingDeltaRadians, float targetSpeedPerTick, JavelinEvasionParams p)
        {
            if (targetSpeedPerTick <= 0f || p.maxChance <= 0f || p.referenceSpeedPerTick <= 0f)
            {
                return 0f;
            }

            // Absolute, because dodging left and dodging right are the same problem for the seeker.
            float delta = Math.Abs(JavelinMissileGuidance.NormalizeAngle(headingDeltaRadians)) * 180f / (float)Math.PI;
            if (delta <= p.minAngleDegrees)
            {
                return 0f;
            }

            float span = 180f - p.minAngleDegrees;
            if (span <= 0f)
            {
                return 0f;
            }

            float angleFactor = (delta - p.minAngleDegrees) / span;
            float speedFactor = targetSpeedPerTick / p.referenceSpeedPerTick;
            if (speedFactor > 1f)
            {
                speedFactor = 1f;
            }

            return p.maxChance * angleFactor * speedFactor;
        }
    }
}
