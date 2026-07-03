using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNodeWorker_TerminusCape : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            return !TerminusOverdriveCapeState.ShouldHideCape(parms.pawn);
        }
    }
}