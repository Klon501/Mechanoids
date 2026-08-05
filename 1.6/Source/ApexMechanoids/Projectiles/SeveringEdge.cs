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
    public class SeveringEdge : Projectile, VEF.Graphics.IAnimationOneTime
    {
        const float maxDist = 1.9f;
        const int ticksBeforeImpact = 10;
        private List<Thing> ignore = new List<Thing>();
        public override void Tick()
        {
            base.Tick();

            if (this.StartingTicksToImpact - this.ticksToImpact > ticksBeforeImpact)
            {
                foreach (var thing in Map.mapPawns.AllPawnsSpawned.Except(this.launcher).Where(IsValidTarget).ToList())
                {
                    Impact(thing);
                }
            }
        }
        protected virtual void Impact(Thing thing)
        {
            BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(this.launcher, thing, null, this.launcher.def, this.def, this.targetCoverDef);
            Find.BattleLog.Add(battleLogEntry_RangedImpact);
            DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, (float)this.DamageAmount, this.ArmorPenetration, this.ExactRotation.eulerAngles.y, this.launcher, null, this.equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, this.intendedTarget.Thing, true, true, QualityCategory.Normal, true, false);
            thing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_RangedImpact);

            ignore.Add(thing);
        }
        protected virtual bool IsValidTarget(Thing target)
        {
            if (!(target is Pawn pawn))
            {
                return false;
            }
            if (ignore.Contains(target))
            {
                return false;
            }
            if ((target.DrawPos - DrawPos).MagnitudeHorizontalSquared() > maxDist * maxDist)
            {
                return false;
            }

            return true;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ignore, nameof(ignore), LookMode.Reference);
        }

        public int CurrentIndex()
        {
            var data = (VEF.Graphics.GraphicData_Animated)this.def.graphicData;
            var graphics = (VEF.Graphics.Graphic_Animated)this.def.graphicData.Graphic;
            return Mathf.Min((Mathf.FloorToInt(StartingTicksToImpact) - ticksToImpact) / data.ticksPerFrame, graphics.SubGraphicCount);
        }
    }
}
