using Verse;

namespace ApexMechanoids
{
    public class CompProperties_Aegis : CompProperties
    {
        // Amount of HP to regenerate per interval
        public float regenerationAmount = 1f;

        // Minimum time in seconds before regeneration starts after damage
        public int regenerationDelaySeconds = 20;

        // How often to regenerate in seconds (converted to ticks internally)
        public int regenerationIntervalSeconds = 5;

        public CompProperties_Aegis()
        {
            compClass = typeof(CompAegis);
        }
    }
}