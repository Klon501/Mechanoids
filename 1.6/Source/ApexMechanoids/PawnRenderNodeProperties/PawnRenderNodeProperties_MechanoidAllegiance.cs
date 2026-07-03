using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNodeProperties_MechanoidAllegiance : PawnRenderNodeProperties
    {
        [NoTranslate]
        public string maskPath;

        public PawnRenderNodeProperties_MechanoidAllegiance()
        {
            nodeClass = typeof(PawnRenderNode_MechanoidAllegiance);
        }
    }
}
