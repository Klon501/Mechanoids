using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
	public class CompProperties_AbilityMoteDualAttachedOnTarget : CompProperties_AbilityEffect
	{
		public ThingDef moteDef;

		public List<ThingDef> moteDefs;

		public float scale = 1f;

		public int preCastTicks;

		public CompProperties_AbilityMoteDualAttachedOnTarget()
		{
			compClass = typeof(CompAbilityEffect_MoteDualAttachedOnTarget);
		}
	}
	public class CompAbilityEffect_MoteDualAttachedOnTarget : CompAbilityEffect
	{
		public new CompProperties_AbilityMoteDualAttachedOnTarget Props => (CompProperties_AbilityMoteDualAttachedOnTarget)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			if (Props.preCastTicks <= 0)
			{
				Props.sound?.PlayOneShot(new TargetInfo(target.Cell, parent.pawn.Map));
				SpawnAll(target);
			}
		}

		public override IEnumerable<PreCastAction> GetPreCastActions()
		{
			if (Props.preCastTicks > 0)
			{
				yield return new PreCastAction
				{
					action = delegate (LocalTargetInfo t, LocalTargetInfo d)
					{
						SpawnAll(t);
						Props.sound?.PlayOneShot(new TargetInfo(t.Cell, parent.pawn.Map));
					},
					ticksAwayFromCast = Props.preCastTicks
				};
			}
		}

		private void SpawnAll(LocalTargetInfo target)
		{
			if (!Props.moteDefs.NullOrEmpty())
			{
				for (int i = 0; i < Props.moteDefs.Count; i++)
				{
					SpawnMote(target, Props.moteDefs[i]);
				}
			}
			else
			{
				SpawnMote(target, Props.moteDef);
			}
		}

		private void SpawnMote(LocalTargetInfo target, ThingDef def)
		{
			MoteMaker.MakeInteractionOverlay(def, parent.pawn, target.ToTargetInfo(parent.pawn.Map));
		}
	}
}
