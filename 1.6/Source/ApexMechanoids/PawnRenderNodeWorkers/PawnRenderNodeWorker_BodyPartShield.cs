using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNodeWorker_BodyPartShield : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            if (node is PawnRenderNode_BodyPartShield shieldNode)
            {
                return shieldNode.HasBodyPart();
            }

            return true;
        }
    }
}
