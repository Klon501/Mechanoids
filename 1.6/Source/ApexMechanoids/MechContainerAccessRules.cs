namespace ApexMechanoids
{
    /// <summary>What opening a stasis container gets you.</summary>
    public enum ContainerOpening
    {
        /// <summary>Nothing to open, or nobody who could open it.</summary>
        Blocked,

        /// <summary>The occupant comes out already answering to the mechanitor who opened it.</summary>
        Controlled,

        /// <summary>
        /// The occupant comes out loose. The container is forced rather than hacked, so the mech is
        /// standing there with no overseer, and stays that way until somebody has the bandwidth to
        /// take it. Vanilla's own feral timer runs from that moment.
        /// </summary>
        Uncontrolled,
    }

    /// <summary>
    /// Who may open a stasis container, and who may be put back into one.
    ///
    /// Kept free of Verse so the decision table can be run outside the game. The bandwidth arithmetic
    /// and the two or three flags around it are the part that decides what the player sees; the stat
    /// lookups that feed it are not what goes wrong.
    /// </summary>
    public static class MechContainerAccessRules
    {
        /// <summary>
        /// Bandwidth decides how the container opens, not whether it opens.
        ///
        /// It used to be a gate: short of bandwidth and the hack option was greyed out with no way
        /// forward, which left a container the colony could see, reach and had every reason to crack
        /// sitting shut for want of a number. Forcing it is now allowed and the cost is paid on the
        /// other side, in a mech nobody is holding the leash of.
        /// </summary>
        public static ContainerOpening ResolveOpen(bool hasOccupant, bool isMechanitor, float freeBandwidth, float occupantBandwidthCost)
        {
            if (!hasOccupant || !isMechanitor)
            {
                return ContainerOpening.Blocked;
            }
            return freeBandwidth >= occupantBandwidthCost
                ? ContainerOpening.Controlled
                : ContainerOpening.Uncontrolled;
        }

        /// <summary>
        /// Whether a mech may be walked into a container.
        ///
        /// The old test was <c>Pawn.IsColonyMech</c>, which is faction plus "not in a mental state".
        /// That quietly excluded the mechs this is most useful for: the ones that came out of a
        /// forced container with no overseer, and the ones that have stopped taking orders. Being
        /// out of control is the reason to box a mech, not a reason to refuse.
        ///
        /// Faction is still the line. A machine that has gone feral has left the colony and belongs
        /// to the mechanoids again, and walking one of those into a crate is not something the
        /// colony gets to order.
        /// </summary>
        public static bool CanBeSentInside(bool playerFaction, bool everControllable, bool downed, bool dead)
        {
            return playerFaction && everControllable && !downed && !dead;
        }
    }
}
