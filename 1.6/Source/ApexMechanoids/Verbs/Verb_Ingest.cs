using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class Verb_Ingest : Verb_CastAbilityTouch
    {
        public override TargetingParameters targetParams
        {
            get
            {
                
                if (base.targetParams.validator == null)
                {
                    base.targetParams.validator = IsValid_AbilityComps;
                }
                return base.targetParams;
            }
        }
        protected bool IsValid_AbilityComps(TargetInfo target)
        {
            foreach (var abilityComp in ability.EffectComps)
            {
                if (!abilityComp.Valid((LocalTargetInfo)target))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
