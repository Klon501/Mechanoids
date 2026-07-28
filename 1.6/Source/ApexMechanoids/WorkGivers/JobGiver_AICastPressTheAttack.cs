using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AICastPressTheAttack : JobGiver_AICastAbility
    {
        public override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
        {
            return caster; // Cast it anyway. There's no reason to hold it
        }
    }
}
