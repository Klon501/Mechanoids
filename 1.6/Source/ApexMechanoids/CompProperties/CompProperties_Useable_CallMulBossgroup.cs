using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_Useable_CallMulBossgroup : CompProperties_UseEffect
    {
        public IncidentDef incidentDef;

        public EffecterDef effecterDef;

        public EffecterDef prepareEffecterDef;

        [MustTranslate]
        public string leaderName;

        [NoTranslate]
        public string spawnLetterTextKey;

        [NoTranslate]
        public string spawnLetterLabelKey;

        public CompProperties_Useable_CallMulBossgroup()
        {
            compClass = typeof(CompUseEffect_CallMulBossgroup);
        }
    }
}
