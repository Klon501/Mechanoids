using System.Collections.Generic;
using System.Linq;

namespace ApexMechanoids
{
    /// <summary>
    /// Which of a stasis container's options are worth handing a colony of a given strength.
    ///
    /// Kept free of Verse so it can be run outside the game: the interesting part is the arithmetic,
    /// and the def lookups and curve evaluation around it are not what goes wrong.
    /// </summary>
    /// <summary>What a container loading from a save should do about its occupant.</summary>
    public enum LoadedOccupancy
    {
        /// <summary>It was opened before the save. It stays open.</summary>
        StayEmpty,

        /// <summary>Its occupant is still a kind this game knows about.</summary>
        KeepKind,

        /// <summary>It had an occupant whose kind left with the mod that added it. Roll another.</summary>
        Reroll,
    }

    public static class MechContainerStockRules
    {
        /// <summary>
        /// Emptiness is a saved fact, not something to be worked out again from the kind. The kind
        /// outlives the occupant in the save, so deciding from it alone hands a container that was
        /// already opened a fresh mech every time the game is loaded.
        /// </summary>
        public static LoadedOccupancy Resolve(bool savedEmpty, bool kindResolved)
        {
            if (savedEmpty)
            {
                return LoadedOccupancy.StayEmpty;
            }
            return kindResolved ? LoadedOccupancy.KeepKind : LoadedOccupancy.Reroll;
        }

        /// <summary>
        /// The options that fall inside the band, as indices into <paramref name="combatPowers"/>.
        ///
        /// The band is a preference rather than a promise. A container never opens onto nothing, so
        /// when the band is empty this falls back to the nearest edge of it: the weakest options when
        /// everything outclasses the colony, the strongest allowed when the colony has outgrown them.
        /// </summary>
        public static List<int> IndicesWithinBand(IList<float> combatPowers, float floor, float cap)
        {
            List<int> all = Enumerable.Range(0, combatPowers?.Count ?? 0).ToList();
            if (all.Count == 0)
            {
                return all;
            }

            List<int> withinCap = all.Where((int i) => combatPowers[i] <= cap).ToList();
            if (withinCap.Count == 0)
            {
                float weakest = all.Min((int i) => combatPowers[i]);
                return all.Where((int i) => combatPowers[i] <= weakest).ToList();
            }

            List<int> withinBand = withinCap.Where((int i) => combatPowers[i] >= floor).ToList();
            if (withinBand.Count > 0)
            {
                return withinBand;
            }

            float strongest = withinCap.Max((int i) => combatPowers[i]);
            return withinCap.Where((int i) => combatPowers[i] >= strongest).ToList();
        }
    }
}
