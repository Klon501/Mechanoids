using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public static class FrostivusUtility
    {
        public static void ApplyDevouredHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
                return;
            if (pawn.health.hediffSet.HasHediff(ApexDefsOf.APM_Hediff_Devoured))
                return;
            pawn.health.AddHediff(ApexDefsOf.APM_Hediff_Devoured);
        }

        public static void RemoveDevouredHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
                return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_Hediff_Devoured);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }
    }
}
