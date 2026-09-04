using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class HediffCompProperties_ApplyHediffOnRemove : HediffCompProperties
    {
        public HediffDef hediffDef;

        public HediffDef mechHediffDef;

        public ThoughtDef thoughtDef;

        public int durationTicks = 0;

        public HediffCompProperties_ApplyHediffOnRemove()
        {
            compClass = typeof(HediffComp_ApplyHediffOnRemove);
        }
    }

    public class HediffComp_ApplyHediffOnRemove : HediffComp
    {
        public HediffCompProperties_ApplyHediffOnRemove Props => (HediffCompProperties_ApplyHediffOnRemove)props;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.health == null)
            {
                return;
            }

            HediffDef aftermath = (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid) ? Props.mechHediffDef : Props.hediffDef;
            if (aftermath != null && pawn.health.hediffSet.GetFirstHediffOfDef(aftermath) == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(aftermath, pawn);
                if (Props.durationTicks > 0)
                {
                    HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
                    if (disappears != null)
                        disappears.ticksToDisappear = Props.durationTicks;
                }
                pawn.health.AddHediff(hediff);
            }

            if (Props.thoughtDef != null && pawn.RaceProps != null && pawn.RaceProps.IsFlesh && pawn.needs?.mood != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(Props.thoughtDef);
            }
        }
    }
}
