using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Just a copy of <see cref="RimWorld.CompAbilityEffect_GiveMentalState"/> without active mental state limitation
    /// </summary>
    public class CompAbilityEffect_GiveMentalState : CompAbilityEffect
    {
        public new CompProperties_AbilityGiveMentalState Props => (CompProperties_AbilityGiveMentalState)this.props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = this.Props.applyToSelf ? this.parent.pawn : (target.Thing as Pawn);
            if (pawn != null)
            {
                CompAbilityEffect_GiveMentalState.TryGiveMentalState(pawn.RaceProps.IsMechanoid ? (this.Props.stateDefForMechs ?? this.Props.stateDef) : this.Props.stateDef, pawn, this.parent.def, this.Props.durationMultiplier, this.parent.pawn, this.Props.forced);
                RestUtility.WakeUp(pawn, true);
                if (this.Props.casterEffect != null)
                {
                    Effecter effecter = this.Props.casterEffect.SpawnAttached(this.parent.pawn, this.parent.pawn.MapHeld, 1f);
                    effecter.Trigger(this.parent.pawn, null, -1);
                    effecter.Cleanup();
                }
                if (this.Props.targetEffect != null)
                {
                    Effecter effecter2 = this.Props.targetEffect.SpawnAttached(this.parent.pawn, this.parent.pawn.MapHeld, 1f);
                    effecter2.Trigger(pawn, null, -1);
                    effecter2.Cleanup();
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }
            Pawn pawn = target.Pawn;
            if (pawn != null)
            {
                if (this.Props.excludeNPCFactions && pawn.Faction != null && !pawn.Faction.IsPlayer)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "TargetBelongsToNPCFaction".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return true;
        }

        public static void TryGiveMentalState(MentalStateDef def, Pawn p, AbilityDef ability, StatDef multiplierStat, Pawn caster, bool forced = false)
        {
            if (p.mindState.mentalStateHandler.TryStartMentalState(def, null, forced, true, false, caster, false, false, ability.IsPsycast))
            {
                float num = ability.GetStatValueAbstract(StatDefOf.Ability_Duration, caster);
                if (multiplierStat != null)
                {
                    num *= p.GetStatValue(multiplierStat, true, -1);
                }
                if (num > 0f)
                {
                    p.mindState.mentalStateHandler.CurState.forceRecoverAfterTicks = num.SecondsToTicks();
                }
                p.mindState.mentalStateHandler.CurState.sourceFaction = caster.Faction;
            }
        }
    }
}
