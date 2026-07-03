using System.Linq;
using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNode_BodyPartShield : PawnRenderNode
    {
        private BodyPartGroupDef linkedBodyPartsGroup;
        public new PawnRenderNodeProperties_BodyPartShield Props => (PawnRenderNodeProperties_BodyPartShield)props;

        public PawnRenderNode_BodyPartShield(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
            linkedBodyPartsGroup = props.linkedBodyPartsGroup;
        }

        public bool HasBodyPart()
        {
            if (linkedBodyPartsGroup == null)
            {
                return true;
            }

            Pawn pawn = tree.pawn;
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            return pawn.health.hediffSet.GetNotMissingParts()
                .Any(part => part.groups != null && part.groups.Contains(linkedBodyPartsGroup));
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            return GraphicDatabase.Get<Graphic_Multi>(Props.texPath, ShaderDatabase.CutoutWithOverlay, Props.maskPath);
        }
    }
}
