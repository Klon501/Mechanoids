using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_PowerPlantDynamo : CompProperties_Power
    {
        [NoTranslate]
        public string overrideAncientTexPath;

        public CompProperties_PowerPlantDynamo()
        {
            compClass = typeof(CompPowerPlantDynamo);
        }
    }
}
