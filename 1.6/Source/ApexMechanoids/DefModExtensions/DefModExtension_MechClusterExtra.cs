using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Puts a building into mech cluster generation on this mod's own terms.
    ///
    /// Vanilla's cluster building roll has no rarity dial. A def tagged <c>MechClusterMemberGood</c>
    /// is picked with a flat <c>TryRandomElement</c> out of everything carrying that tag, and Core
    /// ships exactly one such def, so tagging alone would make a stasis container roughly a coin flip
    /// per good-building slot. This extension carries the chance instead, and
    /// <see cref="Patch_MechClusterExtraBuildings"/> applies it.
    /// </summary>
    public class DefModExtension_MechClusterExtra : DefModExtension
    {
        /// <summary>Chance per slot that this building is added to a cluster.</summary>
        public float spawnChance = 0.1f;

        /// <summary>
        /// Cluster points to that same chance, for buildings that should turn up more often the bigger
        /// the cluster is. Takes the place of <see cref="spawnChance"/> when it is set.
        /// </summary>
        public SimpleCurve spawnChanceByTotalPoints;

        /// <summary>The chance to use for a cluster worth this many points.</summary>
        public float ChanceFor(float totalPoints)
        {
            return spawnChanceByTotalPoints?.Evaluate(totalPoints) ?? spawnChance;
        }

        /// <summary>Clusters below this many total points never get one.</summary>
        public float minTotalPoints;

        /// <summary>Slots rolled per cluster. Each is an independent <see cref="spawnChance"/> roll.</summary>
        public int maxPerCluster = 1;
    }
}
