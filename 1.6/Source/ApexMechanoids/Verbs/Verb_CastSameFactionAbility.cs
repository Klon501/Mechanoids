using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class Verb_CastSameFactionAbility : Verb_CastAbility
    {
        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return !CasterHasCloseMeleeThreat() && IsSameFactionTarget(targ) && base.CanHitTarget(targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return !CasterHasCloseMeleeThreat() && IsSameFactionTarget(targ) && base.CanHitTargetFrom(root, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (CasterHasCloseMeleeThreat() || !IsSameFactionTarget(target))
            {
                if (target.IsValid && showMessages && ability?.pawn != null)
                {
                    Messages.Message(
                        "CannotUseAbility".Translate(ability.def.label) + ": " + "AbilityCannotHitTarget".Translate(),
                        new LookTargets(ability.pawn, target.ToTargetInfo(ability.pawn.Map)),
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }
                return false;
            }

            return base.ValidateTarget(target, showMessages);
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return !CasterHasCloseMeleeThreat()
                && IsSameFactionTarget(castTarg)
                && base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        private bool CasterHasCloseMeleeThreat()
        {
            Pawn caster = ability?.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return false;
            }

            if (caster.mindState != null && caster.mindState.MeleeThreatStillThreat)
            {
                return true;
            }

            return GenAI.EnemyIsNear(caster, 2.9f, out _, meleeOnly: true, requireLos: true);
        }

        private bool IsSameFactionTarget(LocalTargetInfo target)
        {
            Pawn caster = ability?.pawn;
            if (caster == null || caster.Faction == null)
            {
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn != null)
            {
                return IsSameFactionPawn(caster, targetPawn);
            }

            MechShield shield = target.Thing as MechShield;
            return shield != null && IsSameFactionShieldTarget(caster, shield);
        }

        private static bool IsSameFactionPawn(Pawn caster, Pawn targetPawn)
        {
            return targetPawn != null
                && targetPawn.Faction == caster.Faction
                && !targetPawn.HostileTo(caster);
        }

        private static bool IsSameFactionShieldTarget(Pawn caster, MechShield shield)
        {
            if (shield.Destroyed || !shield.Spawned || shield.Map != caster.Map)
            {
                return false;
            }

            var thingList = shield.Position.GetThingList(shield.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Pawn targetPawn = thingList[i] as Pawn;
                if (IsSameFactionPawn(caster, targetPawn) && shield.IsTargeting(targetPawn))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
