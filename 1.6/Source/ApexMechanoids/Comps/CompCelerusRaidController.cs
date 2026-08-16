using Verse;

namespace ApexMechanoids
{
    public enum CelerusRaidPhase
    {
        Ready,
        SmokeThrown,
        Strike,
        Retreat, // Legacy save value; no longer used by AI.
        CooldownWait // Legacy save value; no longer used by AI.
    }

    public class CompProperties_CelerusRaidController : CompProperties
    {
        public int smokeWaitTicks = 180;
        public int strikeWindowTicks = 180;
        public float smokeRadius = 5f;

        public CompProperties_CelerusRaidController()
        {
            compClass = typeof(CompCelerusRaidController);
        }
    }

    public class CompCelerusRaidController : ThingComp
    {
        private CelerusRaidPhase phase;
        private Thing raidTarget;
        private IntVec3 smokeCell = IntVec3.Invalid;
        private int phaseEndTick;

        public CompProperties_CelerusRaidController Props => (CompProperties_CelerusRaidController)props;

        public CelerusRaidPhase Phase => phase;

        public Thing RaidTarget => raidTarget;

        public IntVec3 SmokeCell => smokeCell;

        public bool PhaseExpired => phaseEndTick > 0 && Find.TickManager.TicksGame >= phaseEndTick;

        public void StartSmoke(Thing target, IntVec3 cell)
        {
            SetPhase(CelerusRaidPhase.SmokeThrown, target, cell, Props.smokeWaitTicks);
        }

        public void StartStrike(Thing target)
        {
            SetPhase(CelerusRaidPhase.Strike, target, IntVec3.Invalid, Props.strikeWindowTicks);
        }

        public void ResetRaid()
        {
            phase = CelerusRaidPhase.Ready;
            raidTarget = null;
            smokeCell = IntVec3.Invalid;
            phaseEndTick = 0;
        }

        private void SetPhase(CelerusRaidPhase nextPhase, Thing target, IntVec3 cell, int durationTicks)
        {
            phase = nextPhase;
            raidTarget = target;
            smokeCell = cell;
            phaseEndTick = Find.TickManager.TicksGame + durationTicks;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref phase, nameof(phase), CelerusRaidPhase.Ready);
            Scribe_References.Look(ref raidTarget, nameof(raidTarget));
            Scribe_Values.Look(ref smokeCell, nameof(smokeCell), IntVec3.Invalid);
            Scribe_Values.Look(ref phaseEndTick, nameof(phaseEndTick), 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (phase == CelerusRaidPhase.Retreat || phase == CelerusRaidPhase.CooldownWait))
            {
                ResetRaid();
            }
        }
    }
}
