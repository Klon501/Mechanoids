using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class Hediff_CommandCasketBoost : Hediff
    {
        public HediffStage curStage;

        public int BandwidthOffset;

        public override HediffStage CurStage
        {
            get
            {
                if (curStage == null)
                {
                    StatModifier statModifier = new StatModifier();
                    statModifier.stat = StatDefOf.MechBandwidth;
                    statModifier.value = BandwidthOffset;
                    curStage = new HediffStage();
                    curStage.statOffsets = new List<StatModifier> { statModifier };
                }
                return curStage;
            }
        }

        public override void PostTickInterval(int delta)    // we are not ticking inside the casket, since we are not on a map!
        {
            base.PostTickInterval(delta);
            if (pawn.IsHashIntervalTick(60, delta))
            {
                if(!Utils.IsUplinkActiveFor(pawn))
                {
                    Severity = 0f;  //removes the hediff when pawn leaves the casket
                    return;
                }
                UpdateStats();
            }
        }

        public void UpdateStats()
        {
            curStage = null;
            pawn.mechanitor?.Notify_BandwidthChanged();
        }


       public override void ExposeData()
       {
           base.ExposeData();
           Scribe_Values.Look(ref BandwidthOffset, "BandwidthOffset", 0);
       }

    }
}
