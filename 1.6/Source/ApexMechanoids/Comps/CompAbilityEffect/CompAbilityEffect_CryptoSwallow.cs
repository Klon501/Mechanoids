using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class CompAbilityEffect_CryptoSwallow : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            AcceptanceReport report = CanSwallowTarget(parent?.pawn, target);
            if (!report.Accepted && throwMessages)
            {
                ShowRejectMessage(parent?.pawn, report);
            }

            return report.Accepted;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return CanSwallowTarget(parent?.pawn, target).Accepted;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent?.pawn;
            Thing thing = target.Thing;

            AcceptanceReport report = CanSwallowTarget(caster, target);
            if (!report.Accepted)
            {
                ShowRejectMessage(caster, report);
                return;
            }

            base.Apply(target, dest);

            Map map = thing.MapHeld;
            IntVec3 position = thing.PositionHeld;
            Rot4 rotation = thing.Rotation;
            bool wasSpawned = thing.Spawned;

            thing.DeSpawnOrDeselect();
            if (caster.inventory.innerContainer.TryAdd(thing, canMergeWithExistingStacks: false))
            {
                FrostivusUtility.ApplyDevouredHediff(thing);
                return;
            }

            if (wasSpawned && map != null && position.IsValid && !thing.Destroyed && thing.holdingOwner == null)
            {
                GenSpawn.Spawn(thing, position, map, rotation);
            }
        }

        public static AcceptanceReport CanSwallowTarget(Pawn caster, LocalTargetInfo target)
        {
            Thing thing = target.Thing;
            Pawn targetPawn = FrostivusUtility.ContainedPawn(thing);

            if (caster == null
                || thing == null
                || thing.Destroyed
                || targetPawn == null)
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.InvalidTarget".Translate();
            }

            if (targetPawn == caster)
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.Self".Translate();
            }

            if (!FrostivusFoodPreservationUtility.CanUseFrostivusMapCommand(caster))
            {
                return "APM.FrostivusFoodPreservation.CommandUnavailable".Translate();
            }

            if (!(thing is Pawn) && !(thing is Corpse))
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.InvalidTarget".Translate();
            }

            if (!thing.Spawned || thing.Map != caster.Map)
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.InvalidTarget".Translate();
            }

            if (caster.inventory?.innerContainer == null || caster.inventory.innerContainer.Contains(thing))
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.InvalidTarget".Translate();
            }

            if (!CanSwallowThingForCaster(caster, thing, targetPawn))
            {
                return "APM.FrostivusFoodPreservation.CryptoSwallow.InvalidPawn".Translate();
            }

            if (MassUtility.WillBeOverEncumberedAfterPickingUp(caster, thing, 1))
            {
                return "TooHeavy".Translate();
            }

            return AcceptanceReport.WasAccepted;
        }

        private static bool CanSwallowThingForCaster(Pawn caster, Thing thing, Pawn targetPawn)
        {
            if (targetPawn == null
                || targetPawn.RaceProps?.IsMechanoid == true
                || targetPawn.RaceProps?.Humanlike != true)
            {
                return false;
            }

            if (thing is Corpse)
            {
                return true;
            }

            if (targetPawn.Destroyed)
            {
                return false;
            }

            if (targetPawn.Dead || targetPawn.Downed)
            {
                return true;
            }

            return targetPawn.Faction == Faction.OfPlayer
                || targetPawn.IsPrisonerOfColony
                || targetPawn.IsSlaveOfColony;
        }

        private static void ShowRejectMessage(Pawn caster, AcceptanceReport report)
        {
            if (caster?.Faction == Faction.OfPlayer && !report.Reason.NullOrEmpty())
            {
                Messages.Message(report.Reason, caster, MessageTypeDefOf.RejectInput, false);
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

    public class JobDriver_CryptoSwallow : JobDriver_CastAbility
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo target = job.GetTarget(TargetIndex.A);
            return CompAbilityEffect_CryptoSwallow.CanSwallowTarget(pawn, target).Accepted
                && pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            this.FailOn(() => !CompAbilityEffect_CryptoSwallow.CanSwallowTarget(pawn, job.GetTarget(TargetIndex.A)).Accepted);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
        }
    }
}
