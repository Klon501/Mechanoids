using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// One thing the starting structure may scatter, and what it is allowed to come out as.
    /// </summary>
    public class StructureLootLimit : IExposable
    {
        public ThingDef thingDef;

        /// <summary>Largest stack this def may be found in. Left at -1 the stack is not touched.</summary>
        public int maxStackCount = -1;

        /// <summary>
        /// The band a quality roll is pulled back into. The defaults are the whole scale, so a limit
        /// that names neither end clamps nothing and a limit that only caps a stack cannot
        /// accidentally drag quality down to Awful.
        /// </summary>
        public QualityCategory minQuality = QualityCategory.Awful;

        public QualityCategory maxQuality = QualityCategory.Legendary;

        public bool ClampsQuality =>
            minQuality != QualityCategory.Awful || maxQuality != QualityCategory.Legendary;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref maxStackCount, "maxStackCount", -1);
            Scribe_Values.Look(ref minQuality, "minQuality", QualityCategory.Awful);
            Scribe_Values.Look(ref maxQuality, "maxQuality", QualityCategory.Legendary);
        }

        public override string ToString()
        {
            return (thingDef?.defName ?? "null")
                + " maxStack=" + maxStackCount
                + " quality=" + (ClampsQuality ? minQuality + ".." + maxQuality : "unchanged");
        }
    }

    /// <summary>
    /// Trims what the ancient complex is found stocked with.
    ///
    /// KCSG decides both numbers itself and neither is configurable from a layout. A loose item cell
    /// gets <c>Rand.RangeInclusive(1, thingDef.stackLimit)</c> clamped to 75, which is how a single
    /// crate ends up holding most of a stack of spacer components; anything carrying
    /// <c>CompQuality</c> gets <c>QualityUtility.GenerateQualityBaseGen</c>, which is free to hand
    /// back Awful. There is no SymbolDef field for either, so the tidying happens here, after the
    /// structure exists.
    ///
    /// Scoped twice over. Only the map the player starts on, and only the ground
    /// <see cref="ScenPart_PrepareStructureSite"/> handed to KCSG, so the map generator's own ancient
    /// ruins keep whatever they rolled. Declaration order in the ScenarioDef is what puts this after
    /// the structure: <c>RimWorld.Scenario.PostMapGenerate</c> walks the parts in order.
    /// </summary>
    public class ScenPart_LimitStructureLoot : ScenPart
    {
        public List<StructureLootLimit> limits = new List<StructureLootLimit>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref limits, "limits", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && limits == null)
            {
                limits = new List<StructureLootLimit>();
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (limits.NullOrEmpty())
            {
                yield return "no limits configured";
                yield break;
            }

            for (int i = 0; i < limits.Count; i++)
            {
                StructureLootLimit limit = limits[i];
                if (limit?.thingDef == null)
                {
                    yield return "limit " + i + " has no thingDef";
                    continue;
                }

                if (limit.maxStackCount == 0 || limit.maxStackCount < -1)
                {
                    yield return "limit " + i + " (" + limit.thingDef.defName + ") has maxStackCount "
                        + limit.maxStackCount + ", which is neither a stack size nor -1";
                }

                if (limit.minQuality > limit.maxQuality)
                {
                    yield return "limit " + i + " (" + limit.thingDef.defName
                        + ") has minQuality above maxQuality";
                }

                if (limit.maxStackCount == -1 && !limit.ClampsQuality)
                {
                    yield return "limit " + i + " (" + limit.thingDef.defName
                        + ") caps nothing and clamps nothing";
                }
            }
        }

        public override void PostMapGenerate(Map map)
        {
            // Only the map the player starts on. Later maps generate their own structures and are
            // not what the scenario is describing.
            if (map == null || Find.GameInitData == null || limits.NullOrEmpty())
            {
                return;
            }

            if (StructureSiteCleanup.LastSiteMap != map || StructureSiteCleanup.LastSiteRect.IsEmpty)
            {
                // No site was prepared, so there is nothing this part can honestly call structure
                // loot. Better to leave the map alone than to trim the whole of it.
                return;
            }

            SiteRect site = StructureSiteCleanup.LastSiteRect;

            foreach (StructureLootLimit limit in limits)
            {
                if (limit?.thingDef == null)
                {
                    continue;
                }

                int stacksTrimmed = 0;
                int qualitiesPulled = 0;

                // Copied out first: nothing here despawns anything, but the lister is live and the
                // list of one def is short.
                List<Thing> found = new List<Thing>(map.listerThings.ThingsOfDef(limit.thingDef));
                foreach (Thing thing in found)
                {
                    if (!thing.Spawned || !site.Contains(thing.Position.x, thing.Position.z))
                    {
                        continue;
                    }

                    if (limit.maxStackCount > 0 && thing.stackCount > limit.maxStackCount)
                    {
                        thing.stackCount = limit.maxStackCount;
                        stacksTrimmed++;
                    }

                    if (PullQualityIntoBand(thing, limit))
                    {
                        qualitiesPulled++;
                    }
                }

                if (stacksTrimmed > 0 || qualitiesPulled > 0)
                {
                    Log.Message("[ApexMechanoids] Starting structure loot: " + limit
                        + " trimmed " + stacksTrimmed + " stack(s), pulled " + qualitiesPulled
                        + " quality roll(s) inside " + site + ".");
                }
            }
        }

        private static bool PullQualityIntoBand(Thing thing, StructureLootLimit limit)
        {
            if (!limit.ClampsQuality)
            {
                return false;
            }

            CompQuality comp = thing.TryGetComp<CompQuality>();
            if (comp == null)
            {
                return false;
            }

            QualityCategory rolled = comp.Quality;
            QualityCategory wanted = rolled;
            if (wanted < limit.minQuality)
            {
                wanted = limit.minQuality;
            }
            if (wanted > limit.maxQuality)
            {
                wanted = limit.maxQuality;
            }

            if (wanted == rolled)
            {
                return false;
            }

            comp.SetQuality(wanted, ArtGenerationContext.Outsider);
            return true;
        }
    }
}
