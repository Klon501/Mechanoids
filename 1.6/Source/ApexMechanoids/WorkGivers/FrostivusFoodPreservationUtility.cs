using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class FrostivusFoodPreservationUtility
    {
        public const float ColdStorageMaxTemperature = 0f;
        public const int PickupDelayTicks = 120;

        private const int OutsideStorageRescueScore = 1000000;
        private const int WarmStorageRescueScore = 100000;
        private const int RotUrgencyScoreTicks = 600000;

        private static readonly List<Thing> TmpInventoryFood = new List<Thing>();
        private static readonly List<Thing> TmpDevouredContents = new List<Thing>();
        private static Caravan cachedCaravan;
        private static int cachedCaravanTick = -1;
        private static bool cachedCaravanHasWorkingFrostivus;

        public static bool IsFrostivus(Pawn pawn)
        {
            return pawn != null && pawn.def == ApexDefsOf.APM_Mech_Frostivus;
        }

        public static bool CanDoFoodPreservation(Pawn pawn)
        {
            return CanUseFrostivusMapCommand(pawn);
        }

        public static bool CanUseFrostivusMapCommand(Pawn pawn)
        {
            return IsFrostivus(pawn)
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Spawned
                && pawn.Map != null
                && pawn.Awake()
                && pawn.inventory != null
                && HasFoodPreservationControl(pawn)
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        public static bool HasPlayerFoodPreservationControl(Pawn pawn)
        {
            return IsFrostivus(pawn)
                && pawn.Faction == Faction.OfPlayer
                && pawn.IsColonyMechPlayerControlled
                && pawn.GetOverseer() != null
                && pawn.GetMechControlGroup() != null;
        }

        public static bool HasFoodPreservationControl(Pawn pawn)
        {
            if (!IsFrostivus(pawn))
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return true;
            }

            return HasPlayerFoodPreservationControl(pawn);
        }

        public static bool IsPreservableFoodOnMap(Thing thing)
        {
            if (!IsPreservableFoodThing(thing) || !thing.Spawned)
            {
                return false;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            return rottable != null
                && rottable.Stage == RotStage.Fresh
                && !IsInColdFoodStorage(thing);
        }

        public static bool IsInventoryFood(Thing thing)
        {
            if (!IsPreservableFoodThing(thing))
            {
                return false;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            return rottable != null && rottable.Stage == RotStage.Fresh;
        }

        public static bool IsCaravanPreservableFood(Thing thing)
        {
            return IsPreservableFoodThing(thing);
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

        public static bool IsDevouredContent(Thing thing)
        {
            return thing != null
                && !thing.Destroyed
                && FrostivusUtility.HasDevouredHediff(thing);
        }

        public static Thing FirstDevouredContent(Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null)
            {
                return null;
            }

            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing thing = innerList[i];
                if (IsDevouredContent(thing))
                {
                    return thing;
                }
            }

            return null;
        }

        public static bool HasDevouredContent(Pawn pawn)
        {
            return FirstDevouredContent(pawn) != null;
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
                && (forced || ShouldAutoRescueFoodFromCurrentStorage(pawn, thing))
                && (forced || !thing.IsForbidden(pawn))
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

        public static bool TryFindBestRescuableFood(Pawn pawn, out Thing bestFood, float maxDistance = 9999f)
        {
            bestFood = null;
            if (!CanDoFoodPreservation(pawn))
            {
                return false;
            }

            int bestScore = int.MinValue;
            int bestDistance = int.MaxValue;
            int maxDistanceSquared = maxDistance >= 9999f ? int.MaxValue : (int)System.Math.Ceiling(maxDistance * maxDistance);
            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!CanRescueFoodNow(pawn, thing))
                {
                    continue;
                }

                int distance = pawn.Position.DistanceToSquared(thing.Position);
                if (distance > maxDistanceSquared)
                {
                    continue;
                }

                int score = FoodRescuePriorityScore(thing);
                if (bestFood == null || score > bestScore || (score == bestScore && distance < bestDistance))
                {
                    bestFood = thing;
                    bestScore = score;
                    bestDistance = distance;
                }
            }

            return bestFood != null;
        }

        public static Job MakeTakeFoodJob(Pawn pawn, Thing food, int expiryInterval = 500, bool forced = false, int requestedCount = -1)
        {
            if (!CanRescueFoodNow(pawn, food, forced))
            {
                return null;
            }

            int count = CountToPickUp(pawn, food);
            if (requestedCount > 0)
            {
                count = System.Math.Min(count, requestedCount);
            }

            if (count <= 0)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(ApexDefsOf.APM_FrostivusTakeFoodToInventory, food);
            job.count = count;
            job.playerForced = forced;
            job.expiryInterval = expiryInterval;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static bool TryFindBestInventoryFoodStorageJob(Pawn pawn, int expiryInterval, out Job job)
        {
            job = null;
            if (!CanDoFoodPreservation(pawn) || !HasInventoryFood(pawn))
            {
                return false;
            }

            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing food = innerList[i];
                if (!IsInventoryFood(food))
                {
                    continue;
                }

                if (TryFindColdStorageCell(pawn, food, out IntVec3 cell))
                {
                    job = JobMaker.MakeJob(ApexDefsOf.APM_FrostivusUnloadFoodToStorage, food, cell);
                    job.count = System.Math.Min(food.stackCount, cell.GetItemStackSpaceLeftFor(pawn.Map, food.def));
                    job.expiryInterval = expiryInterval;
                    job.checkOverrideOnExpire = true;
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

            return TryFindColdStorageCellAtLeastPriority(pawn, food, StoragePriority.Unstored, out foundCell);
        }

        public static bool CanReachManualUnloadCell(Pawn pawn, IntVec3 cell)
        {
            return CanUseFrostivusMapCommand(pawn)
                && cell.IsValid
                && cell.InBounds(pawn.Map)
                && !cell.Fogged(pawn.Map)
                && pawn.CanReach(cell, PathEndMode.ClosestTouch, Danger.Deadly);
        }

        public static bool CanReachReleaseCell(Pawn pawn, IntVec3 cell)
        {
            return CanReachManualUnloadCell(pawn, cell);
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
                && cell.GetItemStackSpaceLeftFor(map, food.def) > 0
                && StoreUtility.IsGoodStoreCell(cell, map, food, pawn, pawn.Faction);
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
            if (!CanUseFrostivusMapCommand(pawn) || !cell.IsValid || !cell.InBounds(pawn.Map))
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

        public static void DropAllDevouredContents(Pawn pawn, IntVec3 cell)
        {
            if (!CanUseFrostivusMapCommand(pawn) || !cell.IsValid || !cell.InBounds(pawn.Map))
            {
                return;
            }

            TmpDevouredContents.Clear();
            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing thing = innerList[i];
                if (IsDevouredContent(thing))
                {
                    TmpDevouredContents.Add(thing);
                }
            }

            for (int i = 0; i < TmpDevouredContents.Count; i++)
            {
                Thing thing = TmpDevouredContents[i];
                if (thing.Destroyed || !pawn.inventory.innerContainer.Contains(thing))
                {
                    continue;
                }

                bool dropped = pawn.inventory.innerContainer.TryDrop(
                    thing,
                    cell,
                    pawn.Map,
                    ThingPlaceMode.Near,
                    thing.stackCount,
                    out Thing resultingThing,
                    null,
                    delegate (IntVec3 dropCell)
                    {
                        return dropCell.InBounds(pawn.Map) && !dropCell.Fogged(pawn.Map);
                    });

                if (dropped)
                {
                    FrostivusUtility.RemoveDevouredHediff(resultingThing ?? thing);
                }
            }

            TmpDevouredContents.Clear();
        }

        public static bool IsRotPreservedByFrostivus(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            Pawn holderPawn = (thing.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (holderPawn == null)
            {
                return false;
            }

            if (IsFrostivus(holderPawn))
            {
                return IsInventoryFood(thing) || IsDevouredContent(thing);
            }

            if (!IsPreservableFoodThing(thing))
            {
                return false;
            }

            Caravan caravan = holderPawn.GetCaravan();
            return caravan != null && CaravanHasWorkingFrostivus(caravan);
        }

        public static bool IsFrostivusInventoryHolder(IThingHolder holder)
        {
            return IsFrostivus((holder as Pawn_InventoryTracker)?.pawn);
        }

        private static bool CaravanHasWorkingFrostivus(Caravan caravan)
        {
            if (caravan == null)
            {
                return false;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? -1;
            if (cachedCaravan == caravan && cachedCaravanTick == ticksGame)
            {
                return cachedCaravanHasWorkingFrostivus;
            }

            cachedCaravan = caravan;
            cachedCaravanTick = ticksGame;
            cachedCaravanHasWorkingFrostivus = false;

            List<Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (CanPreserveCaravanFood(pawns[i]))
                {
                    cachedCaravanHasWorkingFrostivus = true;
                    return true;
                }
            }

            return false;
        }

        private static bool CanPreserveCaravanFood(Pawn pawn)
        {
            return IsFrostivus(pawn)
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.inventory != null
                && HasFoodPreservationControlInCaravan(pawn)
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        private static bool HasFoodPreservationControlInCaravan(Pawn pawn)
        {
            if (!IsFrostivus(pawn))
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

        private static bool IsInColdFoodStorage(Thing thing)
        {
            if (thing?.Map == null || !thing.Spawned)
            {
                return false;
            }

            IHaulDestination currentDestination = StoreUtility.CurrentHaulDestinationOf(thing);
            return currentDestination != null
                && currentDestination.Accepts(thing)
                && thing.Position.GetTemperature(thing.Map) < ColdStorageMaxTemperature;
        }

        private static bool IsPreservableFoodThing(Thing thing)
        {
            if (thing == null
                || thing.Destroyed
                || thing.def == null
                || !thing.def.EverHaulable
                || thing.def.ingestible == null
                || !thing.def.IsNutritionGivingIngestible
                || thing.def.IsCorpse
                || thing.def.IsDrug
                || thing.TryGetComp<CompRottable>() == null)
            {
                return false;
            }

            return thing.def.ingestible.preferability >= FoodPreferability.RawBad
                || thing.def.IsWithinCategory(ThingCategoryDefOf.Foods)
                || thing.def.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw)
                || thing.def.IsWithinCategory(ThingCategoryDefOf.MeatRaw);
        }

        private static bool ShouldAutoRescueFoodFromCurrentStorage(Pawn pawn, Thing thing)
        {
            if (pawn?.Map == null || thing?.Map != pawn.Map)
            {
                return false;
            }

            IHaulDestination currentDestination = StoreUtility.CurrentHaulDestinationOf(thing);
            if (currentDestination == null || !currentDestination.Accepts(thing))
            {
                return true;
            }

            if (thing.Position.GetTemperature(thing.Map) < ColdStorageMaxTemperature)
            {
                return false;
            }

            StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
            return TryFindColdStorageCellAtLeastPriority(pawn, thing, currentPriority, out IntVec3 _);
        }

        private static int FoodRescuePriorityScore(Thing thing)
        {
            int score = 0;
            if (!IsInValidStorage(thing))
            {
                score += OutsideStorageRescueScore;
            }

            if (thing.Spawned && thing.Map != null && thing.Position.GetTemperature(thing.Map) >= ColdStorageMaxTemperature)
            {
                score += WarmStorageRescueScore;
            }

            CompRottable rottable = thing.TryGetComp<CompRottable>();
            if (rottable != null)
            {
                int ticksUntilRot = rottable.TicksUntilRotAtCurrentTemp;
                if (ticksUntilRot <= 0)
                {
                    score += RotUrgencyScoreTicks;
                }
                else if (ticksUntilRot < RotUrgencyScoreTicks)
                {
                    score += RotUrgencyScoreTicks - ticksUntilRot;
                }
            }

            return score;
        }

        private static bool IsInValidStorage(Thing thing)
        {
            IHaulDestination currentDestination = StoreUtility.CurrentHaulDestinationOf(thing);
            return currentDestination != null && currentDestination.Accepts(thing);
        }

        private static bool TryFindColdStorageCellAtLeastPriority(Pawn pawn, Thing food, StoragePriority minimumPriority, out IntVec3 foundCell)
        {
            foundCell = IntVec3.Invalid;
            if (pawn?.Map == null || food == null)
            {
                return false;
            }

            Map map = pawn.Map;
            List<SlotGroup> groups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
            for (int i = 0; i < groups.Count; i++)
            {
                SlotGroup group = groups[i];
                if (group?.parent == null)
                {
                    continue;
                }

                if ((int)group.Settings.Priority < (int)minimumPriority)
                {
                    break;
                }

                if (!group.parent.HaulDestinationEnabled || !group.parent.Accepts(food))
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
