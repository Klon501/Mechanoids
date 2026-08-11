using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class FloatMenuOptionProvider_PickUp_Frostivus : FloatMenuOptionProvider
    {
        public override bool Drafted => true;

        public override bool Undrafted => true;

        public override bool Multiselect => false;

        public override bool MechanoidCanDo => true;

        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
        {
            return base.SelectedPawnValid(pawn, context)
                && pawn.Faction == Faction.OfPlayer
                && FrostivusFoodPreservationUtility.IsFrostivus(pawn);
        }

        public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
        {
            return false;
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn frostivus = context.FirstSelectedPawn;
            if (!FrostivusFoodPreservationUtility.IsPreservableFoodOnMap(clickedThing))
            {
                yield break;
            }

            if (!FrostivusFoodPreservationUtility.CanUseFrostivusMapCommand(frostivus))
            {
                yield return new FloatMenuOption(
                    "CannotPickUp".Translate(clickedThing.Label, clickedThing) + ": " + "APM.FrostivusFoodPreservation.CommandUnavailable".Translate(),
                    null);
                yield break;
            }

            if (!frostivus.CanReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotPickUp".Translate(clickedThing.Label, clickedThing) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }

            int maxCount = FrostivusFoodPreservationUtility.CountToPickUp(frostivus, clickedThing);
            if (maxCount <= 0 || MassUtility.WillBeOverEncumberedAfterPickingUp(frostivus, clickedThing, 1))
            {
                yield return new FloatMenuOption("CannotPickUp".Translate(clickedThing.Label, clickedThing) + ": " + "TooHeavy".Translate().CapitalizeFirst(), null);
                yield break;
            }

            if (maxCount < clickedThing.stackCount)
            {
                yield return new FloatMenuOption("CannotPickUpAll".Translate(clickedThing.Label, clickedThing) + ": " + "TooHeavy".Translate(), null);
            }
            else
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUpAll".Translate(clickedThing.Label, clickedThing), delegate
                {
                    TryOrderTakeFood(frostivus, clickedThing, clickedThing.stackCount);
                }, MenuOptionPriority.High), frostivus, clickedThing, "ReservedBy");
            }

            if (clickedThing.stackCount > 1 && maxCount > 1)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUpSome".Translate(clickedThing.LabelNoCount, clickedThing), delegate
                {
                    int to = Mathf.Min(maxCount, clickedThing.stackCount);
                    Dialog_Slider window = new Dialog_Slider("PickUpCount".Translate(clickedThing.LabelNoCount, clickedThing), 1, to, delegate (int count)
                    {
                        TryOrderTakeFood(frostivus, clickedThing, count);
                    });
                    Find.WindowStack.Add(window);
                }, MenuOptionPriority.High), frostivus, clickedThing, "ReservedBy");
            }
        }

        private static void TryOrderTakeFood(Pawn frostivus, Thing food, int count)
        {
            if (food == null || food.Destroyed)
            {
                return;
            }

            food.SetForbidden(false, false);
            Job job = FrostivusFoodPreservationUtility.MakeTakeFoodJob(frostivus, food, 500, true, count);
            if (job == null)
            {
                Messages.Message("CannotPickUp".Translate(food.Label, food), food, MessageTypeDefOf.RejectInput, false);
                return;
            }

            frostivus.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
        }
    }
}
