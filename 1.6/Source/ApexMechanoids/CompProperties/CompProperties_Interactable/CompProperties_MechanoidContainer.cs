using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_MechanoidContainer : CompProperties_Interactable
    {
        public List<PawnKindDefWeight> mechKindOptions = new List<PawnKindDefWeight>();

        /// <summary>
        /// Maps the colony's current threat points onto the highest <c>combatPower</c> this container
        /// is allowed to hold. Options above the cap are dropped before the weighted roll, so a young
        /// colony cannot open one of these and walk away with a centipede.
        ///
        /// Leave it out and the container rolls its whole option list the moment it is made, which is
        /// the old behaviour.
        /// </summary>
        public SimpleCurve maxCombatPowerByThreatPoints;

        /// <summary>
        /// The other end of the same window: the lowest <c>combatPower</c> worth handing a colony this
        /// strong. Without it a rich colony still rolls militors, because the cap only ever widens the
        /// pool and the cheap kinds carry the heaviest weights.
        /// </summary>
        public SimpleCurve minCombatPowerByThreatPoints;

        /// <summary>
        /// Adds every player controllable mech that is not a bossgroup boss and not listed in
        /// <see cref="excludedMechKinds"/> to the roll, whatever mod it came from.
        /// <see cref="mechKindOptions"/> stays the curated core and keeps its own weights; this only
        /// picks up what nobody has written a weight for.
        /// </summary>
        public bool autoIncludeControllableMechs;

        /// <summary>Weight given to a kind that got in through <see cref="autoIncludeControllableMechs"/>.</summary>
        public float autoIncludeWeight = 2f;

        /// <summary>
        /// Kinds that never belong in a container no matter how they qualified. Mechs that cannot
        /// stand on their own outside the group they escort go here.
        /// </summary>
        public List<PawnKindDef> excludedMechKinds = new List<PawnKindDef>();

        /// <summary>
        /// Stock this container only when something other than the player put it on the map. A found
        /// one comes with an occupant sealed inside; one the colony builds starts empty and waits for
        /// a mech to be walked into it.
        /// </summary>
        public bool stockOnlyWhenNotPlayerBuilt;

        public GraphicData emptyGraphic;

        public CompProperties_MechanoidContainer()
        {
            compClass = typeof(Comp_MechanoidContainer);
        }
    }
}
