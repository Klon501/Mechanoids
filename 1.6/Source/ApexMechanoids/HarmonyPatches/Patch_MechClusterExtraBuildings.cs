using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Adds this mod's opt-in buildings to a generated mech cluster.
    ///
    /// <c>MechClusterGenerator.GetBuildingDefsForCluster</c> is the one place that decides what a
    /// cluster is made of; everything after it is placement. Appending to its result means the
    /// container goes through the same sketch placement, the same drop pods and the same
    /// <c>SetFaction(Faction.OfMechanoids)</c> pass as a vanilla cluster building, so nothing here
    /// has to reimplement cluster spawning.
    ///
    /// Rarity and the points floor come from <see cref="DefModExtension_MechClusterExtra"/> on the
    /// building def, so they stay tunable in XML.
    /// </summary>
    [HarmonyPatch(typeof(MechClusterGenerator), "GetBuildingDefsForCluster")]
    internal static class Patch_MechClusterExtraBuildings
    {
        private static List<ThingDef> cachedDefs;

        private static void Postfix(List<ThingDef> __result, float points, float? totalPoints)
        {
            if (__result == null)
            {
                return;
            }

            // Cluster budget before the pawn share was taken out of it: the same number vanilla
            // compares minMechClusterPoints against.
            float budget = totalPoints ?? points;

            foreach (ThingDef def in ExtraBuildingDefs())
            {
                DefModExtension_MechClusterExtra parms = def.GetModExtension<DefModExtension_MechClusterExtra>();
                if (budget < parms.minTotalPoints)
                {
                    continue;
                }

                for (int i = 0; i < parms.maxPerCluster; i++)
                {
                    if (!Rand.Chance(parms.spawnChance))
                    {
                        break;
                    }

                    __result.Add(def);
                }
            }
        }

        private static List<ThingDef> ExtraBuildingDefs()
        {
            return cachedDefs ?? (cachedDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where((ThingDef def) => def.HasModExtension<DefModExtension_MechClusterExtra>())
                .ToList());
        }
    }
}
