using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilitySameFactionTarget : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySameFactionTarget()
        {
            compClass = typeof(CompAbilityEffect_SameFactionTarget);
        }
    }

    public class CompAbilityEffect_SameFactionTarget : CompAbilityEffect
    {
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && IsSameFactionTarget(target);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return IsAllowedByTargetParams(target) && IsSameFactionTarget(target);
        }

        private bool IsSameFactionTarget(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            return caster != null
                && targetPawn != null
                && caster.Faction != null
                && targetPawn.Faction == caster.Faction
                && !targetPawn.HostileTo(caster);
        }

        private bool IsAllowedByTargetParams(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster != null && targetPawn == caster && !parent.def.verbProperties.targetParams.canTargetSelf)
            {
                return false;
            }

            return true;
        }
    }
}
