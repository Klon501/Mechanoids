using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobDriver_ApexRepairMech : JobDriver
    {
        private const int BaseTicksPerHeal = 120;

        private int ticksToNextRepair;

        private Pawn Mech => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        private int TicksPerHeal => Mathf.RoundToInt(1f / pawn.GetStatValue(StatDefOf.MechRepairSpeed) * BaseTicksPerHeal);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Mech != null && pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            if (!ModLister.CheckBiotech("Mech repair"))
            {
                yield break;
            }

            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => Mech.IsAttacking());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil repair = Toils_General.WaitWith(TargetIndex.A, int.MaxValue, useProgressBar: false, maintainPosture: true, maintainSleep: true);
            repair.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A);
            repair.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
            repair.AddPreInitAction(delegate
            {
                ticksToNextRepair = TicksPerHeal;
            });
            repair.handlingFacing = true;
            repair.tickIntervalAction = delegate (int delta)
            {
                ticksToNextRepair -= delta;
                if (ticksToNextRepair <= 0)
                {
                    Need_MechEnergy energy = Mech.needs?.energy;
                    if (energy != null)
                    {
                        energy.CurLevel -= Mech.GetStatValue(StatDefOf.MechEnergyLossPerHP);
                    }

                    MechRepairUtility.RepairTick(Mech);
                    ticksToNextRepair = TicksPerHeal;
                }

                pawn.rotationTracker.FaceTarget(Mech);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.05f * delta);
                }
            };
            repair.AddFinishAction(delegate
            {
                if (Mech != null && Mech.jobs?.curJob != null)
                {
                    Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });
            repair.AddEndCondition(() => MechRepairUtility.CanRepair(Mech) ? JobCondition.Ongoing : JobCondition.Succeeded);
            repair.activeSkill = () => SkillDefOf.Crafting;
            yield return repair;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair", 0);
        }
    }
}
