using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilityFlyerQueueAbility : CompProperties_AbilityEffect
    {
        public AbilityDef abilityOnFinish;
        public CompProperties_AbilityFlyerQueueAbility() => compClass = typeof(CompAbilityFlyerQueueAbility);
    }

    public class CompAbilityFlyerQueueAbility : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted // Used with Verb_JumpExt
    {
        public new CompProperties_AbilityFlyerQueueAbility Props => (CompProperties_AbilityFlyerQueueAbility)props;

        private Ability ab;
        public Ability FinishAbility
        {
            get
            {
                if (ab == null) 
                    ab = Pawn.abilities.GetAbility(Props.abilityOnFinish);
                return ab;
            }
        }

        private Pawn Pawn => parent.pawn;

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            FinishAbility?.QueueCastingJob(target, null);
        }

        public override bool AICanTargetNow(LocalTargetInfo target) => !Pawn.IsColonistPlayerControlled;
    }
}
