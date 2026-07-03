using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAreaHediffGiver : ThingComp
    {
        public CompProperties_AreaHediffGiver Props => (CompProperties_AreaHediffGiver)props;

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (parent.IsHashIntervalTick(Props.interval, delta))
            {
                GiveHediff();
            }
        }

        public void GiveHediff()
        {
            foreach (var item in GenRadial.RadialDistinctThingsAround(parent.PositionHeld, parent.MapHeld, Props.radius, true))
            {
                if (!(item is Pawn pawn)) continue;
                try
                {
                    if (Props.isGiveToHostile)
                    {
                        if (isHostile(pawn))
                        {
                            Hediff hediff = HediffMaker.MakeHediff(Props.hostileHediff,pawn);
                            pawn.health.AddHediff(hediff);
                        }
                    }
                    if (Props.isGiveToAllies)
                    {
                        if (pawn.Faction != parent.Faction)
                        {
                            Hediff hediff = HediffMaker.MakeHediff(Props.alliesHediff,pawn);
                            pawn.health.AddHediff(hediff);
                        }
                    }
                    if (Props.isGiveToSameFaction)
                    {
                        if (pawn.Faction == parent.Faction)
                        {
                            Hediff hediff = HediffMaker.MakeHediff(Props.sameFactionHediff,pawn);
                            pawn.health.AddHediff(hediff);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Error in CompAreaHediffGiver.GiveHediff {ex}");
                }
            }
        }

        public bool isHostile(Pawn pawn)
        {
            if(pawn.HostileTo(parent.Faction)) return true;
            if(pawn.HostileTo(parent)) return true;
            return false;
        }
    }
}
