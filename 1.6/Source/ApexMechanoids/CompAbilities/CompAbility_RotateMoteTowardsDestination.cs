using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompAbility_RotateMoteTowardsDestination : AbilityComp
    {
        public CompProperties_RotateMoteTowardsDestination Props => (CompProperties_RotateMoteTowardsDestination)props;
        public override void CompTick()
        {
            base.CompTick();
            var mote = parent.warmupMote;
            if (mote != null)
            {
                mote.exactRotation = Props.extraRotation + Vector3Utility.AngleToFlat(parent.verb.CurrentTarget.Cell.ToVector3Shifted(), parent.pawn.DrawPos);
            }
        }
    }
    public class CompProperties_RotateMoteTowardsDestination : AbilityCompProperties
    {
        public CompProperties_RotateMoteTowardsDestination() : base()
        {
            this.compClass = typeof(CompAbility_RotateMoteTowardsDestination);
        }
        public float extraRotation = 0f;
    }
}
