using RimWorld;
using UnityEngine;
using Verse.AI;
using Verse;

namespace ApexMechanoids
{
    public class WorkGiver_HaulToMechCommandCasket : WorkGiver_Scanner
    {
        private const float NutritionBuffer = 1.0f;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ApexDefsOf.APM_MechCommandCasket);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }
            if (t.IsBurning())
            {
                return false;
            }
            if (!(t is Building_MechCommandCasket building_Casket))
            {
                return false;
            }
            if (building_Casket.NutritionNeeded >= NutritionBuffer)
            {
                if (FindNutrition(pawn, building_Casket).Thing == null)
                {
                    JobFailReason.Is("NoFood".Translate());
                    return false;
                }

                return true;
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_MechCommandCasket building_Casket))
            {
                return null;
            }
            if (building_Casket.NutritionNeeded > 0f)
            {
                ThingCount thingCount = FindNutrition(pawn, building_Casket);
                if (thingCount.Thing != null)
                {
                    Job job = HaulAIUtility.HaulToContainerJob(pawn, thingCount.Thing, t);
                    job.count = Mathf.Min(job.count, thingCount.Count);
                    return job;
                }
            }
            return null;
        }

        private ThingCount FindNutrition(Pawn pawn, Building_MechCommandCasket casket)
        {
            Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Validator);
            if (thing == null)
            {
                return default(ThingCount);
            }
            int b = Mathf.CeilToInt(casket.NutritionNeeded / thing.GetStatValue(StatDefOf.Nutrition));
            return new ThingCount(thing, Mathf.Min(thing.stackCount, b));
            bool Validator(Thing x)
            {
                if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
                {
                    return false;
                }
                if (!casket.CanAcceptNutrition(x))
                {
                    return false;
                }
                if (x.def.GetStatValueAbstract(StatDefOf.Nutrition) > casket.NutritionNeeded)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
