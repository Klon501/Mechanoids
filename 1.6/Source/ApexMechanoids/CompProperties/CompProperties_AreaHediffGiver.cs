using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace ApexMechanoids
{
    public class CompProperties_AreaHediffGiver : CompProperties
    {
        public int interval = 300;

        public float radius; 

        public bool isGiveToHostile = false;

        public HediffDef hostileHediff;

        public bool isGiveToAllies = false;

        public HediffDef alliesHediff;

        public bool isGiveToSameFaction = false;

        public HediffDef sameFactionHediff;

        public CompProperties_AreaHediffGiver()
        {
            compClass = typeof(CompAreaHediffGiver);
        }
    }
}