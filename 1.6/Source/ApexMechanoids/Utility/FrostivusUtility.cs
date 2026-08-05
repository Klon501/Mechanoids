using Verse;

namespace ApexMechanoids
{
    public static class FrostivusUtility
    {
        public static Pawn ContainedPawn(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                return pawn;
            }

            if (thing is Corpse corpse)
            {
                return corpse.InnerPawn;
            }

            return null;
        }

        public static bool HasDevouredHediff(Pawn pawn)
        {
            return pawn?.health?.hediffSet != null
                && pawn.health.hediffSet.HasHediff(ApexDefsOf.APM_Hediff_Devoured);
        }

        public static bool HasDevouredHediff(Thing thing)
        {
            return HasDevouredHediff(ContainedPawn(thing));
        }

        public static void ApplyDevouredHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            if (HasDevouredHediff(pawn))
            {
                return;
            }

            pawn.health.AddHediff(ApexDefsOf.APM_Hediff_Devoured);
        }

        public static void ApplyDevouredHediff(Thing thing)
        {
            ApplyDevouredHediff(ContainedPawn(thing));
        }

        public static void RemoveDevouredHediff(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_Hediff_Devoured);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public static void RemoveDevouredHediff(Thing thing)
        {
            RemoveDevouredHediff(ContainedPawn(thing));
        }
    }
}
