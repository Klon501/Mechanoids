using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbility_AIRequiresOverpower : CompAbilityEffect
    {
        public CompProperties_AbilityAIRequiresOverpower Props => (CompProperties_AbilityAIRequiresOverpower)props;
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            var targetPawn = target.Pawn;
            if (targetPawn != null)
            {
                var casterDPS = parent.pawn.GetStatValue(StatDefOf.MeleeDPS);
                var targetDPS = targetPawn.GetStatValue(StatDefOf.MeleeDPS);
                return casterDPS + Props.maximalMeleeDPSDifference > targetDPS;
            }

            return true;
        }
    }
    public class CompProperties_AbilityAIRequiresOverpower : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityAIRequiresOverpower()
        {
            this.compClass = typeof(CompAbility_AIRequiresOverpower);
        }
        public float maximalMeleeDPSDifference = 10f;
    }
}
