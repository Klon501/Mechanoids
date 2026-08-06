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

        public GraphicData emptyGraphic;

        public CompProperties_MechanoidContainer()
        {
            compClass = typeof(Comp_MechanoidContainer);
        }
    }
}
