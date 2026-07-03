using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
	public class JobGiver_Tinker : ThinkNode_JobGiver
	{
		public override Job TryGiveJob(Pawn pawn)
		{
			if(pawn.def != ApexDefsOf.APM_Mech_Tinker)
			{
				return null;
			}
			Thing thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerBuildingsRepairable.RepairableBuildings(pawn.Faction), PathEndMode.Touch, TraverseParms.For(pawn), 9999f, (Thing x) => ValidRepairableBuilding(pawn, x));
			if (thing != null)
			{
				return JobMaker.MakeJob(JobDefOf.Repair, thing);
			}
			Thing thing2 = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction), PathEndMode.Touch, TraverseParms.For(pawn), 9999f, (Thing x) => ValidRepairablePawn(pawn, x));
			if (thing2 != null)
			{
				return JobMaker.MakeJob(JobDefOf.RepairMech, thing2);
			}
			return null;
		}

		public bool ValidRepairableBuilding(Pawn pawn, Thing t)
		{
			if (!RepairUtility.PawnCanRepairNow(pawn, t))
			{
				return false;
			}
			Building building = t as Building;
			if (pawn.Faction == Faction.OfPlayer && !pawn.Map.areaManager.Home[t.Position])
			{
				JobFailReason.Is(WorkGiver_FixBrokenDownBuilding.NotInHomeAreaTrans);
				return false;
			}
			if (!pawn.CanReserve(building, 1, -1, null))
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
			if (building.IsBurning())
			{
				return false;
			}
			return true;
		}

		public bool ValidRepairablePawn(Pawn pawn, Thing t)
		{
			Pawn pawn2 = (Pawn)t;
			CompMechRepairable compMechRepairable = t.TryGetComp<CompMechRepairable>();
			if (compMechRepairable == null)
			{
				return false;
			}
			if (!pawn2.RaceProps.IsMechanoid)
			{
				return false;
			}
			if (pawn2.InAggroMentalState || pawn2.HostileTo(pawn))
			{
				return false;
			}
			if (!pawn.CanReserve(t, 1, -1, null))
			{
				return false;
			}
			if (pawn2.IsBurning())
			{
				return false;
			}
			if (pawn2.IsAttacking())
			{
				return false;
			}
			if (pawn2.needs.energy == null)
			{
				return false;
			}
			if (!MechRepairUtility.CanRepair(pawn2))
			{
				return false;
			}
			return compMechRepairable.autoRepair;
		}
	}
}
