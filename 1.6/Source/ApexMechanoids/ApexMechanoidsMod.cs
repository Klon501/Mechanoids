using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class ApexMechanoidsMod : Mod
    {
        public ApexMechanoidsMod(ModContentPack content) : base(content)
        {
            GetSettings<ApexMechanoidsSettings>();
        }

        public override string SettingsCategory()
        {
            return "Apex Mechanoids";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "APM_Settings_ShowRepairStationAutoRepairMessages_Label".Translate(),
                ref ApexMechanoidsSettings.showRepairStationAutoRepairMessages,
                "APM_Settings_ShowRepairStationAutoRepairMessages_Desc".Translate());
            listing.End();
        }
    }

    public class ApexMechanoidsSettings : ModSettings
    {
        public static bool showRepairStationAutoRepairMessages = true;

        public static bool ShowRepairStationAutoRepairMessages => showRepairStationAutoRepairMessages;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showRepairStationAutoRepairMessages, "showRepairStationAutoRepairMessages", true);
        }
    }
}
