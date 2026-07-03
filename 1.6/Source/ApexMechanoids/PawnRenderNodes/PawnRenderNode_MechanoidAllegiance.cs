using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNode_MechanoidAllegiance : PawnRenderNode
    {
        private const string TerminusBossKindDefName = "APM_Mech_Terminus_Boss";
        private const string TerminusBossCapeTexPath = "Things/Pawn/Mechanoid/Terminus/Boss/Nodes/CapeBAncient";
        private const string TerminusRegularCapeTexPath = "Things/Pawn/Mechanoid/Terminus/Regular/Nodes/Cape";

        public new PawnRenderNodeProperties_MechanoidAllegiance Props => (PawnRenderNodeProperties_MechanoidAllegiance)props;
        public PawnRenderNode_MechanoidAllegiance(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (pawn?.kindDef?.defName == TerminusBossKindDefName && Props.texPath == TerminusRegularCapeTexPath)
            {
                return GraphicDatabase.Get<Graphic_Multi>(TerminusBossCapeTexPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
            }

            GraphicData data = new GraphicData();
            data.texPath = Props.texPath;
            data.graphicClass = typeof(Graphic_Multi);
            data.drawSize = Vector2.one;
            data.shaderType = DefDatabase<ShaderTypeDef>.GetNamed("CutoutWithOverlay");
            data.maskPath = Props.maskPath;
            return data.GraphicColoredFor(pawn);
        }
    }
}