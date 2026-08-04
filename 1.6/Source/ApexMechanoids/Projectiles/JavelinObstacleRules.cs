namespace ApexMechanoids
{
    /// <summary>
    /// What a javelin missile is allowed to run into on its way to the target, kept free of Verse
    /// types so the rule can be checked outside the game.
    ///
    /// Vanilla's free-intercept roll is the wrong shape for a guided missile: it rolls once per
    /// crossed cell against every pawn and every piece of cover, which is survivable for a bullet on
    /// a straight line but not for a seeker that curves across half the battlefield to get behind its
    /// target. The missile therefore ignores that roll entirely and uses this rule instead - it flies
    /// over pawns, chunks and low cover, and is stopped only by something it physically cannot pass.
    /// </summary>
    public static class JavelinObstacleRules
    {
        /// <summary>
        /// Whether a thing standing in a cell the missile just crossed stops it there.
        /// </summary>
        /// <param name="blocksFully">The thing fills its cell completely - a wall, a closed door, natural rock.</param>
        /// <param name="isOpenDoor">Open doors fill their cell on paper but are flown straight through.</param>
        /// <param name="isIntendedTarget">The thing the missile was fired at, which the normal impact path handles.</param>
        /// <param name="tilesFromLaunch">How far the missile has travelled from the launcher.</param>
        /// <param name="armAfterTiles">Grace distance out of the tube, mirroring vanilla's own intercept dead zone around the shooter.</param>
        public static bool Blocks(bool blocksFully, bool isOpenDoor, bool isIntendedTarget, float tilesFromLaunch, float armAfterTiles)
        {
            // Whatever the missile was fired at is handled by the normal impact path, which is what
            // applies the escalating warhead. Stopping it here would rob it of that.
            if (isIntendedTarget)
            {
                return false;
            }

            // Pawns, chunks, sandbags, low rock - the missile flies over all of it. Only something
            // that fills its cell outright is in the way, and an open door is not.
            if (!blocksFully || isOpenDoor)
            {
                return false;
            }

            // A missile clearing the tube next to a wall would otherwise detonate on the launcher's
            // own doorframe. Vanilla gives shooters the same grace via its intercept dead zone.
            return tilesFromLaunch >= armAfterTiles;
        }
    }
}
