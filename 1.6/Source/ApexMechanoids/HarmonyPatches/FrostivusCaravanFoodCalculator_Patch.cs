using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), nameof(DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood), new Type[]
    {
        typeof(List<TransferableOneWay>),
        typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode),
        typeof(Faction),
        typeof(WorldPath),
        typeof(float),
        typeof(int)
    })]
    internal static class FrostivusDaysWorthOfFoodTransferables_Patch
    {
        public static void Prefix(ref List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            FrostivusCaravanFoodCalculatorUtility.TryAugmentTransferablesWithSelectedFrostivusFood(ref transferables, ignoreInventory);
        }
    }

    internal static class FrostivusCaravanFoodCalculatorUtility
    {
        private enum TransferableFoodContextMode
        {
            ToDestination,
            LeftAfterTransfer
        }

        private static List<TransferableOneWay> activeRotContextTransferables;
        private static IgnorePawnsInventoryMode activeRotContextIgnoreInventory;
        private static TransferableFoodContextMode activeRotContextMode;
        private static bool activeRotContextPreserveAnyCaravanFood;
        private static readonly List<ThingCount> TmpTradeThingCounts = new List<ThingCount>();

        public static void TryAugmentTransferablesWithSelectedFrostivusFood(ref List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            if (transferables.NullOrEmpty())
            {
                return;
            }

            List<TransferableOneWay> augmentedTransferables = null;

            for (int i = 0; i < transferables.Count; i++)
            {
                TransferableOneWay transferable = transferables[i];
                if (!(transferable?.AnyThing is Pawn))
                {
                    continue;
                }

                int selectedCount = Math.Min(transferable.CountToTransfer, transferable.things.Count);
                for (int j = 0; j < selectedCount; j++)
                {
                    Pawn pawn = transferable.things[j] as Pawn;
                    if (!ShouldExposeFrostivusInventoryFoodToCalculator(pawn, ignoreInventory))
                    {
                        continue;
                    }

                    List<Thing> inventory = pawn.inventory.innerContainer.InnerListForReading;
                    for (int k = 0; k < inventory.Count; k++)
                    {
                        Thing food = inventory[k];
                        if (!FrostivusFoodPreservationUtility.IsInventoryFood(food))
                        {
                            continue;
                        }

                        int countToExpose = food.stackCount - CountAlreadySelected(transferables, food);
                        if (countToExpose <= 0)
                        {
                            continue;
                        }

                        if (augmentedTransferables == null)
                        {
                            augmentedTransferables = new List<TransferableOneWay>(transferables);
                        }

                        augmentedTransferables.Add(MakeFoodTransferable(food, countToExpose));
                    }
                }
            }

            if (augmentedTransferables != null)
            {
                transferables = augmentedTransferables;
            }
        }

        private static bool ShouldExposeFrostivusInventoryFoodToCalculator(Pawn pawn, IgnorePawnsInventoryMode ignoreInventory)
        {
            return FrostivusFoodPreservationUtility.IsFrostivus(pawn)
                && pawn.inventory?.innerContainer != null
                && !InventoryCalculatorsUtility.ShouldIgnoreInventoryOf(pawn, ignoreInventory);
        }

        public static void BeginTransferableRotPreservationContext(List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            BeginTransferableRotPreservationContext(transferables, ignoreInventory, TransferableFoodContextMode.ToDestination);
        }

        public static void BeginTransferableRotPreservationContextLeftAfterTransfer(List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            BeginTransferableRotPreservationContext(transferables, ignoreInventory, TransferableFoodContextMode.LeftAfterTransfer);
        }

        public static void EndTransferableRotPreservationContext()
        {
            activeRotContextTransferables = null;
            activeRotContextPreserveAnyCaravanFood = false;
            TmpTradeThingCounts.Clear();
        }

        public static bool IsFoodPreservedInTransferableRotContext(Thing thing)
        {
            if (!FrostivusFoodPreservationUtility.IsCaravanPreservableFood(thing))
            {
                return false;
            }

            if (activeRotContextPreserveAnyCaravanFood)
            {
                return true;
            }

            return activeRotContextTransferables != null && ThingIsSelectedInActiveRotContext(thing);
        }

        public static void BeginTradeableRotPreservationContext(List<Thing> allCurrentThings, List<Tradeable> tradeables)
        {
            activeRotContextTransferables = null;
            activeRotContextPreserveAnyCaravanFood = false;
            TmpTradeThingCounts.Clear();

            if (allCurrentThings.NullOrEmpty() || tradeables == null)
            {
                return;
            }

            TransferableUtility.SimulateTradeableTransfer(allCurrentThings, tradeables, TmpTradeThingCounts);
            for (int i = 0; i < TmpTradeThingCounts.Count; i++)
            {
                ThingCount thingCount = TmpTradeThingCounts[i];
                if (thingCount.Count > 0 && CanPreservePlannedCaravanFood(thingCount.Thing as Pawn))
                {
                    activeRotContextPreserveAnyCaravanFood = true;
                    break;
                }
            }

            TmpTradeThingCounts.Clear();
        }

        private static void BeginTransferableRotPreservationContext(List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory, TransferableFoodContextMode mode)
        {
            activeRotContextPreserveAnyCaravanFood = false;
            if (transferables.NullOrEmpty() || !HasSelectedWorkingFrostivus(transferables, mode))
            {
                activeRotContextTransferables = null;
                return;
            }

            activeRotContextTransferables = transferables;
            activeRotContextIgnoreInventory = ignoreInventory;
            activeRotContextMode = mode;
        }

        private static bool HasSelectedWorkingFrostivus(List<TransferableOneWay> transferables, TransferableFoodContextMode mode)
        {
            for (int i = 0; i < transferables.Count; i++)
            {
                TransferableOneWay transferable = transferables[i];
                if (!(transferable?.AnyThing is Pawn))
                {
                    continue;
                }

                int startIndex;
                int endIndex;
                GetSelectedPawnRange(transferable, mode, out startIndex, out endIndex);
                for (int j = startIndex; j < endIndex; j++)
                {
                    if (CanPreservePlannedCaravanFood(transferable.things[j] as Pawn))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CanPreservePlannedCaravanFood(Pawn pawn)
        {
            return FrostivusFoodPreservationUtility.IsFrostivus(pawn)
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.inventory != null
                && HasPlannedCaravanFoodPreservationControl(pawn)
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        private static bool HasPlannedCaravanFoodPreservationControl(Pawn pawn)
        {
            if (!FrostivusFoodPreservationUtility.IsFrostivus(pawn))
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return true;
            }

            return pawn.OverseerSubject?.State == OverseerSubjectState.Overseen
                && pawn.GetOverseer() != null
                && pawn.GetMechControlGroup() != null;
        }

        private static bool ThingIsSelectedInActiveRotContext(Thing thing)
        {
            for (int i = 0; i < activeRotContextTransferables.Count; i++)
            {
                TransferableOneWay transferable = activeRotContextTransferables[i];
                if (transferable == null || !transferable.HasAnyThing)
                {
                    continue;
                }

                if (transferable.AnyThing is Pawn)
                {
                    int startIndex;
                    int endIndex;
                    GetSelectedPawnRange(transferable, activeRotContextMode, out startIndex, out endIndex);
                    for (int j = startIndex; j < endIndex; j++)
                    {
                        Pawn pawn = transferable.things[j] as Pawn;
                        if (pawn?.inventory?.innerContainer != null
                            && !InventoryCalculatorsUtility.ShouldIgnoreInventoryOf(pawn, activeRotContextIgnoreInventory)
                            && pawn.inventory.innerContainer.Contains(thing))
                        {
                            return true;
                        }
                    }
                }
                else if (TransferableHasSelectedThing(transferable, thing, activeRotContextMode))
                {
                    return true;
                }
            }

            return false;
        }

        private static void GetSelectedPawnRange(TransferableOneWay transferable, TransferableFoodContextMode mode, out int startIndex, out int endIndex)
        {
            if (mode == TransferableFoodContextMode.LeftAfterTransfer)
            {
                startIndex = Math.Min(transferable.CountToTransfer, transferable.things.Count);
                endIndex = transferable.things.Count;
                return;
            }

            startIndex = 0;
            endIndex = Math.Min(transferable.CountToTransfer, transferable.things.Count);
        }

        private static bool TransferableHasSelectedThing(TransferableOneWay transferable, Thing thing, TransferableFoodContextMode mode)
        {
            int selectedCount = mode == TransferableFoodContextMode.LeftAfterTransfer
                ? transferable.MaxCount - transferable.CountToTransfer
                : transferable.CountToTransfer;

            if (selectedCount <= 0)
            {
                return false;
            }

            for (int i = 0; i < transferable.things.Count; i++)
            {
                if (transferable.things[i] == thing)
                {
                    return true;
                }
            }

            return false;
        }

        private static TransferableOneWay MakeFoodTransferable(Thing food, int count)
        {
            TransferableOneWay transferable = new TransferableOneWay
            {
                interactive = false
            };
            transferable.things.Add(food);
            transferable.ForceToDestination(count);
            return transferable;
        }

        private static int CountAlreadySelected(List<TransferableOneWay> transferables, Thing thing)
        {
            int count = 0;
            for (int i = 0; i < transferables.Count; i++)
            {
                TransferableOneWay transferable = transferables[i];
                if (transferable == null || transferable.AnyThing is Pawn || !transferable.HasAnyThing || transferable.CountToTransfer <= 0)
                {
                    continue;
                }

                int remaining = transferable.CountToTransfer;
                for (int j = 0; j < transferable.things.Count && remaining > 0; j++)
                {
                    Thing transferableThing = transferable.things[j];
                    int taken = Math.Min(transferableThing.stackCount, remaining);
                    if (transferableThing == thing)
                    {
                        count += taken;
                    }

                    remaining -= taken;
                }
            }

            return count;
        }
    }

    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRot), new Type[]
    {
        typeof(List<TransferableOneWay>),
        typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode),
        typeof(WorldPath),
        typeof(float),
        typeof(int)
    })]
    internal static class FrostivusDaysUntilRotTransferables_Patch
    {
        public static void Prefix(List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            FrostivusCaravanFoodCalculatorUtility.BeginTransferableRotPreservationContext(transferables, ignoreInventory);
        }

        public static void Finalizer()
        {
            FrostivusCaravanFoodCalculatorUtility.EndTransferableRotPreservationContext();
        }
    }

    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRotLeftAfterTransfer), new Type[]
    {
        typeof(List<TransferableOneWay>),
        typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode),
        typeof(WorldPath),
        typeof(float),
        typeof(int)
    })]
    internal static class FrostivusDaysUntilRotLeftAfterTransfer_Patch
    {
        public static void Prefix(List<TransferableOneWay> transferables, IgnorePawnsInventoryMode ignoreInventory)
        {
            FrostivusCaravanFoodCalculatorUtility.BeginTransferableRotPreservationContextLeftAfterTransfer(transferables, ignoreInventory);
        }

        public static void Finalizer()
        {
            FrostivusCaravanFoodCalculatorUtility.EndTransferableRotPreservationContext();
        }
    }

    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRotLeftAfterTradeableTransfer), new Type[]
    {
        typeof(List<Thing>),
        typeof(List<Tradeable>),
        typeof(PlanetTile),
        typeof(IgnorePawnsInventoryMode)
    })]
    internal static class FrostivusDaysUntilRotTradeableTransfer_Patch
    {
        public static void Prefix(List<Thing> allCurrentThings, List<Tradeable> tradeables)
        {
            FrostivusCaravanFoodCalculatorUtility.BeginTradeableRotPreservationContext(allCurrentThings, tradeables);
        }

        public static void Finalizer()
        {
            FrostivusCaravanFoodCalculatorUtility.EndTransferableRotPreservationContext();
        }
    }
}
