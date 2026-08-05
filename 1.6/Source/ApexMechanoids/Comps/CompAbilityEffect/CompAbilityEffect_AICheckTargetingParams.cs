using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    // Seems like a there's a bug: ai doesn't call targetingParam.CanTarget when selecting an ability to cast. See https://discord.com/channels/684960023020961812/1530332228901539961/1530332228901539961
    // So we call it ourselves then
    public class CompAbilityEffect_AICheckTargetingParams : CompAbilityEffect
    {
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            var targetingParams = parent.verb.targetParams;
            return (targetingParams.canTargetSelf || target.Pawn != this.parent.pawn) && targetingParams.CanTarget(target.ToTargetInfo(parent.pawn.MapHeld), parent.verb);
            
        }
    }
}
