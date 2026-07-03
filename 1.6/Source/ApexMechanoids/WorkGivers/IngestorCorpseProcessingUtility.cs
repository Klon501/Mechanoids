using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class IngestorCorpseProcessingUtility
    {
        public const float ChemfuelPerBodySizeOfCorpse = 36f;

        public static bool IsIngestor(Pawn pawn)
        {
            return pawn != null && pawn.def == ApexDefsOf.APM_Mech_Ingestor;
        }

        public static bool CanDoCorpseProcessing(Pawn pawn)
        {
            return IsIngestor(pawn)
                && pawn.Faction == Faction.OfPlayer
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Spawned
                && pawn.Map != null
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        public static bool IsProcessableCorpse(Corpse corpse)
        {
            if (corpse?.InnerPawn?.RaceProps == null || !corpse.InnerPawn.RaceProps.IsFlesh)
            {
                return false;
            }

            CompRottable rottable = corpse.TryGetComp<CompRottable>();
            return rottable != null && rottable.Stage != RotStage.Dessicated;
        }

        public static bool CanReserveAndProcessCorpse(Pawn pawn, Corpse corpse, bool forced = false)
        {
            return CanDoCorpseProcessing(pawn)
                && corpse != null
                && !corpse.Destroyed
                && corpse.Spawned
                && corpse.Map == pawn.Map
                && !corpse.IsForbidden(pawn)
                && !corpse.IsBurning()
                && IsProcessableCorpse(corpse)
                && pawn.CanReserveAndReach(corpse, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced);
        }

        public static Ability GetAbsorbAbility(Pawn pawn)
        {
            return pawn?.abilities?.GetAbility(ApexDefsOf.APM_Absorb);
        }

        public static bool CanUseAbsorbOnCorpse(Pawn pawn, Corpse corpse, bool forced = false)
        {
            Ability absorb = GetAbsorbAbility(pawn);
            LocalTargetInfo target = corpse;
            return absorb != null
                && absorb.CanCast
                && CanReserveAndProcessCorpse(pawn, corpse, forced)
                && absorb.CanApplyOn(target)
                && absorb.verb.ValidateTarget(target, false);
        }

        public static int ChemfuelFromCorpse(Corpse corpse, float chemfuelPerBodySize = ChemfuelPerBodySizeOfCorpse)
        {
            if (corpse?.InnerPawn == null)
            {
                return 0;
            }

            return System.Math.Max(1, UnityEngine.Mathf.FloorToInt(corpse.InnerPawn.BodySize * chemfuelPerBodySize));
        }

        public static int ChemfuelFromNutrition(Thing thing, float chemfuelPerNutrition)
        {
            if (thing?.def?.ingestible == null)
            {
                return 0;
            }

            return System.Math.Max(1, UnityEngine.Mathf.FloorToInt(thing.def.ingestible.CachedNutrition * thing.stackCount * chemfuelPerNutrition));
        }

        public static void SpawnChemfuelNear(IntVec3 center, Map map, int count)
        {
            if (map == null || !center.IsValid || count <= 0)
            {
                return;
            }

            Thing chemfuel = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
            chemfuel.stackCount = count;
            GenPlace.TryPlaceThing(chemfuel, center, map, ThingPlaceMode.Near);
        }
    }
}
