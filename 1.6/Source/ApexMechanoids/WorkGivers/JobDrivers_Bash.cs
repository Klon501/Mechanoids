using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
	public class JobDriver_Bash : JobDriver
	{
		public LocalTargetInfo Target => job.GetTarget(TargetIndex.A);

		private Vector3 exactPos;

		private Vector3 direction;

		public float moveSpeed = 1f;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref exactPos, "exactPos");
			Scribe_Values.Look(ref direction, "direction");
			Scribe_Values.Look(ref moveSpeed, "moveSpeed");
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override void Notify_Starting()
		{
			zeroPos = true;
			try
			{
				exactPos = pawn.TrueCenter();
			}
			finally
			{
				zeroPos = false;
			}
			direction = Target.CenterVector3 - exactPos;
				direction = direction.normalized;
				direction = direction.Yto0();
				base.Notify_Starting();
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil1 = ToilMaker.MakeToil("MoveBash");
			toil1.tickAction = delegate
			{
				toil1.actor.rotationTracker.FaceCell(Target.Cell);
				IntVec3 pos1 = exactPos.ToIntVec3();
				exactPos += direction * moveSpeed;
				IntVec3 pos2 = exactPos.ToIntVec3();
				if (pos1 != pos2)
				{
					TryEnterNextPathCell(pawn, pos2);
				}
			};
			toil1.handlingFacing = true;
			toil1.defaultCompleteMode = ToilCompleteMode.Never;
			yield return toil1;
			Toil toil2 = ToilMaker.MakeToil("ActionBash");
			toil2.initAction = delegate
			{
				pawn.Drawer.tweener.ResetTweenedPosToRoot();
				DoBashAction();
			};
			toil2.handlingFacing = true;
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}

		protected virtual void DoBashAction()
		{
			Find.CameraDriver.shaker.SetMinShake(0.1f);
		}

		protected virtual void TryEnterNextPathCell(Pawn pawn, IntVec3 nextCell)
		{
			if(Mathf.DeltaAngle((Target.CenterVector3 - exactPos).AngleFlat(), direction.AngleFlat()) > 60f || Target.Cell.DistanceTo(pawn.Position) <= 1.1f)
			{
				pawn.Position = TargetA.Cell;
				pawn.jobs.curDriver.ReadyForNextToil();
			}
			else
			{
				pawn.Position = nextCell;
			}
			pawn.filth.Notify_EnteredNewCell();
		}

		private bool zeroPos = false;

		public override Vector3 ForcedBodyOffset
		{
			get
			{
				if (zeroPos)
				{
					return Vector3.zero;
				}
				zeroPos = true;
				Vector3 vec;
				try
				{
					vec = pawn.DrawPos;
				}
				finally
				{
					zeroPos = false;
				}
				return exactPos - vec;
			}
		}
	}

	public class JobDriver_BashStun : JobDriver_Bash
	{
		protected override void DoBashAction()
		{
			base.DoBashAction();
			DamageInfo dinfo = new DamageInfo(DamageDefOf.Stun, job.maxNumMeleeAttacks, instigator: pawn);
			foreach (IntVec3 cell in CellRect.FromCell(pawn.Position).ExpandedBy(1))
			{
				foreach(Thing t in cell.GetThingList(pawn.Map).ToList())
				{
					if(t != pawn)
					{
						t.TakeDamage(dinfo);
					}
				}
			}
		}
	}

	public class JobDriver_BashDamage : JobDriver_Bash
	{
		protected override void DoBashAction()
		{
			base.DoBashAction();
			DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, job.maxNumMeleeAttacks, DamageDefOf.Blunt.defaultArmorPenetration, instigator: pawn);
			foreach (IntVec3 cell in CellRect.FromCell(pawn.Position).ExpandedBy(1))
			{
				foreach (Thing t in cell.GetThingList(pawn.Map).ToList())
				{
					if (t == pawn || (t.Faction != null && !t.HostileTo(pawn) && t != TargetThingB))
					{
						continue;
					}
					t.TakeDamage(dinfo);
				}
			}
		}
	}
}