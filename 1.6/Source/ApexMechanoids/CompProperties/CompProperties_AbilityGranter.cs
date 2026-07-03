using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilityGranter : CompProperties
    {
        public List<AbilityDef> abilities = new List<AbilityDef>();

        public CompProperties_AbilityGranter()
        {
            compClass = typeof(CompAbilityGranter);
        }
    }
}
