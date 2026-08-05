using Verse;

namespace ApexMechanoids
{
    public class CompProperties_MechanoidContainerControlled : CompProperties_MechanoidContainer
    {
        [MustTranslate]
        public string ChooseMechLabel;
        [MustTranslate]
        public string ChooseMechDesc;

        public JobDef enterJobDef;

        public CompProperties_MechanoidContainerControlled()
        {
            compClass = typeof(Comp_MechanoidContainerControlled);
        }
    }
}
