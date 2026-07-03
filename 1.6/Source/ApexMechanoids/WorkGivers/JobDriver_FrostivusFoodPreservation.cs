using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobDriver_FrostivusUnloadFoodToStorage : JobDriver
    {
        private const TargetIndex FoodIndex = TargetIndex.A;
        private const TargetIndex CellIndex = TargetIndex.B;

        private Thing Food => job.GetTarget(FoodIndex).Thing;
        private IntVec3 StorageCell => job.GetTarget(CellIndex).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return StorageCell.IsValid;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn));
            this.FailOn(() => !FrostivusFoodPreservationUtility.IsInventoryFood(Food));
            this.FailOn(() => !StorageCell.IsValid || !StorageCell.InBounds(Map));
            this.FailOn(() => !FrostivusFoodPreservationUtility.CanUnloadInventoryFoodToColdStorageCell(pawn, Food, StorageCell));

            yield return Toils_Goto.GotoCell(CellIndex, PathEndMode.ClosestTouch);

            Toil unload = ToilMaker.MakeToil("FrostivusUnloadFoodToStorage");
            unload.initAction = delegate
            {
                if (!FrostivusFoodPreservationUtility.TryDropInventoryFoodToColdStorageCell(pawn, Food, StorageCell))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            unload.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return unload;
        }
    }

    public class JobDriver_FrostivusManualUnloadFood : JobDriver
    {
        private const TargetIndex CellIndex = TargetIndex.A;

        private IntVec3 DropCell => job.GetTarget(CellIndex).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return DropCell.IsValid;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !FrostivusFoodPreservationUtility.CanDoFoodPreservation(pawn));
            this.FailOn(() => !FrostivusFoodPreservationUtility.HasInventoryFood(pawn));
            this.FailOn(() => !DropCell.IsValid || !DropCell.InBounds(Map));

            yield return Toils_Goto.GotoCell(CellIndex, PathEndMode.ClosestTouch);

            Toil unload = ToilMaker.MakeToil("FrostivusManualUnloadFood");
            unload.initAction = delegate
            {
                FrostivusFoodPreservationUtility.DropAllInventoryFoodForbidden(pawn, DropCell);
            };
            unload.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return unload;
        }
    }
}
