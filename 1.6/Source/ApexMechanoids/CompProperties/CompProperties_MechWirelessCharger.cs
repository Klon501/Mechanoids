using Verse;

namespace ApexMechanoids
{
    public class CompProperties_MechWirelessCharger : CompProperties
    {
        public float radius;

        public float fuelPerCharge = 0.009f;

        public float powerPerCharge = 0.5f;

        public int maxMechPerCharge = int.MaxValue;


        public CompProperties_MechWirelessCharger() : base()
        {
            compClass = typeof(CompMechWirelessCharger);
        }
    }
}
