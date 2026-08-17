using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// The second half of preparing the starting structure's site, run once KCSG has built and the
    /// structure's real extent is known.
    ///
    /// The first half cannot know that extent. It runs before KCSG picks which of the five layouts
    /// to build, so it has to prepare enough ground for the largest of them; on a map where the
    /// smallest is chosen that is roughly three times the area the complex ever covers. Clearing and
    /// repairing all of it left a stripped rectangle of bare ground standing out around the base,
    /// which is what the client photographed.
    ///
    /// So the site is prepared wide and then given back. Whatever the structure took keeps its
    /// prepared ground and gets cleaned properly; everything else goes back to the map generator's
    /// terrain with its plants, chunks and rock untouched.
    /// </summary>
    public static class StructureSiteCleanup
    {
        private static Map armedMap;

        private static SiteRect armedRect;

        private static TerrainDef[] terrainBeforeRepair;

        private static TerrainDef[] terrainAtHandover;

        private static bool[] repairedByUs;

        private static bool[] hadEdifice;

        private static int keepMargin;

        private static bool clearNaturalRock;

        private static bool clearBlockingThings;

        /// <summary>
        /// Records the site as the structure is about to be built on it. Everything the cleanup
        /// decides is a comparison against this, so it is taken after the ground is repaired and
        /// before KCSG places a single thing.
        /// </summary>
        public static void Arm(
            Map map,
            SiteRect rect,
            TerrainDef[] originalTerrain,
            bool[] repaired,
            int margin,
            bool clearRock,
            bool clearThings)
        {
            armedMap = map;
            armedRect = rect;
            terrainBeforeRepair = originalTerrain;
            repairedByUs = repaired;
            keepMargin = margin;
            clearNaturalRock = clearRock;
            clearBlockingThings = clearThings;

            int count = rect.Width * rect.Height;
            terrainAtHandover = new TerrainDef[count];
            hadEdifice = new bool[count];

            int i = 0;
            foreach ((int x, int z) in StructureSiteRules.Cells(rect))
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                terrainAtHandover[i] = map.terrainGrid.TerrainAt(cell);
                hadEdifice[i] = cell.GetEdifice(map) != null;
                i++;
            }
        }

        public static void Disarm()
        {
            armedMap = null;
            armedRect = SiteRect.Empty;
            terrainBeforeRepair = null;
            terrainAtHandover = null;
            repairedByUs = null;
            hadEdifice = null;
        }

        /// <summary>
        /// Cleans what the structure took and gives the rest of the site back to the map. Runs from a
        /// postfix on KCSG's own scen part, so it cannot happen before the structure exists.
        /// </summary>
        public static void Run(Map map)
        {
            if (map == null || map != armedMap || armedRect.IsEmpty || terrainAtHandover == null)
            {
                return;
            }

            try
            {
                Clean(map);
            }
            catch (System.Exception ex)
            {
                // Map generation is still in flight. A rectangle of cleared ground is a far smaller
                // problem than a half-generated map, so this never rethrows.
                Log.Warning("[Apex Mechanoids] Could not tidy the starting structure site: " + ex);
            }
            finally
            {
                Disarm();
            }
        }

        private static void Clean(Map map)
        {
            int count = armedRect.Width * armedRect.Height;
            bool[] painted = new bool[count];
            bool[] gainedBuilding = new bool[count];

            List<IntVec3> cells = new List<IntVec3>(count);
            int i = 0;
            foreach ((int x, int z) in StructureSiteRules.Cells(armedRect))
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                cells.Add(cell);
                painted[i] = map.terrainGrid.TerrainAt(cell) != terrainAtHandover[i];
                gainedBuilding[i] = !hadEdifice[i] && cell.GetEdifice(map) != null;
                i++;
            }

            bool[] claimed = StructureSiteRules.ClaimedMask(painted, gainedBuilding);
            bool[] keep = StructureSiteRules.Grown(claimed, armedRect.Width, armedRect.Height, keepMargin);

            int cleared = 0;
            int givenBack = 0;
            for (int index = 0; index < cells.Count; index++)
            {
                if (claimed[index])
                {
                    cleared += ClearCell(map, cells[index]);
                    continue;
                }

                // Ground repaired for a structure that never reached this cell. The map's own terrain
                // goes back, soft sand and all: nothing is going to be built here.
                if (keep.Length == cells.Count && !keep[index] && repairedByUs != null && repairedByUs[index])
                {
                    map.terrainGrid.SetTerrain(cells[index], terrainBeforeRepair[index]);
                    givenBack++;
                }
            }

            int claimedCount = 0;
            for (int index = 0; index < claimed.Length; index++)
            {
                if (claimed[index])
                {
                    claimedCount++;
                }
            }

            Log.Message(
                $"[Apex Mechanoids] Starting structure took {claimedCount} of {count} prepared cell(s); "
                + $"{cleared} blocker(s) removed inside it, {givenBack} repaired cell(s) given back to the map.");
        }

        /// <summary>
        /// Plants, chunks, filth and natural rock standing where the structure is. KCSG only cleans
        /// the cells its roof grid marks, so an unroofed courtyard keeps whatever grew there, and it
        /// keeps natural rock everywhere because layouts are allowed to be cut into rock. This one is
        /// not: it is a built complex, and the mountain roof over removed rock would collapse on it.
        /// </summary>
        private static int ClearCell(Map map, IntVec3 cell)
        {
            int removed = 0;
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing is Pawn)
                {
                    continue;
                }

                if (thing.def.category == ThingCategory.Building
                    && thing.def.building != null
                    && thing.def.building.isNaturalRock)
                {
                    if (!clearNaturalRock)
                    {
                        continue;
                    }

                    thing.DeSpawn();
                    removed++;
                    continue;
                }

                if (!clearBlockingThings)
                {
                    continue;
                }

                if (thing.def.category == ThingCategory.Plant
                    || thing.def.category == ThingCategory.Filth
                    || (thing.def.thingCategories != null && thing.def.thingCategories.Contains(ThingCategoryDefOf.Chunks)))
                {
                    thing.DeSpawn();
                    removed++;
                }
            }

            if (clearNaturalRock)
            {
                RoofDef roof = map.roofGrid.RoofAt(cell);
                if (roof != null && roof.isNatural)
                {
                    map.roofGrid.SetRoof(cell, null);
                }
            }

            return removed;
        }
    }
}
