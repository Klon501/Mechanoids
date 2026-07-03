using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class HediffGiver_StartWithHediff : HediffGiver
    {
        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            if (pawn.health.hediffSet.GetFirstHediffOfDef(hediff) == null)
            {
                Hediff newHediff = HediffMaker.MakeHediff(hediff, pawn, null);
                pawn.health.AddHediff(newHediff, null, null);
            }
        }
    }
}
