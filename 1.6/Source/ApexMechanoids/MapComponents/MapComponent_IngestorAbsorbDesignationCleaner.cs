using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class MapComponent_IngestorAbsorbDesignationCleaner : MapComponent
    {
        private const int CleanupIntervalTicks = 1000;

        private List<Corpse> markedCorpses = new List<Corpse>();
        private int ticksUntilCleanup = CleanupIntervalTicks;
        private bool cacheInitialized;

        public MapComponent_IngestorAbsorbDesignationCleaner(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildCacheFromDesignations();
        }

        public void Register(Corpse corpse)
        {
            if (!IsCacheable(corpse))
            {
                return;
            }

            if (!markedCorpses.Contains(corpse))
            {
                markedCorpses.Add(corpse);
            }
        }

        public void Unregister(Corpse corpse)
        {
            if (corpse != null)
            {
                markedCorpses.Remove(corpse);
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!cacheInitialized)
            {
                RebuildCacheFromDesignations();
            }

            ticksUntilCleanup--;
            if (ticksUntilCleanup > 0)
            {
                return;
            }

            ticksUntilCleanup = CleanupIntervalTicks;
            CleanCachedCorpses();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilCleanup, "ticksUntilCleanup", CleanupIntervalTicks);
            if (markedCorpses == null)
            {
                markedCorpses = new List<Corpse>();
            }
        }

        private void RebuildCacheFromDesignations()
        {
            if (markedCorpses == null)
            {
                markedCorpses = new List<Corpse>();
            }
            else
            {
                markedCorpses.Clear();
            }

            if (map?.designationManager != null)
            {
                foreach (Designation designation in map.designationManager.SpawnedDesignationsOfDef(ApexDefsOf.APM_IngestorAbsorbCorpse))
                {
                    if (designation.target.Thing is Corpse corpse)
                    {
                        Register(corpse);
                    }
                }
            }

            cacheInitialized = true;
        }

        private void CleanCachedCorpses()
        {
            if (markedCorpses == null || map?.designationManager == null)
            {
                return;
            }

            for (int i = markedCorpses.Count - 1; i >= 0; i--)
            {
                Corpse corpse = markedCorpses[i];
                if (!IsCacheable(corpse))
                {
                    markedCorpses.RemoveAt(i);
                    continue;
                }

                Designation designation = map.designationManager.DesignationOn(corpse, ApexDefsOf.APM_IngestorAbsorbCorpse);
                if (designation == null)
                {
                    markedCorpses.RemoveAt(i);
                    continue;
                }

                if (IngestorCorpseProcessingUtility.ShouldClearAbsorbDesignation(corpse))
                {
                    map.designationManager.RemoveDesignation(designation);
                    markedCorpses.RemoveAt(i);
                }
            }
        }

        private bool IsCacheable(Corpse corpse)
        {
            return corpse != null && !corpse.Destroyed && corpse.Spawned && corpse.Map == map;
        }
    }
}
