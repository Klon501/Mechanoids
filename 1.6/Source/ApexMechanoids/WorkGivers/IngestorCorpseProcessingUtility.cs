using RimWorld;
using UnityEngine;
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

        public static AcceptanceReport CanAbsorbThing(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return "APM_IngestorAbsorb_InvalidTarget".Translate();
            }

            if (thing is Corpse corpse)
            {
                return CanAbsorbCorpse(corpse);
            }

            if (thing.IngestibleNow && thing.stackCount > 0)
            {
                return true;
            }

            return "APM_IngestorAbsorb_InvalidTarget".Translate();
        }

        public static AcceptanceReport CanAbsorbCorpse(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || corpse.InnerPawn?.RaceProps == null)
            {
                return "APM_IngestorAbsorb_InvalidCorpse".Translate();
            }

            if (!corpse.InnerPawn.RaceProps.IsFlesh)
            {
                return "APM_IngestorAbsorb_NotOrganic".Translate();
            }

            if (corpse.IsBurning())
            {
                return "APM_IngestorAbsorb_Burning".Translate();
            }

            CompRottable rottable = corpse.TryGetComp<CompRottable>();
            if (rottable == null || rottable.Stage != RotStage.Fresh)
            {
                return "APM_IngestorAbsorb_NotFresh".Translate();
            }

            return true;
        }

        public static AcceptanceReport TryGetAbsorbOutput(Thing thing, CompProperties_Absorb props, out ThingDef outputThingDef, out int outputCount, out int durationTicks)
        {
            outputThingDef = null;
            outputCount = 0;
            durationTicks = 0;

            AcceptanceReport report = CanAbsorbThing(thing);
            if (!report.Accepted)
            {
                return report;
            }

            if (props == null)
            {
                return "APM_IngestorAbsorb_InvalidTarget".Translate();
            }

            outputThingDef = ThingDefOf.Chemfuel;
            if (thing is Corpse corpse && corpse.InnerPawn != null)
            {
                outputCount = ChemfuelFromCorpse(corpse, props.chemfuelPer1BodySizeOfCorpse);
                durationTicks = DurationTicksFromCurve(props.durationFromSize, corpse.InnerPawn.BodySize);
                return outputCount > 0 && durationTicks > 0;
            }

            if (thing.def?.ingestible != null)
            {
                float nutrition = thing.def.ingestible.CachedNutrition * thing.stackCount;
                outputCount = ChemfuelFromNutrition(thing, props.chemfuelPer1Nutrition);
                durationTicks = DurationTicksFromCurve(props.durationFromNutrition, nutrition);
                return outputCount > 0 && durationTicks > 0;
            }

            return "APM_IngestorAbsorb_InvalidTarget".Translate();
        }

        public static bool CanReserveAndProcessCorpse(Pawn pawn, Corpse corpse, bool forced = false)
        {
            return CanDoCorpseProcessing(pawn)
                && corpse != null
                && !corpse.Destroyed
                && corpse.Spawned
                && corpse.Map == pawn.Map
                && !corpse.IsForbidden(pawn)
                && CanAbsorbCorpse(corpse).Accepted
                && CanAutoProcessCorpse(pawn, corpse)
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

        public static Corpse FindBestAutoAbsorbCorpse(Pawn pawn, float maxDistance = 9999f)
        {
            if (!CanDoCorpseProcessing(pawn))
            {
                return null;
            }

            return RequiresMarkedCorpses(pawn)
                ? FindBestMarkedAbsorbCorpse(pawn, maxDistance)
                : FindClosestAbsorbCorpse(pawn, maxDistance);
        }

        public static Job MakeAbsorbCorpseJob(Pawn pawn, Corpse corpse, Ability absorb = null, int expiryInterval = 500)
        {
            if (absorb == null)
            {
                absorb = GetAbsorbAbility(pawn);
            }

            if (absorb == null || corpse == null || !absorb.CanCast)
            {
                return null;
            }

            Job job = absorb.GetJob(corpse, corpse);
            job.expiryInterval = expiryInterval;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static HediffComp_IngestorBiomassProcessor GetOrCreateBiomassProcessor(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_AbsorbedThing);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(ApexDefsOf.APM_AbsorbedThing, pawn);
                pawn.health.AddHediff(hediff);
            }

            return hediff.TryGetComp<HediffComp_IngestorBiomassProcessor>();
        }

        private static Corpse FindBestMarkedAbsorbCorpse(Pawn pawn, float maxDistance)
        {
            if (pawn?.Map == null)
            {
                return null;
            }

            Corpse bestCorpse = null;
            Designation staleDesignation = null;
            int bestDistance = int.MaxValue;
            int maxDistanceSquared = maxDistance >= 9999f ? int.MaxValue : Mathf.CeilToInt(maxDistance * maxDistance);

            foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(ApexDefsOf.APM_IngestorAbsorbCorpse))
            {
                if (!(designation.target.Thing is Corpse corpse))
                {
                    if (staleDesignation == null)
                    {
                        staleDesignation = designation;
                    }
                    continue;
                }

                int distance = pawn.Position.DistanceToSquared(corpse.Position);
                if (distance > maxDistanceSquared || distance >= bestDistance)
                {
                    continue;
                }

                if (!CanUseAbsorbOnCorpse(pawn, corpse))
                {
                    if (ShouldClearAbsorbDesignation(corpse))
                    {
                        if (staleDesignation == null)
                        {
                            staleDesignation = designation;
                        }
                    }

                    continue;
                }

                bestCorpse = corpse;
                bestDistance = distance;
            }

            if (staleDesignation != null)
            {
                pawn.Map.designationManager.RemoveDesignation(staleDesignation);
            }

            return bestCorpse;
        }

        private static Corpse FindClosestAbsorbCorpse(Pawn pawn, float maxDistance)
        {
            if (pawn?.Map == null)
            {
                return null;
            }

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Corpse),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Deadly),
                maxDistance,
                thing => thing is Corpse corpse && CanUseAbsorbOnCorpse(pawn, corpse)) as Corpse;
        }

        public static CompIngestorAbsorbSettings GetAbsorbSettings(Pawn pawn)
        {
            return pawn?.TryGetComp<CompIngestorAbsorbSettings>();
        }

        public static bool RequiresMarkedCorpses(Pawn pawn)
        {
            return GetAbsorbSettings(pawn)?.onlyProcessMarkedCorpses ?? false;
        }

        public static bool IsMarkedForAbsorb(Corpse corpse)
        {
            return corpse?.Map?.designationManager.DesignationOn(corpse, ApexDefsOf.APM_IngestorAbsorbCorpse) != null;
        }

        public static bool CanAutoProcessCorpse(Pawn pawn, Corpse corpse)
        {
            return !RequiresMarkedCorpses(pawn) || IsMarkedForAbsorb(corpse);
        }

        public static bool ShouldStripCorpseBeforeAbsorb(Pawn pawn, Corpse corpse)
        {
            return (GetAbsorbSettings(pawn)?.stripCorpseBeforeAbsorb ?? false) && HasStrippableThings(corpse);
        }

        public static bool ShouldClearAbsorbDesignation(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.InnerPawn?.RaceProps == null)
            {
                return true;
            }

            if (!corpse.InnerPawn.RaceProps.IsFlesh)
            {
                return true;
            }

            CompRottable rottable = corpse.TryGetComp<CompRottable>();
            return rottable == null || rottable.Stage != RotStage.Fresh;
        }

        private static bool HasStrippableThings(Corpse corpse)
        {
            Pawn innerPawn = corpse?.InnerPawn;
            return innerPawn?.apparel?.WornApparelCount > 0
                || innerPawn?.equipment?.AllEquipmentListForReading?.Count > 0
                || innerPawn?.inventory?.innerContainer?.Count > 0;
        }

        public static int ChemfuelFromCorpse(Corpse corpse, float chemfuelPerBodySize = ChemfuelPerBodySizeOfCorpse)
        {
            if (corpse?.InnerPawn == null)
            {
                return 0;
            }

            return System.Math.Max(1, Mathf.FloorToInt(corpse.InnerPawn.BodySize * chemfuelPerBodySize));
        }

        public static int ChemfuelFromNutrition(Thing thing, float chemfuelPerNutrition)
        {
            if (thing?.def?.ingestible == null)
            {
                return 0;
            }

            return System.Math.Max(1, Mathf.FloorToInt(thing.def.ingestible.CachedNutrition * thing.stackCount * chemfuelPerNutrition));
        }

        public static void SpawnChemfuelNear(IntVec3 center, Map map, int count)
        {
            TrySpawnThingNear(center, map, ThingDefOf.Chemfuel, count);
        }

        public static void SpawnThingNear(IntVec3 center, Map map, ThingDef thingDef, int count)
        {
            TrySpawnThingNear(center, map, thingDef, count);
        }

        public static bool TrySpawnThingNear(IntVec3 center, Map map, ThingDef thingDef, int count)
        {
            if (map == null || !center.IsValid || thingDef == null || count <= 0)
            {
                return false;
            }

            int remaining = count;
            int stackLimit = System.Math.Max(1, thingDef.stackLimit);
            while (remaining > 0)
            {
                int stackCount = System.Math.Min(remaining, stackLimit);
                Thing thing = ThingMaker.MakeThing(thingDef);
                thing.stackCount = stackCount;
                if (!GenPlace.TryPlaceThing(thing, center, map, ThingPlaceMode.Near))
                {
                    return false;
                }

                remaining -= stackCount;
            }

            return true;
        }

        private static int DurationTicksFromCurve(SimpleCurve curve, float value)
        {
            float hours = curve?.Evaluate(value) ?? 1f;
            return System.Math.Max(1, Mathf.CeilToInt(hours * GenDate.TicksPerHour));
        }
    }
}