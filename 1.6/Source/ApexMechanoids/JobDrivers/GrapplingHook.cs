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
	public class Ability_TerminusHook : Ability
	{
		public Ability_TerminusHook()
		{
		}

		public Ability_TerminusHook(Pawn pawn)
			: base(pawn)
		{
		}

		public Ability_TerminusHook(Pawn pawn, AbilityDef def)
			: base(pawn, def)
		{
		}

		public Ability_TerminusHook(Pawn pawn, Precept sourcePrecept, AbilityDef def)
			: base(pawn, sourcePrecept, def)
		{
		}

		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			return base.AICanTargetNow(target) && TerminusHookUtility.IsValidAITarget(pawn, target.Pawn, this);
		}
	}

	public static class TerminusHookUtility
	{
		private const string HookAbilityDefName = "APM_HookPawn";
		private const string TerminusDefName = "APM_Mech_Terminus";
		private const string TerminusBossDefName = "APM_Mech_TerminusB";

		public const float DefaultMinAIHookDistance = 6f;

		public static bool IsValidAITarget(Pawn caster, Pawn target, Ability ability)
		{
			return IsValidAITarget(caster, target, ability, DefaultMinAIHookDistance, -1f);
		}

		public static bool IsValidAITarget(Pawn caster, Pawn target, Ability ability, float minDistance, float maxDistance)
		{
			if (caster == null || target == null || target == caster || caster.DeadOrDowned || target.DeadOrDowned)
			{
				return false;
			}
			if (!caster.Spawned || !target.Spawned || caster.Map == null || target.Map != caster.Map)
			{
				return false;
			}
			if (!target.HostileTo(caster) || target.IsPsychologicallyInvisible())
			{
				return false;
			}
			if (target.Fogged() || target.BodySize >= caster.BodySize)
			{
				return false;
			}
			if (caster.ParentHolder is PawnFlyer || target.ParentHolder is PawnFlyer)
			{
				return false;
			}
			if (target is IAttackTarget attackTarget && attackTarget.ThreatDisabled(caster))
			{
				return false;
			}

			LocalTargetInfo targetInfo = target;
			if (ability?.pawn != caster || ability.def?.defName != HookAbilityDefName || ability.verb == null || !ability.def.aiCanUse || !ability.CanCast)
			{
				return false;
			}
			if (!ability.def.verbProperties.targetParams.CanTarget(target) || !ability.CanApplyOn(targetInfo))
			{
				return false;
			}

			float distanceSquared = (caster.Position - target.Position).LengthHorizontalSquared;
			if (minDistance > 0f && distanceSquared < minDistance * minDistance)
			{
				return false;
			}
			if (maxDistance > 0f && distanceSquared > maxDistance * maxDistance)
			{
				return false;
			}

			float verbMinRange = ability.verb.verbProps.EffectiveMinRange(targetInfo, caster);
			if (verbMinRange > 0f && distanceSquared < verbMinRange * verbMinRange)
			{
				return false;
			}
			if (!ability.verb.CanHitTarget(targetInfo))
			{
				return false;
			}
			return caster.CanReserve(target, 1, -1, null, false);
		}

		public static bool IsTerminus(Pawn pawn)
		{
			string defName = pawn?.def?.defName;
			return defName == TerminusDefName || defName == TerminusBossDefName;
		}

		public static bool TryMakeBestAIHookJob(Pawn pawn, float minDistance, float maxRange, out Job job)
		{
			job = null;
			Ability ability = GetHookAbility(pawn);
			if (!TryFindBestAIHookTarget(pawn, ability, minDistance, maxRange, out Pawn target))
			{
				return false;
			}

			LocalTargetInfo targetInfo = target;
			job = ability.GetJob(targetInfo, targetInfo);
			if (job == null)
			{
				return false;
			}

			job.expiryInterval = 0;
			job.checkOverrideOnExpire = false;
			return true;
		}

		private static bool TryFindBestAIHookTarget(Pawn pawn, Ability ability, float minDistance, float maxRange, out Pawn target)
		{
			target = null;
			if (!CanUseHook(pawn, ability))
			{
				return false;
			}

			if (maxRange <= 0f || maxRange > ability.verb.EffectiveRange)
			{
				maxRange = ability.verb.EffectiveRange;
			}

			float bestScore = float.MinValue;
			IReadOnlyList<Pawn> spawnedPawns = pawn.Map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < spawnedPawns.Count; i++)
			{
				Pawn candidate = spawnedPawns[i];
				if (!IsValidAITarget(pawn, candidate, ability, minDistance, maxRange))
				{
					continue;
				}

				float distanceSquared = (pawn.Position - candidate.Position).LengthHorizontalSquared;
				float score = distanceSquared;
				if (candidate == pawn.mindState?.enemyTarget)
				{
					score += 25f;
				}

				if (target == null || score > bestScore)
				{
					target = candidate;
					bestScore = score;
				}
			}

			return target != null;
		}

		private static bool CanUseHook(Pawn pawn, Ability ability)
		{
			return pawn != null
				&& IsTerminus(pawn)
				&& Utils.CanRunAutonomousPawn(pawn)
				&& pawn.abilities != null
				&& pawn.CurJob?.ability == null
				&& ability?.pawn == pawn
				&& ability.def?.defName == HookAbilityDefName
				&& ability.def.aiCanUse
				&& ability.CanCast
				&& ability.verb != null
				&& pawn.Position.WalkableBy(pawn.Map, pawn)
				&& pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
		}

		private static Ability GetHookAbility(Pawn pawn)
		{
			List<Ability> abilities = pawn?.abilities?.AllAbilitiesForReading;
			if (abilities == null)
			{
				return null;
			}

			for (int i = 0; i < abilities.Count; i++)
			{
				Ability ability = abilities[i];
				if (ability?.def?.defName == HookAbilityDefName)
				{
					return ability;
				}
			}

			return null;
		}
	}

	public class JobDriver_HookPawn : JobDriver_CastAbility
	{
		private Projectile_GrapplingHook hook;

		public bool hooked = false;
		
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (job.GetTarget(TargetIndex.A).Thing is Pawn targetPawn)
			{
				return pawn.Reserve(targetPawn, job, 1, -1, null, errorOnFailed);
			}
			return true;
		}
		
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
			if (caster != null && !caster.DeadOrDowned)
			{
				if (blockedByShield)
				{
					caster.jobs?.EndCurrentJob(JobCondition.Succeeded);
					Destroy();
					return;
				}
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
					flyer.hookTarget = hitThing;
				}
				else
				{
					flyer.hookTarget = caster;
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
		// Named hookTarget rather than target: PawnFlyer has its own (private) target, which the
		// publicized reference assembly exposes and which this used to shadow with a different type.
		public Thing hookTarget;

		public MoteDualAttached mote;

		public override void RespawnPawn()
		{
			Clear();
			base.RespawnPawn();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref hookTarget, "target");
		}

		public override void Tick()
		{
			base.Tick();
			TargetInfo other = (hookTarget == null || !hookTarget.Spawned) ? new TargetInfo(DestinationPos.ToIntVec3(), this.Map) : new TargetInfo(hookTarget);
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
			if (hookTarget is Pawn p)
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