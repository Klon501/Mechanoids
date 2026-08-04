using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_MechanoidContainer : CompProperties_Interactable
    {
        public List<PawnKindDefWeight> mechKindOptions = new List<PawnKindDefWeight>();

        public GraphicData emptyGraphic;

        public CompProperties_MechanoidContainer()
        {
            compClass = typeof(Comp_MechanoidContainer);
        }
    }
}
