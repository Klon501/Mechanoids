using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class FloatMenuOptionProvider_SirenCapturePawn : FloatMenuOptionProvider_CapturePawn
    {
        public override bool MechanoidCanDo => true;

        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
        {
            return base.SelectedPawnValid(pawn, context)
                && pawn.IsColonyMechPlayerControlled
                && SirenWardenUtility.CanSirenWork(pawn);
        }
    }
}
