using System;

namespace ApexMechanoids
{
    /// <summary>
    /// How hard an Aegis swings a shield that has been chewed up.
    ///
    /// Kept free of Verse so it can be run outside the game. The arithmetic is small but it has to
    /// undo a number the game has already applied, which is exactly the kind of thing worth pinning
    /// down away from a running colony.
    /// </summary>
    public static class AegisShieldDamageRules
    {
        /// <summary>
        /// The floor <c>VerbProperties.GetDamageFactorFor</c> puts under a tool marked
        /// <c>ensureLinkedBodyPartsGroupAlwaysUsable</c>. Hardcoded there, so it is hardcoded here.
        /// </summary>
        public const float VanillaAlwaysUsableFloor = 0.4f;

        /// <summary>
        /// The multiplier to apply to what the game worked out, to move a tool off vanilla's floor and
        /// onto ours.
        ///
        /// Everything else in that factor - life stage, the melee damage stat - is left alone, which
        /// is why this rescales rather than recomputing: the efficiency term is the only one that
        /// should change, and it is the only one this touches.
        ///
        /// A shield in one piece is efficiency 1 and comes back 1, so an undamaged Aegis hits exactly
        /// as hard as it did.
        /// </summary>
        public static float FloorAdjustment(float naturalEfficiency, float destroyedFloor)
        {
            float vanilla = Math.Max(naturalEfficiency, VanillaAlwaysUsableFloor);
            float ours = Math.Max(naturalEfficiency, Clamp01(destroyedFloor));
            return ours / vanilla;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            return value > 1f ? 1f : value;
        }
    }
}
