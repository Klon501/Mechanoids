using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class CompProperties_FrostivusFoodPreservation : CompProperties
    {
        public CompProperties_FrostivusFoodPreservation()
        {
            compClass = typeof(CompFrostivusFoodPreservation);
        }
    }

    public class CompFrostivusFoodPreservation : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!(parent is Pawn pawn)
                || pawn.Faction != Faction.OfPlayer
                || !FrostivusFoodPreservationUtility.IsFrostivus(pawn))
            {
                yield break;
            }

            if (FrostivusFoodPreservationUtility.HasInventoryFood(pawn))
            {
                yield return MakeManualUnloadCommand(pawn);
            }

            if (FrostivusFoodPreservationUtility.HasDevouredContent(pawn))
            {
                yield return MakeReleaseDevouredCommand(pawn);
            }
        }

        private static Command_Target MakeManualUnloadCommand(Pawn pawn)
        {
            Command_Target command = new Command_Target
            {
                defaultLabel = "APM.FrostivusFoodPreservation.ManualUnload.Label".Translate(),
                defaultDesc = "APM.FrostivusFoodPreservation.ManualUnload.Desc".Translate(),
                icon = TexCommand.DropCarriedPawn,
                targetingParams = TargetingParameters.ForCell(),
                action = delegate (LocalTargetInfo target)
                {
                    if (!FrostivusFoodPreservationUtility.CanReachManualUnloadCell(pawn, target.Cell))
                    {
                        Messages.Message("APM.FrostivusFoodPreservation.ManualUnload.NoPath".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Job job = JobMaker.MakeJob(ApexDefsOf.APM_FrostivusManualUnloadFood, target.Cell);
                    job.expiryInterval = 500;
                    job.checkOverrideOnExpire = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                }
            };

            command.targetingParams.validator = delegate (TargetInfo target)
            {
                return FrostivusFoodPreservationUtility.CanReachManualUnloadCell(pawn, target.Cell);
            };

            if (!FrostivusFoodPreservationUtility.CanUseFrostivusMapCommand(pawn))
            {
                command.Disable("APM.FrostivusFoodPreservation.CommandUnavailable".Translate());
            }
            else if (!FrostivusFoodPreservationUtility.HasInventoryFood(pawn))
            {
                command.Disable("APM.FrostivusFoodPreservation.ManualUnload.NoFood".Translate());
            }

            return command;
        }

        private static Command_Target MakeReleaseDevouredCommand(Pawn pawn)
        {
            Command_Target command = new Command_Target
            {
                defaultLabel = "APM.FrostivusFoodPreservation.ReleaseDevoured.Label".Translate(),
                defaultDesc = "APM.FrostivusFoodPreservation.ReleaseDevoured.Desc".Translate(),
                icon = TexCommand.DropCarriedPawn,
                targetingParams = TargetingParameters.ForCell(),
                action = delegate (LocalTargetInfo target)
                {
                    if (!FrostivusFoodPreservationUtility.CanReachReleaseCell(pawn, target.Cell))
                    {
                        Messages.Message("APM.FrostivusFoodPreservation.ReleaseDevoured.NoPath".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Job job = JobMaker.MakeJob(ApexDefsOf.APM_FrostivusReleaseDevouredContents, target.Cell);
                    job.expiryInterval = 500;
                    job.checkOverrideOnExpire = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                }
            };

            command.targetingParams.validator = delegate (TargetInfo target)
            {
                return FrostivusFoodPreservationUtility.CanReachReleaseCell(pawn, target.Cell);
            };

            if (!FrostivusFoodPreservationUtility.CanUseFrostivusMapCommand(pawn))
            {
                command.Disable("APM.FrostivusFoodPreservation.CommandUnavailable".Translate());
            }
            else if (!FrostivusFoodPreservationUtility.HasDevouredContent(pawn))
            {
                command.Disable("APM.FrostivusFoodPreservation.ReleaseDevoured.NoContents".Translate());
            }

            return command;
        }
    }
}
