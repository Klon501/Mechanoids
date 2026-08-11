using ApexMechanoids;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilityOnJumpCompleted : CompProperties_AbilityEffect
    {
        public AbilityDef ability; // should be Command_Invisible

        public CompProperties_AbilityOnJumpCompleted() => compClass = typeof(CompAbilityOnJumpCompleted);
    }
    public class CompAbilityOnJumpCompleted : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted // Used with Verb_JumpExt
    {
        public new CompProperties_AbilityOnJumpCompleted Props => (CompProperties_AbilityOnJumpCompleted)props;

        private Pawn Pawn => parent.pawn;

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            if (Props.ability != null)
            {
                Utils.TryDoAbility(Pawn, Props.ability, target); //.Thing is Pawn p && Props.abilityOnFinish.verbProperties.targetParams.canTargetPawns ? p : target.Cell);
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target) => !Pawn.IsColonistPlayerControlled;
    }
}
