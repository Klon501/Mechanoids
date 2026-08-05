namespace ApexMechanoids
{
    /// <summary>
    /// Which rocket a javelin is allowed to load and whether it can pay for the next one, kept free
    /// of Verse types so the rule can be checked outside the game.
    ///
    /// The launcher carries a uranium magazine. The basic rocket costs nothing and is always
    /// available, so the mech is never left unable to shoot; every other warhead is drawn from that
    /// magazine and falls back to basic once it runs dry. A launcher that is not the player's pays
    /// nothing at all - a raid has no colony stockpile to haul from - and is instead handed one
    /// rocket type when it spawns and keeps it.
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
        /// Whether firing this rocket draws on the launcher's uranium magazine.
        /// </summary>
        public static bool ChargesFor(int uraniumCost, bool launcherIsPlayerFaction)
        {
            // Uranium is a colony logistics cost. Charging a raid for it would mean simulating a
            // supply line that does not exist, and the only visible effect would be every enemy
            // javelin quietly reverting to the basic rocket on its first shot.
            return uraniumCost > 0 && launcherIsPlayerFaction;
        }

        /// <summary>
        /// Whether the launcher can fire this rocket right now. A launcher that cannot falls back to
        /// the basic rocket rather than refusing the shot.
        /// </summary>
        public static bool CanFire(int uraniumCost, float uraniumHeld, bool launcherIsPlayerFaction)
        {
            return !ChargesFor(uraniumCost, launcherIsPlayerFaction) || uraniumHeld >= uraniumCost;
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
