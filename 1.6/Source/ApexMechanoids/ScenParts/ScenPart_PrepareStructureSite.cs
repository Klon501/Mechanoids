using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Makes the ground the starting structure lands on fit to build on, before KCSG builds on it.
    ///
    /// KCSG's <c>ScenPart_AddStartingStructure</c> has two placement paths. Left to scatter, it
    /// searches the map for a spot and rejects any that fails <c>GenGrid.SupportsStructureType</c>
    /// with the Heavy affordance, or that contains a cell a wall could not be built on. With
    /// <c>nearMapCenter</c> set, which is what the Buried Legacy scenario uses, it skips that search
    /// entirely and takes <c>map.Center</c> unchecked. Whatever the map generator left there is what
    /// the ancient complex is founded on, and nothing puts it right afterwards: buildings are placed
    /// through <c>GenSpawn.Spawn</c>, which does not test terrain, and terrain the layout does not
    /// explicitly floor over is left as generated. Soft sand, marsh and mud all carry Light rather
    /// than Heavy, so the complex ends up standing on ground the player cannot rebuild on.
    ///
    /// This part runs first, works out the same cells KCSG is about to use, and makes them good:
    /// unsuitable ground is replaced with whatever buildable natural terrain the surrounding map
    /// already uses, and blockers are taken out of the way. It changes nothing when KCSG is doing
    /// its own scattering, since that path already guarantees the site.
    ///
    /// Declaration order in the ScenarioDef is what makes this run first:
    /// <c>RimWorld.Scenario.PostMapGenerate</c> walks <c>AllParts</c> in order.
    /// </summary>
    public class ScenPart_PrepareStructureSite : ScenPart
    {
        // KCSG's own scen part, matched by def name so this needs no reference to their assembly.
        private const string StructureScenPartDefName = "VFEC_AddStartingStructure";

        // Cells of prepared ground around the structure itself. The layout's own walls sit on the
        // edge of its footprint, so a little apron keeps the doorways and the ground a player first
        // walks on from opening straight onto sand. It is also how much of the repaired ground
        // survives the cleanup; past it the map gets its own terrain back.
        public int margin = 2;

        // How far out the replacement ground is sampled from. Wide enough to see past a single
        // patch of whatever is being replaced, narrow enough to still be the local biome.
        public int sampleRingThickness = 6;

        // Natural rock inside the footprint, and the mountain roof over it. KCSG keeps natural rock
        // when it cleans, because layouts are allowed to be cut into rock; this one is not.
        public bool clearNaturalRock = true;

        // Plants, chunks and filth inside the footprint. KCSG only cleans the cells its roof grid
        // marks, so anything standing in the apron or in an unroofed part of the layout survives.
        public bool clearBlockingThings = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref margin, "margin", 2);
            Scribe_Values.Look(ref sampleRingThickness, "sampleRingThickness", 6);
            Scribe_Values.Look(ref clearNaturalRock, "clearNaturalRock", defaultValue: true);
            Scribe_Values.Look(ref clearBlockingThings, "clearBlockingThings", defaultValue: true);
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            if (map == null)
            {
                return;
            }

            try
            {
                PrepareSite(map);
            }
            catch (Exception ex)
            {
                // Map generation is still in flight here. A structure on poor ground is a far
                // smaller problem than a half-generated map, so this never rethrows.
                Log.Warning("[Apex Mechanoids] Could not prepare the starting structure site: " + ex);
            }
        }

        private void PrepareSite(Map map)
        {
            ScenPart structurePart = FindStructurePart();
            if (structurePart == null)
            {
                return;
            }

            if (!ReadsMapCentre(structurePart))
            {
                // KCSG is choosing its own spot, and that path already refuses unsuitable ground.
                return;
            }

            if (!TryGetLayoutSize(structurePart, out int width, out int height))
            {
                return;
            }

            SiteRect rect = StructureSiteRules.Footprint(
                map.Center.x, map.Center.z, width, height, margin, map.Size.x, map.Size.z);
            if (rect.IsEmpty)
            {
                return;
            }

            TerrainDef replacement = ChooseReplacementTerrain(map, rect);

            int unbuildableBefore = CountUnbuildable(map, rect, out string groundBefore);
            int count = rect.Width * rect.Height;
            TerrainDef[] originalTerrain = new TerrainDef[count];
            bool[] repairedCells = new bool[count];
            int repaired = 0;
            int index = 0;
            foreach ((int x, int z) in StructureSiteRules.Cells(rect))
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                originalTerrain[index] = map.terrainGrid.TerrainAt(cell);
                if (replacement != null && RepairCell(map, cell, replacement))
                {
                    repairedCells[index] = true;
                    repaired++;
                }
                index++;
            }

            // Nothing is cleared here. The site is prepared for the largest layout because KCSG has
            // not chosen one yet, and clearing all of that is what left a bare rectangle around a
            // smaller complex. StructureSiteCleanup does it once the structure exists and its real
            // extent can be read, and hands back the ground the structure never reached.
            StructureSiteCleanup.Arm(
                map, rect, originalTerrain, repairedCells, margin, clearNaturalRock, clearBlockingThings);

            // The after count is what says the site is actually fit to build on, rather than that
            // the pass ran. It is the line to read in Player.log when a start goes wrong.
            Log.Message(
                $"[Apex Mechanoids] Starting structure site {rect}: {unbuildableBefore} of {count} "
                + $"cell(s) could not carry a structure{groundBefore}, {repaired} replaced with {replacement?.defName ?? "nothing"}, "
                + $"{CountUnbuildable(map, rect, out _)} still unbuildable.");
        }

        /// <summary>
        /// The ground to repair with: whatever buildable natural terrain the map already uses around
        /// the site, so a desert complex is founded on sand and a temperate one on soil. Falls back
        /// to the commonest such terrain on the whole map when the ring gives nothing, and to leaving
        /// the ground alone when the map has none at all.
        /// </summary>
        private TerrainDef ChooseReplacementTerrain(Map map, SiteRect rect)
        {
            List<string> samples = new List<string>();
            foreach ((int x, int z) in StructureSiteRules.RingCells(rect, sampleRingThickness, map.Size.x, map.Size.z))
            {
                CollectSample(map, new IntVec3(x, 0, z), samples);
            }

            string picked = StructureSiteRules.MostCommon(samples);
            if (picked == null)
            {
                samples.Clear();
                foreach (IntVec3 cell in map.AllCells)
                {
                    CollectSample(map, cell, samples);
                }

                picked = StructureSiteRules.MostCommon(samples);
            }

            return picked == null ? null : DefDatabase<TerrainDef>.GetNamedSilentFail(picked);
        }

        private static void CollectSample(Map map, IntVec3 cell, List<string> samples)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain != null && terrain.natural && CarriesStructures(terrain))
            {
                samples.Add(terrain.defName);
            }
        }

        private static bool CarriesStructures(TerrainDef terrain)
        {
            return terrain.affordances != null && terrain.affordances.Contains(TerrainAffordanceDefOf.Heavy);
        }

        /// <summary>
        /// How many cells of the site cannot carry a structure, and which grounds those are. The
        /// breakdown is what tells the client whether a bad start was soft sand, marsh or water.
        /// </summary>
        private static int CountUnbuildable(Map map, SiteRect rect, out string breakdown)
        {
            int count = 0;
            Dictionary<string, int> byTerrain = new Dictionary<string, int>();
            foreach ((int x, int z) in StructureSiteRules.Cells(rect))
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(new IntVec3(x, 0, z));
                if (terrain != null && CarriesStructures(terrain))
                {
                    continue;
                }

                count++;
                string name = terrain?.defName ?? "none";
                byTerrain.TryGetValue(name, out int seen);
                byTerrain[name] = seen + 1;
            }

            breakdown = byTerrain.Count == 0
                ? string.Empty
                : " (" + string.Join(", ", byTerrain.Select(entry => $"{entry.Key} x{entry.Value}")) + ")";
            return count;
        }

        private static bool RepairCell(Map map, IntVec3 cell, TerrainDef replacement)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain == null || CarriesStructures(terrain))
            {
                return false;
            }

            map.terrainGrid.SetTerrain(cell, replacement);
            return true;
        }

        private static ScenPart FindStructurePart()
        {
            Scenario scenario = Find.Scenario;
            if (scenario == null)
            {
                return null;
            }

            foreach (ScenPart part in scenario.AllParts)
            {
                if (part?.def != null && part.def.defName == StructureScenPartDefName)
                {
                    return part;
                }
            }

            return null;
        }

        /// <summary>
        /// True when KCSG will drop the structure on the map centre without checking the ground.
        /// </summary>
        private static bool ReadsMapCentre(ScenPart structurePart)
        {
            FieldInfo field = structurePart.GetType().GetField("nearMapCenter", BindingFlags.Public | BindingFlags.Instance);
            return field != null && field.GetValue(structurePart) is bool near && near;
        }

        /// <summary>
        /// The largest footprint any of the layouts KCSG might pick will need. KCSG chooses one at
        /// random at generation time, so the site is prepared for the biggest of them.
        /// </summary>
        private static bool TryGetLayoutSize(ScenPart structurePart, out int width, out int height)
        {
            width = 0;
            height = 0;

            FieldInfo field = structurePart.GetType().GetField("chooseFrom", BindingFlags.Public | BindingFlags.Instance);
            if (!(field?.GetValue(structurePart) is IList layouts))
            {
                return false;
            }

            foreach (object layout in layouts)
            {
                if (layout == null)
                {
                    continue;
                }

                PropertyInfo sizesProperty = layout.GetType().GetProperty("Sizes", BindingFlags.Public | BindingFlags.Instance);
                if (!(sizesProperty?.GetValue(layout) is IntVec2 size))
                {
                    continue;
                }

                int layoutWidth = size.x;
                int layoutHeight = size.z;

                FieldInfo rotationField = layout.GetType().GetField("randomRotation", BindingFlags.Public | BindingFlags.Instance);
                if (rotationField?.GetValue(layout) is bool rotates && rotates)
                {
                    // A quarter turn swaps the two, so the site has to be square enough for either.
                    layoutWidth = Math.Max(size.x, size.z);
                    layoutHeight = layoutWidth;
                }

                width = Math.Max(width, layoutWidth);
                height = Math.Max(height, layoutHeight);
            }

            return width > 0 && height > 0;
        }
    }
}
