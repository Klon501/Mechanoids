namespace ApexMechanoids
{
    /// <summary>
    /// When an opened stasis container stops being the container it was.
    ///
    /// A container that was placed with something already sealed inside is described by that: the
    /// Buried Legacy ones are a dead mechanitor's own garrison, still sealed, still turning over
    /// their preservation cycle. Take the mech out and every word of that is false, and the player
    /// is left with a crate whose name and description belong to a moment that has passed. Emptied,
    /// it is a plain stasis container and should say so.
    ///
    /// Kept free of Verse so the table can be run outside the game. The swap itself is a few lines
    /// of spawning; which containers it may touch, and when, is the part worth being sure of.
    /// </summary>
    public static class MechContainerResetRules
    {
        /// <summary>
        /// Whether a container should now become the def it names as what it is once emptied.
        ///
        /// Emptiness is the trigger rather than the act of opening, so a container the colony has
        /// walked a mech back into is left alone until that mech comes out again, and a container
        /// found already empty in a save from before this existed is caught on its next tick.
        /// </summary>
        /// <param name="namesAReplacement">The def says what this container becomes once emptied.</param>
        /// <param name="alreadyTheReplacement">It is already that def, so there is nothing to do.</param>
        /// <param name="spawned">It is standing on a map. A minified one in a caravan is not.</param>
        /// <param name="empty">Nothing sealed inside and nothing walked in.</param>
        public static bool ShouldBecomeEmptiedDef(bool namesAReplacement, bool alreadyTheReplacement, bool spawned, bool empty)
        {
            return namesAReplacement && !alreadyTheReplacement && spawned && empty;
        }
    }
}
