namespace ApexMechanoids
{
    /// <summary>
    /// The tuning knobs behind the anti-armour target preference, in the units the vanilla shooting
    /// score is expressed in. Vanilla keeps every candidate within 30 points of the best one and then
    /// picks between them at random, so an offset has to clear 30 points to actually decide a target
    /// rather than just nudge the odds.
    /// </summary>
    public struct JavelinTargetPriorityParams
    {
        // Combat power of the target the weapon is considered neutral about. Anything tougher scores
        // up from here, anything flimsier scores down.
        public float neutralCombatPower;

        public float scorePerCombatPower;
        public float maxCombatPowerScore;
        public float minCombatPowerScore;

        // Armour rating is what actually makes a target a job for this launcher rather than for the
        // rest of the line, so it is scored on top of raw combat power.
        public float scorePerArmourRating;
        public float maxArmourScore;

        // Every missile already landed on this target makes the next one hit harder, so switching
        // targets throws that away. Worth points to stop the mech wandering off mid-sequence.
        public float scorePerLockStack;
        public float maxLockScore;
    }

    /// <summary>
    /// A target reduced to the three things this weapon cares about.
    /// </summary>
    public struct JavelinTargetProfile
    {
        public bool isPawn;
        public float combatPower;
        public float armourRating;
        public int lockStacks;
    }

    /// <summary>
    /// Scores how much a javelin wants one target over another, as an offset added on top of
    /// vanilla's own shooting score. Kept free of Verse types so the ordering can be checked outside
    /// the game.
    ///
    /// The launcher is a tank killer with a long cycle and an escalating warhead, so it should be
    /// spending missiles on the heaviest thing in the fight and finishing what it started, not
    /// firing at whatever happens to be closest.
    /// </summary>
    public static class JavelinTargetPriority
    {
        public static float ScoreOffset(JavelinTargetProfile profile, JavelinTargetPriorityParams p)
        {
            // Buildings have no combat power to read and cannot carry a missile lock, so they are
            // left on vanilla's own scoring rather than given a made-up number.
            if (!profile.isPawn)
            {
                return 0f;
            }

            float score = Clamp(p.scorePerCombatPower * (profile.combatPower - p.neutralCombatPower), p.minCombatPowerScore, p.maxCombatPowerScore);
            score += Clamp(p.scorePerArmourRating * profile.armourRating, 0f, p.maxArmourScore);

            int stacks = profile.lockStacks > 0 ? profile.lockStacks : 0;
            score += Clamp(p.scorePerLockStack * stacks, 0f, p.maxLockScore);

            return score;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
