using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class TinkerRepairUtility
    {
        public const float MaxRepairSearchDistance = 9999f;

        public static bool IsTinker(Pawn pawn)
        {
            return pawn != null && pawn.def == ApexDefsOf.APM_Mech_Tinker;
        }

        public static bool CanDoTinkerRepair(Pawn pawn)
        {
            return IsTinker(pawn)
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Spawned
                && pawn.Map != null
                && pawn.Faction != null
                && HasRepairControl(pawn)
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        private static bool HasRepairControl(Pawn pawn)
        {
            return pawn.Faction != Faction.OfPlayer || pawn.IsColonyMechPlayerControlled;
        }

        public static Thing FindRepairableBuilding(Pawn pawn, float maxDistance = MaxRepairSearchDistance)
        {
            if (!CanDoTinkerRepair(pawn))
            {
                return null;
            }

            List<Thing> repairableBuildings = pawn.Map.listerBuildingsRepairable.RepairableBuildings(pawn.Faction);
            return GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                repairableBuildings,
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                maxDistance,
                t => CanRepairBuildingNow(pawn, t));
        }

        public static Thing FindRepairableMech(Pawn pawn, float maxDistance = MaxRepairSearchDistance)
        {
            if (!CanDoTinkerRepair(pawn))
            {
                return null;
            }

            List<Pawn> factionPawns = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            return GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                factionPawns,
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                maxDistance,
                t => CanRepairMechNow(pawn, t));
        }

        public static bool CanRepairBuildingNow(Pawn pawn, Thing t, bool forced = false)
        {
            if (!CanDoTinkerRepair(pawn))
            {
                return false;
            }

            Building building = t as Building;
            if (building == null || building.Destroyed || !building.Spawned || building.Map != pawn.Map)
            {
                return false;
            }

            if (building.Faction != pawn.Faction)
            {
                return false;
            }

            if (!RepairUtility.PawnCanRepairNow(pawn, building))
            {
                return false;
            }

            if (pawn.Faction == Faction.OfPlayer && !pawn.Map.areaManager.Home[building.Position])
            {
                JobFailReason.Is(WorkGiver_FixBrokenDownBuilding.NotInHomeAreaTrans);
                return false;
            }

            if (!pawn.CanReserve(building, 1, -1, null, forced))
            {
                return false;
            }

            if (building.Map.designationManager.DesignationOn(building, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (building.def.mineable && building.Map.designationManager.DesignationAt(building.Position, DesignationDefOf.Mine) != null)
            {
                return false;
            }

            if (building.def.mineable && building.Map.designationManager.DesignationAt(building.Position, DesignationDefOf.MineVein) != null)
            {
                return false;
            }

            return !building.IsBurning();
        }

        public static bool CanRepairMechNow(Pawn pawn, Thing t, bool forced = false)
        {
            if (!ModsConfig.BiotechActive || !CanDoTinkerRepair(pawn))
            {
                return false;
            }

            Pawn target = t as Pawn;
            if (target == null || target == pawn || target.Destroyed || target.Dead || !target.Spawned || target.Map != pawn.Map)
            {
                return false;
            }

            if (target.Faction != pawn.Faction)
            {
                return false;
            }

            CompMechRepairable repairable = target.TryGetComp<CompMechRepairable>();
            if (repairable == null || target.RaceProps == null || !target.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (target.InAggroMentalState || target.HostileTo(pawn))
            {
                return false;
            }

            if (Building_RepairStation.IsPawnClaimedByAnyRepairStation(target))
            {
                return false;
            }

            if (!pawn.CanReserve(target, 1, -1, null, forced))
            {
                return false;
            }

            if (target.IsBurning() || target.IsAttacking())
            {
                return false;
            }

            if (target.needs?.energy == null || !MechRepairUtility.CanRepair(target))
            {
                return false;
            }

            return forced || repairable.autoRepair;
        }
    }
}
