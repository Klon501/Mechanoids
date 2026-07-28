using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbility_AIIngores : CompAbilityEffect
    {
        public CompProperties_AbilityAIIngores Props => (CompProperties_AbilityAIIngores)props;
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (target.HasThing && Props.ignoredTargets.Contains(target.Thing.def))
            {
                return false;
            }

            return true;
        }
    }
    public class CompProperties_AbilityAIIngores : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityAIIngores()
        {
            this.compClass = typeof(CompAbility_AIIngores);
        }
        public List<ThingDef> ignoredTargets;
    }
}
