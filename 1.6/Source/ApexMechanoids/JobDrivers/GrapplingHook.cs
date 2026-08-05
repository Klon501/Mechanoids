using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using static RimWorld.Dialog_BeginRitual;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;

namespace ApexMechanoids
{

	public class JobDriver_HookPawn : JobDriver_CastAbility
	{
		private Projectile_GrapplingHook hook;

		public bool hooked = false;

		public override IEnumerable<Toil> MakeNewToils()
		{
			foreach (Toil item in base.MakeNewToils())
			{
				yield return item;
			}
			List<Func<JobCondition>> list = globalFailConditions.ToList();
			globalFailConditions.Clear();
			foreach (Func<JobCondition> endCondition in list)
			{
				Func<JobCondition> newEndCondition = () => hooked ? JobCondition.Ongoing : endCondition.Invoke();
				globalFailConditions.Add(newEndCondition);
			}
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			toil.initAction = delegate
			{
				pawn.rotationTracker.FaceTarget(base.TargetThingA);
				pawn.pather.StopDead();
				hook = (Projectile_GrapplingHook)GenSpawn.Spawn(ApexDefsOf.APM_Projectile_Hook, base.TargetThingA.Position, pawn.Map);
				hook.Launch(pawn, pawn.DrawPos, base.TargetThingA, base.TargetThingA, ProjectileHitFlags.IntendedTarget, true);
			};
			toil.tickIntervalAction = delegate
			{
				pawn.rotationTracker.FaceTarget(base.TargetThingA);
			};
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			toil.handlingFacing = true;
			/*toil.AddFailCondition(delegate
			{
				if (hooked)
				{
					return false;
				}
				if (hook != null)
				{
					if (hook.DestroyedOrNull() || !job.ability.verb.CanHitTargetFrom(pawn.Position, base.TargetThingA))
					{
						return true;
					}
				}
				return false;
			});*/
			yield return toil;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref hook, "hook");
			Scribe_Values.Look(ref hooked, "hooked");
		}
	}

	public class Projectile_GrapplingHook : Projectile
	{
		public MoteDualAttached mote;

		public override void Tick()
		{
			base.Tick();
			if (mote == null)
			{
				TargetInfo other = (Launcher == null || !Launcher.Spawned) ? new TargetInfo(origin.ToIntVec3(), this.Map) : new TargetInfo(Launcher);
				mote = MoteMaker.MakeInteractionOverlay(ApexDefsOf.APM_Mote_HookRope, this, other/*, DrawPos - Position.ToVector3(), Vector3.zero*/);
			}
			mote.Maintain();
		}

		public override void Impact(Thing hitThing, bool blockedByShield = false)
		{
			Pawn caster = launcher as Pawn;
			if (!caster.DeadOrDowned)
			{
				IntVec3 position = hitThing?.Position ?? base.Position;
				IntVec3 flyerOrigin = base.Position;
				Pawn victim = hitThing as Pawn;
				GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);
				Pawn flyingPawn = caster;
				bool flag = false;
				if (victim != null && victim.BodySize < caster.BodySize)
				{
					position = caster.PositionHeld;
					flyingPawn = victim;
				}
				else
				{
					caster.jobs.EndCurrentJob(JobCondition.Succeeded);
					if (victim != null && victim.pather != null)
					{
						victim.pather.debugDisabled = true;
					}
					flag = true;
					flyerOrigin = caster.PositionHeld;
				}
				bool selected = Find.Selector.IsSelected(flyingPawn);
				PawnFlyer_Hooked flyer = (PawnFlyer_Hooked)PawnFlyer.MakeFlyer(ApexDefsOf.APM_PawnFlyer_Hooked, flyingPawn, position, null, null);
				
				if (flag)
				{
					flyer.target = hitThing;
				}
				else
				{
					flyer.target = caster;
				}
				if (!flag && caster.jobs?.curDriver is JobDriver_HookPawn driver)
				{
					driver.hooked = true;
				}
				flyer.mote = mote;
				if (flyer != null)
				{
					GenSpawn.Spawn(flyer, flyerOrigin, Map);
					if (selected)
					{
						Find.Selector.Select(flyingPawn, false, false);
					}
				}
			}
			Destroy();
		}
	}

	public class PawnFlyer_Hooked : PawnFlyer
	{
		public Thing target;

		public MoteDualAttached mote;

		public override void RespawnPawn()
		{
			Clear();
			base.RespawnPawn();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref target, "target");
		}

		public override void Tick()
		{
			base.Tick();
			TargetInfo other = (target == null || !target.Spawned) ? new TargetInfo(DestinationPos.ToIntVec3(), this.Map) : new TargetInfo(target);
			if (mote == null)
			{
				mote = MoteMaker.MakeInteractionOverlay(ApexDefsOf.APM_Mote_HookRope, this, other);
			}
			mote.UpdateTargets(this, other, Vector3.zero, Vector3.zero);
			mote.Maintain();
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			Clear();
			base.Destroy(mode);
		}

		public void Clear()
		{
			if (target is Pawn p)
			{
				if(p.pather != null)
				{
					p.pather.debugDisabled = false;
				}
				if (p.jobs?.curDriver is JobDriver_HookPawn driver)
				{
					driver.EndJobWith(JobCondition.Succeeded);
				}
			}
		}
	}

	public class PawnFlyerWorker_Hooked : PawnFlyerWorker
	{
		public PawnFlyerWorker_Hooked(PawnFlyerProperties properties) : base(properties)
		{
		}

		public override float GetHeight(float t) => 0f;
	}
}