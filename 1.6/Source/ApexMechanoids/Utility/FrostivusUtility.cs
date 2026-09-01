using RimWorld;
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

        public static bool IsInCryoStasis(Thing thing)
        {
            Pawn pawn = ContainedPawn(thing);
            if (pawn == null || !HasDevouredHediff(pawn))
            {
                return false;
            }

            // Belt and braces: a leftover tag shouldn't freeze a pawn that's out in the open.
            return FrostivusFoodPreservationUtility.IsFrostivusInventoryHolder(thing.ParentHolder);
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

        // Released from the internal cryo-chamber: same after-effect a vanilla cryptosleep casket gives on eject.
        // Corpses and non-flesh pawns are skipped. Re-applying merges into the existing hediff and keeps the
        // longer remaining duration, so repeated swallow/release refreshes the timer instead of stacking.
        public static void ApplyCryptosleepSickness(Thing thing)
        {
            if (!(thing is Pawn pawn) || pawn.Dead || pawn.health == null)
            {
                return;
            }

            if (!(pawn.RaceProps?.IsFlesh ?? false))
            {
                return;
            }

            pawn.health.AddHediff(HediffDefOf.CryptosleepSickness);
        }
    }
}
