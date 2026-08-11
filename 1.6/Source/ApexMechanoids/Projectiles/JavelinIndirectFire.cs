namespace ApexMechanoids
{
    /// <summary>
    /// Whether a launcher is allowed to take a shot its line of sight does not support, kept free of
    /// Verse types so the rule can be checked outside the game.
    ///
    /// This is the geometric half of firing from behind cover: it decides that the launcher is the
    /// one behind something, rather than the target being buried inside a mountain. The other half -
    /// whether the missile's curve actually gets around that cover - is answered by flying the shot
    /// with JavelinMissileGuidance.SamplePath, which needs a map and so lives on the verb.
    /// </summary>
    public static class JavelinIndirectFire
    {
        /// <param name="anyBlocker">The line to the target is obstructed at all.</param>
        /// <param name="farthestBlockerTiles">Distance from the launcher to the last obstruction on that line.</param>
        /// <param name="maxBlockerTiles">How far out cover may sit and still count as the launcher's own cover.</param>
        public static bool AllowsBlockedShot(bool anyBlocker, float farthestBlockerTiles, float maxBlockerTiles)
        {
            // A clear line is an ordinary shot and is not this rule's business.
            if (!anyBlocker || maxBlockerTiles <= 0f)
            {
                return false;
            }

            // Every obstruction has to be close enough to be the launcher's own cover. One wall
            // halfway down the line means the target is behind terrain, not that the launcher is
            // tucked in behind a rock.
            return farthestBlockerTiles <= maxBlockerTiles;
        }
    }
}
