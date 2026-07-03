using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class FrostivusFoodPreservationUtility
    {
        public const float ColdStorageMaxTemperature = 0f;
        public const int PickupDelayTicks = 120;

        private static readonly List<Thing> TmpInventoryFood = new List<Thing>();

        public static bool IsFrostivus(Pawn pawn)
        {
            return pawn != null && pawn.def == ApexDefsOf.APM_Mech_Frostivus;
        }

        public static bool CanDoFoodPreservation(Pawn pawn)
        {
            return IsFrostivus(pawn)
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Spawned
                && pawn.Map != null
                && pawn.inventory != null
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        public static bool IsPreservableFoodOnMap(Thing thing)
        {
            if (!IsFoodWithRotComp(thing) || !thing.Spawned)
            {
                return false;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            return rottable.Active
                && rottable.Stage == RotStage.Fresh
                && GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) > 0f;
        }

        public static bool IsInventoryFood(Thing thing)
        {
            if (!IsFoodWithRotComp(thing))
            {
                return false;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            return rottable.Stage == RotStage.Fresh;
        }

        public static Thing FirstInventoryFood(Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null)
            {
                return null;
            }

            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing thing = innerList[i];
                if (IsInventoryFood(thing))
                {
                    return thing;
                }
            }

            return null;
        }

        public static bool HasInventoryFood(Pawn pawn)
        {
            return FirstInventoryFood(pawn) != null;
        }

        public static int CountToPickUp(Pawn pawn, Thing thing)
        {
            if (pawn == null || thing == null)
            {
                return 0;
            }

            return System.Math.Min(thing.stackCount, MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing));
        }

        public static bool CanRescueFoodNow(Pawn pawn, Thing thing, bool forced = false)
        {
            return CanDoFoodPreservation(pawn)
                && IsPreservableFoodOnMap(thing)
                && !thing.IsForbidden(pawn)
                && !thing.IsBurning()
                && CountToPickUp(pawn, thing) > 0
                && pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1, null, forced);
        }

        public static bool HasRescuableFoodAvailable(Pawn pawn)
        {
            if (!CanDoFoodPreservation(pawn))
            {
                return false;
            }

            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                if (CanRescueFoodNow(pawn, things[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindColdStorageCell(Pawn pawn, Thing food, out IntVec3 foundCell)
        {
            foundCell = IntVec3.Invalid;
            if (!CanDoFoodPreservation(pawn) || !IsInventoryFood(food))
            {
                return false;
            }

            Map map = pawn.Map;
            List<SlotGroup> groups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
            for (int i = 0; i < groups.Count; i++)
            {
                SlotGroup group = groups[i];
                if (group?.parent == null || !group.parent.HaulDestinationEnabled || !group.parent.Accepts(food))
                {
                    continue;
                }

                IntVec3 bestInGroup = IntVec3.Invalid;
                float bestDistanceSquared = float.MaxValue;
                List<IntVec3> cells = group.CellsList;
                for (int j = 0; j < cells.Count; j++)
                {
                    IntVec3 cell = cells[j];
                    if (!IsColdStoreCellFor(pawn, food, cell))
                    {
                        continue;
                    }

                    float distanceSquared = pawn.Position.DistanceToSquared(cell);
                    if (!bestInGroup.IsValid || distanceSquared < bestDistanceSquared)
                    {
                        bestInGroup = cell;
                        bestDistanceSquared = distanceSquared;
                    }
                }

                if (bestInGroup.IsValid)
                {
                    foundCell = bestInGroup;
                    return true;
                }
            }

            return false;
        }

        public static bool CanReachManualUnloadCell(Pawn pawn, IntVec3 cell)
        {
            return CanDoFoodPreservation(pawn)
                && cell.IsValid
                && cell.InBounds(pawn.Map)
                && !cell.Fogged(pawn.Map)
                && pawn.CanReach(cell, PathEndMode.ClosestTouch, Danger.Deadly);
        }

        public static bool CanUnloadInventoryFoodToColdStorageCell(Pawn pawn, Thing food, IntVec3 cell)
        {
            if (!CanDoFoodPreservation(pawn) || !IsInventoryFood(food) || !cell.IsValid)
            {
                return false;
            }

            Map map = pawn.Map;
            SlotGroup slotGroup = map.haulDestinationManager.SlotGroupAt(cell);
            return slotGroup?.parent != null
                && slotGroup.parent.HaulDestinationEnabled
                && slotGroup.parent.Accepts(food)
                && cell.GetTemperature(map) < ColdStorageMaxTemperature
                && cell.GetItemStackSpaceLeftFor(map, food.def) > 0;
        }

        public static bool TryDropInventoryFoodToColdStorageCell(Pawn pawn, Thing food, IntVec3 cell)
        {
            if (!CanUnloadInventoryFoodToColdStorageCell(pawn, food, cell)
                || !pawn.inventory.innerContainer.Contains(food))
            {
                return false;
            }

            int count = System.Math.Min(food.stackCount, cell.GetItemStackSpaceLeftFor(pawn.Map, food.def));
            if (count <= 0)
            {
                return false;
            }

            bool placed = pawn.inventory.innerContainer.TryDrop(food, cell, pawn.Map, ThingPlaceMode.Direct, count, out Thing resultingThing);
            if (placed)
            {
                return true;
            }

            return pawn.inventory.innerContainer.TryDrop(
                food,
                cell,
                pawn.Map,
                ThingPlaceMode.Near,
                count,
                out resultingThing,
                null,
                dropCell => CanUnloadInventoryFoodToColdStorageCell(pawn, food, dropCell));
        }

        public static void DropAllInventoryFoodForbidden(Pawn pawn, IntVec3 cell)
        {
            if (!CanDoFoodPreservation(pawn) || !cell.IsValid || !cell.InBounds(pawn.Map))
            {
                return;
            }

            TmpInventoryFood.Clear();
            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing thing = innerList[i];
                if (IsInventoryFood(thing))
                {
                    TmpInventoryFood.Add(thing);
                }
            }

            for (int i = 0; i < TmpInventoryFood.Count; i++)
            {
                Thing thing = TmpInventoryFood[i];
                if (thing.Destroyed || !pawn.inventory.innerContainer.Contains(thing))
                {
                    continue;
                }

                pawn.inventory.innerContainer.TryDrop(
                    thing,
                    cell,
                    pawn.Map,
                    ThingPlaceMode.Near,
                    thing.stackCount,
                    out Thing resultingThing,
                    delegate (Thing placed, int count)
                    {
                        placed.SetForbidden(true, false);
                    },
                    delegate (IntVec3 dropCell)
                    {
                        return dropCell.InBounds(pawn.Map) && !dropCell.Fogged(pawn.Map);
                    });

                if (resultingThing != null)
                {
                    resultingThing.SetForbidden(true, false);
                }
            }

            TmpInventoryFood.Clear();
        }

        private static bool IsFoodWithRotComp(Thing thing)
        {
            return thing != null
                && !thing.Destroyed
                && thing.def != null
                && thing.def.EverHaulable
                && thing.def.ingestible != null
                && thing.def.IsNutritionGivingIngestible
                && !thing.def.IsCorpse
                && thing.TryGetComp<CompRottable>() != null;
        }

        private static bool IsColdStoreCellFor(Pawn pawn, Thing food, IntVec3 cell)
        {
            Map map = pawn.Map;
            return cell.InBounds(map)
                && cell.GetTemperature(map) < ColdStorageMaxTemperature
                && cell.GetItemStackSpaceLeftFor(map, food.def) > 0
                && StoreUtility.IsGoodStoreCell(cell, map, food, pawn, pawn.Faction);
        }
    }
}
