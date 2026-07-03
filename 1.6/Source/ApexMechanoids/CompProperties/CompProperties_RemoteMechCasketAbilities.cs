using Verse;

namespace ApexMechanoids
{
    public class CompProperties_RemoteMechCasketAbilities : CompProperties
    {
        public CompProperties_RemoteMechCasketAbilities()
        {
            compClass = typeof(CompRemoteMechCasketAbilities);
        }

        public float actionspeed = 1f;

        public float repairExp = 0.05f;

        public bool repairCostsEnergy = true;

        public int Shieldcooldown = 300;    //300 is vanilla!

        public string textpath_Connect = "UI/Gizmos/APM_ConnectToMech";

        public string textpath_Disconnect = "UI/Gizmos/APM_DisconnectFromMech";

        public string textpath_Repair = "UI/Gizmos/APM_Repair";

        public string textpath_Shield = "UI/Gizmos/APM_ShieldMech";

        public string textpath_ShieldCooldown = "UI/Gizmos/APM_ShieldMechCooldown";

        public string labelShort = "Casket";

    }
}
