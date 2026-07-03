using System.Collections.Generic;
using Verse;


namespace ApexMechanoids
{
    public class PawnRenderNodeProperties_BodyPartShield : PawnRenderNodeProperties
    {
        public string maskPath;
        public PawnRenderNodeProperties_BodyPartShield()
        {
            workerClass = typeof(PawnRenderNodeWorker_BodyPartShield);
            nodeClass = typeof(PawnRenderNode_BodyPartShield);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (linkedBodyPartsGroup == null)
            {
                yield return $"Body part shield node {debugLabel} has no linkedBodyPartsGroup defined.";
            }

            if (maskPath == null)
            {
                yield return $"Body part shield node {debugLabel} has no maskPath defined.";
            }
        }
    }
}
