using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbilityEffect_KillCaster : CompAbilityEffect
    {
        public new CompProperties_KillCaster Props => (CompProperties_KillCaster)props;
        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            base.PostApplied(targets, map);
            if (Props.destroy)
            {
                parent.pawn.Destroy(DestroyMode.Vanish);
            }
            else
            {
                parent.pawn.Kill(null);
            }
        }
    }
    public class CompProperties_KillCaster : CompProperties_AbilityEffect
    {
        public CompProperties_KillCaster() : base()
        {
            this.compClass = typeof(CompAbilityEffect_KillCaster);
        }
        /// <summary>
        /// Completely destroys caster if true, otherwise kills caster
        /// </summary>
        public bool destroy = false;
    }
}
