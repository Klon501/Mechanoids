using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
	public class JobGiver_Tinker : ThinkNode_JobGiver
	{
		public override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.Drafted || !TinkerRepairUtility.CanDoTinkerRepair(pawn))
			{
				return null;
			}
			Thing thing = TinkerRepairUtility.FindRepairableBuilding(pawn);
			if (thing != null)
			{
				return JobMaker.MakeJob(JobDefOf.Repair, thing);
			}
			Thing thing2 = TinkerRepairUtility.FindRepairableMech(pawn);
			if (thing2 != null)
			{
				return JobMaker.MakeJob(JobDefOf.RepairMech, thing2);
			}
			return null;
		}
	}
}
