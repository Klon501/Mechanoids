using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Names the body a casket is found with, from the def rather than from code.
    ///
    /// This is the casket's version of what <c>CompProperties_MechanoidContainer.fixedMechKind</c>
    /// does for the stasis containers: a layout cell can only name a ThingDef, so anything that
    /// varies per placement has to be readable off that def. Naming the occupant here keeps the
    /// choice next to the building it belongs to and out of <see cref="Building_AncientCommandCasket"/>,
    /// which no longer knows or cares which pawn kind it is holding.
    ///
    /// A def that carries no extension, or an extension with no <see cref="pawnKind"/>, is left
    /// empty. Filling a casket is opt-in.
    /// </summary>
    public class DefModExtension_CasketOccupant : DefModExtension
    {
        /// <summary>Who is inside. Nothing is generated when this is null.</summary>
        public PawnKindDef pawnKind;

        /// <summary>
        /// How far gone the body is. Dessicated is the default because a casket that has been sealed
        /// for centuries should not hand the player a fresh corpse, nor start rotting on arrival.
        /// </summary>
        public RotStage rotStage = RotStage.Dessicated;

        /// <summary>
        /// The faction the occupant belonged to. Left null for the hostile ancients, which is what a
        /// pre-collapse body defaults to and what the corpse's apparel is judged by.
        /// </summary>
        public FactionDef faction;
    }
}
