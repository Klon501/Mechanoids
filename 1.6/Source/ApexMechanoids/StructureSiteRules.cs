using System;
using System.Collections.Generic;

namespace ApexMechanoids
{
    /// <summary>
    /// A rectangle of map cells, inclusive on both ends, in the same convention as Verse.CellRect.
    /// </summary>
    public struct SiteRect
    {
        public int minX;

        public int minZ;

        public int maxX;

        public int maxZ;

        public int Width => (maxX - minX) + 1;

        public int Height => (maxZ - minZ) + 1;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public bool Contains(int x, int z)
        {
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        public static SiteRect Empty => new SiteRect { minX = 0, minZ = 0, maxX = -1, maxZ = -1 };

        public override string ToString()
        {
            return IsEmpty ? "(empty)" : $"({minX}, {minZ})-({maxX}, {maxZ})";
        }
    }

    /// <summary>
    /// Where a starting structure will land and what ground should be under it, kept free of Verse
    /// types so it can be checked outside the game.
    ///
    /// KCSG picks its own spot when the scen part is left to scatter, and that path already refuses
    /// ground that cannot carry a structure. With <c>nearMapCenter</c> it takes the map centre
    /// unchecked instead, so whatever the map generator put there is what the complex is built on.
    /// This works out the same cells KCSG will use, so they can be made good before it builds.
    /// </summary>
    public static class StructureSiteRules
    {
        /// <summary>
        /// The cells a layout of this size will occupy when centred on the given cell, grown by
        /// margin on every side and clipped to the map.
        ///
        /// The centring arithmetic is Verse.CellRect.CenteredOn's, integer division and all: an odd
        /// size lands symmetrically and an even one leans one cell towards the low corner. Rounding
        /// the other way would prepare a strip of ground the structure never reaches and leave the
        /// far edge untouched.
        /// </summary>
        public static SiteRect Footprint(int centreX, int centreZ, int width, int height, int margin, int mapSizeX, int mapSizeZ)
        {
            if (width <= 0 || height <= 0 || mapSizeX <= 0 || mapSizeZ <= 0)
            {
                return SiteRect.Empty;
            }

            if (margin > 0)
            {
                width += margin * 2;
                height += margin * 2;
            }

            SiteRect rect = default(SiteRect);
            rect.minX = centreX - (width / 2);
            rect.minZ = centreZ - (height / 2);
            rect.maxX = (rect.minX + width) - 1;
            rect.maxZ = (rect.minZ + height) - 1;

            if (rect.minX < 0)
            {
                rect.minX = 0;
            }

            if (rect.minZ < 0)
            {
                rect.minZ = 0;
            }

            if (rect.maxX > mapSizeX - 1)
            {
                rect.maxX = mapSizeX - 1;
            }

            if (rect.maxZ > mapSizeZ - 1)
            {
                rect.maxZ = mapSizeZ - 1;
            }

            return rect.IsEmpty ? SiteRect.Empty : rect;
        }

        public static IEnumerable<(int x, int z)> Cells(SiteRect rect)
        {
            if (rect.IsEmpty)
            {
                yield break;
            }

            for (int z = rect.minZ; z <= rect.maxZ; z++)
            {
                for (int x = rect.minX; x <= rect.maxX; x++)
                {
                    yield return (x, z);
                }
            }
        }

        /// <summary>
        /// The band of cells just outside the footprint, clipped to the map. The replacement ground
        /// is sampled from here rather than from the footprint itself, because the footprint is what
        /// is being replaced and sampling it would let soft sand vote for more soft sand.
        /// </summary>
        public static IEnumerable<(int x, int z)> RingCells(SiteRect rect, int thickness, int mapSizeX, int mapSizeZ)
        {
            if (rect.IsEmpty || thickness <= 0)
            {
                yield break;
            }

            SiteRect outer = default(SiteRect);
            outer.minX = Math.Max(0, rect.minX - thickness);
            outer.minZ = Math.Max(0, rect.minZ - thickness);
            outer.maxX = Math.Min(mapSizeX - 1, rect.maxX + thickness);
            outer.maxZ = Math.Min(mapSizeZ - 1, rect.maxZ + thickness);

            foreach ((int x, int z) in Cells(outer))
            {
                if (!rect.Contains(x, z))
                {
                    yield return (x, z);
                }
            }
        }

        /// <summary>
        /// Which cells of the prepared site the structure actually took, over the cells of
        /// <see cref="Cells"/> in the same order.
        ///
        /// The site has to be prepared for the largest layout in the list, because KCSG does not pick
        /// which one it builds until afterwards. Only one gets built, so most of that ground is never
        /// reached: preparing it and leaving it prepared is what put a stripped rectangle around a
        /// smaller complex. A cell counts as taken when the layout painted its terrain or when it
        /// gained a building, and a cell that did neither is given back to the map untouched.
        /// </summary>
        public static bool[] ClaimedMask(IList<bool> terrainPainted, IList<bool> gainedBuilding)
        {
            if (terrainPainted == null
                || gainedBuilding == null
                || terrainPainted.Count != gainedBuilding.Count)
            {
                return new bool[0];
            }

            bool[] claimed = new bool[terrainPainted.Count];
            for (int i = 0; i < claimed.Length; i++)
            {
                claimed[i] = terrainPainted[i] || gainedBuilding[i];
            }
            return claimed;
        }

        /// <summary>
        /// The mask grown by an apron of <paramref name="margin"/> cells, eight-way, clipped to the
        /// site. The repaired ground is kept here and given back everywhere else, so a doorway never
        /// opens straight onto the soft sand that was repaired away.
        /// </summary>
        public static bool[] Grown(bool[] mask, int width, int height, int margin)
        {
            if (mask == null || width <= 0 || height <= 0 || mask.Length != width * height)
            {
                return new bool[0];
            }

            if (margin <= 0)
            {
                return (bool[])mask.Clone();
            }

            bool[] grown = new bool[mask.Length];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[(z * width) + x])
                    {
                        continue;
                    }

                    int minX = Math.Max(0, x - margin);
                    int maxX = Math.Min(width - 1, x + margin);
                    int minZ = Math.Max(0, z - margin);
                    int maxZ = Math.Min(height - 1, z + margin);
                    for (int nz = minZ; nz <= maxZ; nz++)
                    {
                        for (int nx = minX; nx <= maxX; nx++)
                        {
                            grown[(nz * width) + nx] = true;
                        }
                    }
                }
            }
            return grown;
        }

        /// <summary>
        /// The commonest entry in the sample, ties broken on the name so the same map is always
        /// repaired the same way. Null when there is nothing to choose from, which the caller reads
        /// as "leave the ground alone" rather than substituting a guess.
        /// </summary>
        public static string MostCommon(IList<string> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return null;
            }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < samples.Count; i++)
            {
                string sample = samples[i];
                if (string.IsNullOrEmpty(sample))
                {
                    continue;
                }

                counts.TryGetValue(sample, out int count);
                counts[sample] = count + 1;
            }

            string best = null;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> entry in counts)
            {
                if (entry.Value > bestCount
                    || (entry.Value == bestCount && string.CompareOrdinal(entry.Key, best) < 0))
                {
                    best = entry.Key;
                    bestCount = entry.Value;
                }
            }

            return best;
        }
    }
}
