using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompAbilityGranter : ThingComp
    {
        public CompProperties_AbilityGranter Props => (CompProperties_AbilityGranter)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                if (pawn.abilities.GetAbility(Props.abilities[i]) == null)
                {
                    pawn.abilities.GainAbility(Props.abilities[i]);
                }
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            for (int i = 0; i < Props.abilities.Count; i++)
            {
                pawn.abilities.RemoveAbility(Props.abilities[i]);
            }
        }
    }
}
