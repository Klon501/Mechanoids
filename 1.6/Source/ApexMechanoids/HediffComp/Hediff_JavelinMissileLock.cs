using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_JavelinMissileLock : DefModExtension
    {
        // Ticks without a fresh hit before one stack is shed. Has to stay longer than the launcher's
        // own shot cycle (warmup plus cooldown, about 318 ticks), or a lone javelin sheds each stack
        // before it can fire again and the escalation never builds.
        public int decayIntervalTicks = 600;
    }

    /// <summary>
    /// Tracks how many javelin missiles have already landed on this pawn, which is what makes each
    /// successive hit on the same target hit harder. The count lives on the target rather than the
    /// launcher, so several javelins concentrating fire feed one shared stack and nothing is left
    /// dangling in the save.
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

        // Born holding the first hit, since it only exists because a missile landed. Anything that
        // adds it gets a valid one-stack lock instead of an empty one that removes itself on the
        // next health tick.
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
