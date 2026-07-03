using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class CompAbilityEffect_CryptoSwallow : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;

            Pawn caster = parent.pawn;

            if (!(target.Thing is Pawn) && !(target.Thing is Corpse))
                return false;

            if (MassUtility.WillBeOverEncumberedAfterPickingUp(caster, target.Thing, 1))
            {
                if (throwMessages && caster.Faction == Faction.OfPlayer)
                    Messages.Message("TooHeavy".Translate(), caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Thing thing = target.Thing;

            if (thing == null || thing.Destroyed)
                return;

            if (thing is Pawn targetPawn && !targetPawn.Dead)
                HealthUtility.DamageUntilDowned(targetPawn, allowBleedingWounds: false);

            thing.DeSpawnOrDeselect();
            if (caster.inventory.innerContainer.TryAdd(thing, 1) > 0)
            {
                if (thing is Pawn swallowedPawn)
                    FrostivusUtility.ApplyDevouredHediff(swallowedPawn);
                else if (thing is Corpse corpse && corpse.InnerPawn != null)
                    FrostivusUtility.ApplyDevouredHediff(corpse.InnerPawn);
            }
        }
    }

    public class CompProperties_CryptoSwallow : CompProperties_AbilityEffect
    {
        public CompProperties_CryptoSwallow()
        {
            compClass = typeof(CompAbilityEffect_CryptoSwallow);
        }
    }
}
