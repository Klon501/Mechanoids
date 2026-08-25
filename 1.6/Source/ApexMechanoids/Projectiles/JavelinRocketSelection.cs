namespace ApexMechanoids
{
    /// <summary>
    /// Which rocket a javelin is allowed to load and whether it can pay for the next one, kept free
    /// of Verse types so the rule can be checked outside the game.
    ///
    /// The launcher carries a uranium magazine. The basic rocket costs nothing and is always
    /// available, so the mech is never left unable to shoot; every other warhead is drawn from that
    /// magazine and falls back to basic once it runs dry.
    ///
    /// That applies to every launcher, the player's or not. A raid has no stockpile to haul from, so
    /// rather than exempt it from the cost it is handed one rocket type and a finite magazine when it
    /// spawns: it opens with the warhead it rolled, spends the magazine, and fights on basic rockets
    /// from there. Exempting it instead is what let an enemy javelin field high explosive warheads
    /// indefinitely for nothing.
    /// </summary>
    public static class JavelinRocketSelection
    {
        /// <summary>
        /// Whether a rocket type may be loaded into this launcher at all.
        /// </summary>
        /// <param name="playerOnlyRocket">The type is withheld from launchers the player does not own.</param>
        /// <param name="launcherIsPlayerFaction">The launcher belongs to the player.</param>
        public static bool CanSelect(bool playerOnlyRocket, bool launcherIsPlayerFaction)
        {
            return !playerOnlyRocket || launcherIsPlayerFaction;
        }

        /// <summary>
        /// Whether firing this rocket draws on the launcher's uranium magazine. Only the cost decides
        /// it; who owns the launcher does not.
        /// </summary>
        public static bool ChargesFor(int uraniumCost)
        {
            return uraniumCost > 0;
        }

        /// <summary>
        /// Whether the launcher can fire this rocket right now. A launcher that cannot falls back to
        /// the basic rocket rather than refusing the shot.
        /// </summary>
        public static bool CanFire(int uraniumCost, float uraniumHeld)
        {
            return !ChargesFor(uraniumCost) || uraniumHeld >= uraniumCost;
        }

        /// <summary>
        /// How much uranium a launcher the player does not own spawns holding: enough for
        /// <paramref name="charges"/> shots of the warhead it rolled, and no more than its magazine
        /// can take.
        /// </summary>
        /// <param name="uraniumCost">Per shot cost of the rolled warhead. Free warheads need no stock.</param>
        /// <param name="charges">How many paid shots the launcher is stocked for.</param>
        /// <param name="capacity">The magazine's capacity.</param>
        public static float StartingUranium(int uraniumCost, int charges, float capacity)
        {
            if (uraniumCost <= 0 || charges <= 0 || capacity <= 0f)
            {
                return 0f;
            }

            float wanted = uraniumCost * charges;
            return wanted > capacity ? capacity : wanted;
        }

        /// <summary>
        /// Which rocket a launcher the player does not own spawns loaded with.
        /// </summary>
        /// <param name="playerOnlyByIndex">The player-only flag of every rocket type, in def order.</param>
        /// <param name="roll">A roll in [0, 1).</param>
        /// <returns>An index into <paramref name="playerOnlyByIndex"/>, or 0 if nothing is eligible.</returns>
        public static int RandomEnemyRocket(bool[] playerOnlyByIndex, float roll)
        {
            if (playerOnlyByIndex == null)
            {
                return 0;
            }

            int eligible = 0;
            for (int i = 0; i < playerOnlyByIndex.Length; i++)
            {
                if (!playerOnlyByIndex[i])
                {
                    eligible++;
                }
            }

            if (eligible == 0)
            {
                return 0;
            }

            // Clamped rather than wrapped, so a roll that comes back as exactly 1 lands on the last
            // eligible rocket instead of running off the end of the roster.
            int pick = (int)(roll * eligible);
            if (pick < 0)
            {
                pick = 0;
            }
            else if (pick >= eligible)
            {
                pick = eligible - 1;
            }

            for (int i = 0; i < playerOnlyByIndex.Length; i++)
            {
                if (playerOnlyByIndex[i])
                {
                    continue;
                }

                if (pick == 0)
                {
                    return i;
                }

                pick--;
            }

            return 0;
        }
    }
}
