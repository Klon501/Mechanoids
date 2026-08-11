using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_Incident_CallMulBossgroup : DefModExtension
    {
        public List<PawnKindDef> bosses = new List<PawnKindDef>();

        public List<PawnGenOption> escorts = new List<PawnGenOption>();

        public List<int> bossMultPerThreat = new List<int>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (bosses.NullOrEmpty())
            {
                yield return "at least one boss required";
            }
            if (escorts.NullOrEmpty())
            {
                yield return "no escort defined.";
            }
            if (bossMultPerThreat.NullOrEmpty())
            {
                yield return "no boss mult defined.";
            }
        }
    }
}
