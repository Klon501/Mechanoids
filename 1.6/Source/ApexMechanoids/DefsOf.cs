using RimWorld;
using System.Security.Cryptography;
using Verse;

namespace ApexMechanoids
{
    [DefOf]
    public static class ApexDefsOf
    {
		public static ThingDef APM_Mech_Tinker;
		public static ThingDef APM_Mech_Frostivus;
        public static ThingDef APM_Mech_Ingestor;
        public static AbilityDef APM_Absorb;
        public static HediffDef APM_Hediff_TerminusOverdrive;
        public static BodyPartGroupDef APM_LeftAegisShield;
        public static BodyPartGroupDef APM_RightAegisShield;
        public static PawnKindDef APM_Mech_Aegis;
		public static BodyPartDef APM_AegisShield;
        //public static JobDef APM_RepairAegisShields;
        public static HediffDef APM_Hediff_Unity;
        public static HediffDef APM_DuelWinner;
        public static HediffDef APM_DuelDraw;
        public static HediffDef APM_InDuel;
        public static FleckDef ArcLargeEMP_B;
        public static HediffDef APM_AbsorbedThing;
        public static AnimationDef APM_EatingIngestor;
        public static FleckDef APM_AirPuffGreen;
        public static ThingDef APM_PawnFlyer_Hooked;
        public static ThingDef APM_Projectile_Hook;
        public static ThingDef APM_Mote_HookRope;
        public static StatDef APM_GestationFactor;
        public static ThingDef APM_MechCommandCasket;
		
        public static HediffDef RemoteRepairerImplant;  //from Biotech
        public static HediffDef RemoteShielderImplant;  //from Biotech
        public static HediffDef APM_Hediff_Devoured;
        public static SoundDef ShieldMech_Complete;     //from Biotech
        public static SoundDef ShieldMech_Start;        //from Biotech
        public static SoundDef APM_DuelWin;
        public static SoundDef APM_DuelLose;
        public static SoundDef APM_DuelStarted;
		
        static ApexDefsOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ApexDefsOf));
        }
		
        public static JobDef APM_LasherTame;
        public static JobDef APM_LasherTrain;
        public static JobDef APM_LasherMilk;
        public static JobDef APM_LasherShear;
        public static JobDef APM_LasherSlaughter;
        public static JobDef APM_LasherReleaseToWild;
        public static JobDef APM_SirenChatWithPrisoner;
        public static JobDef APM_FrostivusUnloadFoodToStorage;
        public static JobDef APM_FrostivusManualUnloadFood;
    }
    [DefOf]
    public static class ApexEffecterDefsOf
    {
        public static EffecterDef APM_DuelStart;
        public static EffecterDef APM_DuelWin;
        public static EffecterDef APM_DuelDraw;
        public static EffecterDef APM_DuelLose;
    }
}
