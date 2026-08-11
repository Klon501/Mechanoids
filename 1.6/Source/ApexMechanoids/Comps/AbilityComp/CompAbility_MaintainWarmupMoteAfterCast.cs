using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbility_MaintainWarmupMoteAfterCast : AbilityComp
    {
        private int ticks = 0;
        public CompProperties_MaintainWarmupMoteAfterCast Props => (CompProperties_MaintainWarmupMoteAfterCast)props;
        public override void CompTick()
        {
            base.CompTick();
            if (parent.wasCastingOnPrevTick)
            {
                ticks = Props.ticksToMaintain;
            }
            if (ticks > 0)
            {
                parent.warmupMote.Maintain();
                ticks--;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticks, nameof(ticks));
        }
    }
    public class CompProperties_MaintainWarmupMoteAfterCast : AbilityCompProperties
    {
        public CompProperties_MaintainWarmupMoteAfterCast()
        {
            this.compClass = typeof(CompAbility_MaintainWarmupMoteAfterCast);
        }
        public int ticksToMaintain = 30;
    }
}
