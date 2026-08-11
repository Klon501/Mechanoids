using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbilityEffect_NeedOffset : CompAbilityEffect
    {
        public CompAbilityEffect_NeedOffset() : base() { }
        public new CompProperties_NeedOffset Props => (CompProperties_NeedOffset)this.props;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.TryGetPawn(out var pawn))
            {
                var need = pawn.needs?.TryGetNeed(Props.needDef);
                if (need != null)
                {
                    need.CurLevel += Props.offset;
                }
            }
        }
    }
    public class CompProperties_NeedOffset : CompProperties_AbilityEffect
    {
        public CompProperties_NeedOffset() : base()
        {
            this.compClass = typeof(CompAbilityEffect_NeedOffset);
        }
        public NeedDef needDef;
        public float offset = 0.1f;
    }
}
