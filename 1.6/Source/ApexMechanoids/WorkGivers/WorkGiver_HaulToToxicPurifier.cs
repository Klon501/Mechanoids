using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
	public class WorkGiver_HaulToToxicPurifier : WorkGiver_Scanner
	{
		private const float MaxFillPercent = 0.5f;

		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ApexDefsOf.APM_Building_ToxicPurifier);

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!pawn.CanReserve(t, 1, -1, null, forced))
			{
				return false;
			}
			CompToxicPurifier comp = t.TryGetComp<CompToxicPurifier>();
			if (comp.mode == PurifierMode.OnlyGround)
			{
				return false;
			}
			if (comp.Full)
			{
				JobFailReason.Is(HaulAIUtility.ContainerFullLowerTrans);
				return false;
			}
			if (!forced && comp.FillPercent > 0.5f)
			{
				return false;
			}
			if (!forced && comp.mode == PurifierMode.GroundOverWastepacks && comp.HasCellToUnpolluteCached())
			{
				return false;
			}
			if (HaulAIUtility.FindFixedIngredientCount(pawn, ThingDefOf.Wastepack, comp.Props.wastepackCapacity - comp.wastepacksCount).NullOrEmpty())
			{
				JobFailReason.Is("NoIngredient".Translate(ThingDefOf.Wastepack));
				return false;
			}
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			CompToxicPurifier comp = t.TryGetComp<CompToxicPurifier>();
			if (comp.Full)
			{
				JobFailReason.Is(HaulAIUtility.ContainerFullLowerTrans);
				return null;
			}
			if (comp.mode == PurifierMode.OnlyGround)
			{
				return null;
			}
			int spaceLeft = comp.Props.wastepackCapacity - comp.wastepacksCount;
			List<Thing> list = HaulAIUtility.FindFixedIngredientCount(pawn, ThingDefOf.Wastepack, spaceLeft);
			if (list.NullOrEmpty())
			{
				JobFailReason.Is("NoIngredient".Translate(ThingDefOf.Wastepack));
				return null;
			}
			Job job = JobMaker.MakeJob(ApexDefsOf.APM_HaulToToxicPurifier, t);
			job.targetQueueB = list.Select((Thing f) => new LocalTargetInfo(f)).ToList();
			job.count = spaceLeft;
			return job;
		}
	}

	public class JobDriver_HaulToToxicPurifier : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
			return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			AddEndCondition(() => (base.TargetThingA.TryGetComp<CompToxicPurifier>().Full) ? JobCondition.Succeeded : JobCondition.Ongoing);
			Toil clearQueue = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.B);
			yield return clearQueue;
			yield return Toils_JobTransforms.SucceedOnNoTargetInQueue(TargetIndex.B);
			yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);
			yield return Toils_Reserve.Reserve(TargetIndex.B);
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(clearQueue, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
			Toil toil = ToilMaker.MakeToil("DepositHauledThingInPurifier");
			toil.initAction = delegate
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;
				if (actor.carryTracker.CarriedThing == null)
				{
					Log.Error(actor?.ToString() + " tried to place hauled thing in purifier but is not hauling anything.");
				}
				else
				{
					Thing purifier = curJob.GetTarget(TargetIndex.A).Thing;
					int num = actor.carryTracker.CarriedThing.stackCount;
					CompToxicPurifier comp = purifier.TryGetComp<CompToxicPurifier>();
					comp.wastepacksCount += num;
					actor.carryTracker.innerContainer.ClearAndDestroyContents();
				}
			};
			yield return toil;
			yield return Toils_Jump.Jump(clearQueue);
		}
	}
}
