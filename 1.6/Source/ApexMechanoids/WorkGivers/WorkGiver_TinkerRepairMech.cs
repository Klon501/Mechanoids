using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_TinkerRepairMech : WorkGiver_Scanner
    {
        private const string TinkerDefName = "APM_Mech_Tinker";

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Faction == null)
            {
                yield break;
            }

            foreach (Pawn candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                if (candidate != pawn)
                {
                    yield return candidate;
                }
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !IsTinker(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn target = t as Pawn;
            if (!ModsConfig.BiotechActive || !IsTinker(pawn) || target == null || target == pawn)
            {
                return false;
            }

            if (pawn.Drafted || pawn.Map == null || !pawn.Spawned || target.Map != pawn.Map || !target.Spawned || target.Destroyed || target.Dead)
            {
                return false;
            }

            if (target.Faction != pawn.Faction)
            {
                return false;
            }

            CompMechRepairable repairable = target.TryGetComp<CompMechRepairable>();
            if (repairable == null || !target.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (target.InAggroMentalState || target.HostileTo(pawn) || target.IsBurning() || target.IsAttacking())
            {
                return false;
            }

            if (target.needs.energy == null || !MechRepairUtility.CanRepair(target))
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return forced || repairable.autoRepair;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDefOf.RepairMech, t);
        }

        private static bool IsTinker(Pawn pawn)
        {
            return pawn?.def?.defName == TinkerDefName;
        }
    }
}
