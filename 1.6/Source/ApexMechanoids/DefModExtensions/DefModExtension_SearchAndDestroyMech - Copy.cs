using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_SearchAndDestroyMech : DefModExtension
    {
        public List<AbilityDef> disabledAutoUseAbilitiesWhenSearchAndDestroy = new List<AbilityDef>();
    }
}
