using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobDriver_RepairAegisShields : JobDriver
    {
        private const TargetIndex MechInd = TargetIndex.A;
        private const TargetIndex SteelInd = TargetIndex.B;
        private const int RepairWorkTicks = 120;

        private int steelDelivered = 0;
        private int ticksToNextRepair = 0;

        private Pawn Mech => (Pawn)job.GetTarget(MechInd).Thing;
        private Thing CurrentSteel => job.GetTarget(SteelInd).Thing;
        private CompAegis MechComp => Mech?.TryGetComp<CompAegis>();
        private int SteelRequired => job.count;
        private bool HasEnoughSteel => steelDelivered >= SteelRequired;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed))
                return false;

            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref steelDelivered, "steelDelivered", 0);
            Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair", 0);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(MechInd);
            this.FailOnForbidden(MechInd);
            this.FailOn(() => Mech.IsAttacking());

            Toil initializeSteel = new Toil();
            initializeSteel.initAction = delegate
            {
                steelDelivered = MechComp.steelDeliveredForRepair;
            };
            initializeSteel.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return initializeSteel;

            Toil repairLabel = Toils_General.Label();

            Toil haulLoop = Toils_General.Label();
            yield return haulLoop;

            yield return Toils_Jump.JumpIf(repairLabel, () => HasEnoughSteel);

            Toil extractNextTarget = new Toil();
            extractNextTarget.initAction = delegate
            {
                if (job.targetQueueA != null && job.targetQueueA.Count > 0)
                {
                    LocalTargetInfo nextTarget = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                    job.SetTarget(SteelInd, nextTarget);
                }
                else
                {
                    if (!HasEnoughSteel)
                    {
                        Messages.Message(
                            "APM.NotEnoughSteel".Translate(steelDelivered, SteelRequired),
                            Mech,
                            MessageTypeDefOf.NegativeEvent
                        );
                        EndJobWith(JobCondition.Incompletable);
                    }
                }
            };
            extractNextTarget.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return extractNextTarget;

            yield return Toils_Goto.GotoThing(SteelInd, PathEndMode.ClosestTouch)
                .FailOnDestroyedNullOrForbidden(SteelInd);

            Toil startCarrySteel = new Toil();
            startCarrySteel.initAction = delegate
            {
                if (CurrentSteel == null || CurrentSteel.Destroyed)
                {
                    return;
                }

                int amountNeeded = SteelRequired - steelDelivered;
                int amountToTake = Mathf.Min(CurrentSteel.stackCount, amountNeeded, pawn.carryTracker.MaxStackSpaceEver(ThingDefOf.Steel));

                Thing takenSteel = CurrentSteel.SplitOff(amountToTake);
                if (takenSteel == null)
                {
                    return;
                }

                if (CurrentSteel.stackCount <= 0)
                {
                    CurrentSteel.Destroy();
                }

                pawn.carryTracker.TryStartCarry(takenSteel);
            };
            startCarrySteel.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return startCarrySteel;

            yield return Toils_Goto.GotoThing(MechInd, PathEndMode.Touch);

            Toil deliverSteel = new Toil();
            deliverSteel.initAction = delegate
            {
                if (pawn.carryTracker.CarriedThing != null)
                {
                    Thing carriedSteel = pawn.carryTracker.CarriedThing;
                    int amountDelivered = carriedSteel.stackCount;

                    steelDelivered += amountDelivered;

                    if (MechComp != null)
                    {
                        MechComp.steelDeliveredForRepair = steelDelivered;
                    }

                    carriedSteel.Destroy();
                    pawn.carryTracker.innerContainer.Remove(carriedSteel);
                }
            };
            deliverSteel.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return deliverSteel;

            yield return Toils_Jump.Jump(haulLoop);

            yield return repairLabel;

            yield return Toils_Goto.GotoThing(MechInd, PathEndMode.Touch);

            Toil repairToil = Toils_General.WaitWith(MechInd, int.MaxValue, useProgressBar: false, maintainPosture: true);
            repairToil.WithEffect(EffecterDefOf.MechRepairing, MechInd);
            repairToil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
            repairToil.AddPreInitAction(delegate
            {
                ticksToNextRepair = RepairWorkTicks;
            });

            repairToil.tickIntervalAction = delegate (int delta)
            {
                ticksToNextRepair -= delta;

                if (ticksToNextRepair <= 0)
                {
                    RepairShields();
                    ticksToNextRepair = RepairWorkTicks;
                }

                pawn.rotationTracker.FaceTarget(Mech);

                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.1f * delta);
                }
            };

            repairToil.AddEndCondition(() => AreShieldsFullyRepaired() ? JobCondition.Succeeded : JobCondition.Ongoing);

            repairToil.AddFinishAction(delegate
            {
                MechComp.steelDeliveredForRepair = 0;
                steelDelivered = 0;

                if (Mech.jobs?.curJob != null && Mech.jobs.curJob.def == JobDefOf.Wait_Combat)
                {
                    Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }

                Messages.Message(
                    "APM.ShieldsFullyRepaired".Translate(Mech.LabelShort),
                    Mech,
                    MessageTypeDefOf.PositiveEvent
                );
            });

            repairToil.activeSkill = () => SkillDefOf.Crafting;

            yield return repairToil;
        }

        private void RepairShields()
        {
            if (MechComp == null) return;

            var shieldParts = Mech.RaceProps.body.AllParts.Where(part => part.def == ApexDefsOf.APM_AegisShield);

            foreach (var shieldPart in shieldParts)
            {
                if (ShieldMissing(Mech, shieldPart))
                {
                    Mech.health.RemoveHediff(Mech.health.hediffSet.GetMissingPartFor(shieldPart));
                    FleckMaker.ThrowMetaIcon(Mech.Position, Mech.Map, FleckDefOf.HealingCross);
                    Messages.Message(
                        "APM.ShieldRestored".Translate(Mech.LabelShort, shieldPart.Label),
                        Mech,
                        MessageTypeDefOf.PositiveEvent,
                        historical: false
                    );
                    return;
                }

                if (ShieldDamaged(Mech, shieldPart))
                {
                    var injuries = Mech.health.hediffSet.hediffs
                        .OfType<Hediff_Injury>()
                        .Where(h => h.Part == shieldPart)
                        .ToList();

                    if (injuries.Any())
                    {
                        injuries.First().Heal(2f);
                        FleckMaker.ThrowMetaIcon(Mech.Position, Mech.Map, FleckDefOf.HealingCross);
                        return;
                    }
                }
            }
        }

        private bool AreShieldsFullyRepaired()
        {
            return !ShieldsMissing(Mech) && !ShieldsDamaged(Mech);
        }

        private bool ShieldsMissing(Pawn pawn)
        {
            var shieldParts = pawn.RaceProps.body.AllParts.Where(part => part.def == ApexDefsOf.APM_AegisShield);
            return shieldParts.Any(shieldPart => pawn.health.hediffSet.PartIsMissing(shieldPart));
        }

        private bool ShieldsDamaged(Pawn pawn)
        {
            var shieldParts = Utils.GetNonMissingBodyParts(pawn, ApexDefsOf.APM_AegisShield);
            var injuredParts = pawn.health.hediffSet.GetInjuredParts();

            return shieldParts.Any(shieldPart => injuredParts.Contains(shieldPart));
        }

        private bool ShieldDamaged(Pawn pawn, BodyPartRecord shield)
        {
            return pawn.health.hediffSet.GetInjuredParts().Contains(shield);
        }

        private bool ShieldMissing(Pawn pawn, BodyPartRecord shield)
        {
            return pawn.health.hediffSet.PartIsMissing(shield);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            if (MechComp != null)
            {
                steelDelivered = MechComp.steelDeliveredForRepair;
            }
        }
    }
}