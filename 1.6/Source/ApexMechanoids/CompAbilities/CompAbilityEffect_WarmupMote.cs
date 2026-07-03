using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompAbilityEffect_WarmupMote : CompAbilityEffect
    {
        private Mote mote;
        public CompProperties_AbilityWarmupMote Props => (CompProperties_AbilityWarmupMote)props;
        public override void CompTick()
        {
            base.CompTick();
            if (this.Props.moteDef == null || !this.parent.Casting)
            {
                return;
            }
            Vector3 vector = parent.pawn.DrawPos;
            vector += (parent.verb.CurrentTarget.CenterVector3 - vector);// * parent.def.moteOffsetAmountTowardsTarget;
            if (mote != null && !mote.Destroyed)
            {
                if (!(mote is MoteAttached))
                {
                    mote.exactPosition = vector;
                }
                mote.exactRotation += Props.rotation;
                mote.Maintain();
                return;
            }
            if (Props.moteDef.thingClass != typeof(MoteAttached))
            {
                mote = MoteMaker.MakeStaticMote(vector, parent.pawn.Map, Props.moteDef, 1f, false, 0f);
                return;
            }
            mote = MoteMaker.MakeAttachedOverlay((parent.verb.CurrentTarget.Thing as Corpse) ?? parent.verb.CurrentTarget.Thing, Props.moteDef, Vector3.zero, 1f, -1f);
        }
    }
    public class CompProperties_AbilityWarmupMote : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityWarmupMote() : base()
        {
            this.compClass = typeof(CompAbilityEffect_WarmupMote);
        }
        public ThingDef moteDef;
        public float rotation = 0f;
    }
}
