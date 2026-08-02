using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_JavelinMissileLock : DefModExtension
    {
        /// <summary>
        /// Ticks without a fresh hit before one stack is shed. This has to stay comfortably longer
        /// than the launcher's own shot cycle (warmup plus cooldown, about 318 ticks), otherwise a
        /// lone javelin sheds each stack before it can fire again and the escalation never builds.
        /// </summary>
        public int decayIntervalTicks = 600;
    }

    /// <summary>
    /// Tracks how many javelin missiles have already landed on this pawn, which is what makes each
    /// successive hit on the same target hit harder.
    ///
    /// The count lives on the target rather than on the launcher, so several javelins concentrating
    /// fire feed one shared stack, and the whole thing disappears with the pawn instead of leaving a
    /// dangling reference behind in the save.
    /// </summary>
    public class Hediff_JavelinMissileLock : HediffWithComps
    {
        private const int FallbackDecayIntervalTicks = 600;

        private int stacks;
        private int ticksSinceLastHit;

        private int DecayIntervalTicks =>
            def?.GetModExtension<DefModExtension_JavelinMissileLock>()?.decayIntervalTicks ?? FallbackDecayIntervalTicks;

        public int Stacks => stacks;

        public override string LabelInBrackets => stacks.ToString();

        public override bool ShouldRemove => stacks <= 0;

        /// <summary>
        /// The hediff only exists because a missile landed, so it is born holding that first hit.
        /// Anything that adds it - the projectile, a dev-mode add, a test fixture - therefore gets a
        /// valid one-stack lock rather than an empty one that removes itself on the next health tick.
        /// </summary>
        public override void PostMake()
        {
            base.PostMake();
            stacks = 1;
            ticksSinceLastHit = 0;
            Severity = stacks;
        }

        public void RegisterHit()
        {
            stacks++;
            ticksSinceLastHit = 0;
            Severity = stacks;
        }

        public override void Tick()
        {
            base.Tick();

            ticksSinceLastHit++;
            if (ticksSinceLastHit < DecayIntervalTicks)
            {
                return;
            }

            ticksSinceLastHit = 0;
            stacks--;
            Severity = stacks > 0 ? stacks : 0f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stacks, nameof(stacks));
            Scribe_Values.Look(ref ticksSinceLastHit, nameof(ticksSinceLastHit));
        }
    }
}
