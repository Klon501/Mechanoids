using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_ConvertToBuilding : CompProperties_AbilityEffect
    {
        public ThingDef thingDef;
        public CompProperties_ConvertToBuilding() : base()
        {
            compClass = typeof(CompAbilityEffect_ConvertToBuilding);
        }
    }
}
