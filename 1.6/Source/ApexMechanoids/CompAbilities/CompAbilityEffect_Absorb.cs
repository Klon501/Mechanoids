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
    public class CompAbilityEffect_Absorb : CompAbilityEffect
    {
        public new CompProperties_Absorb Props => (CompProperties_Absorb)this.props;
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var valid = base.Valid(target, throwMessages);
            if (valid)
            {
                if (target.Thing is Corpse corpse && (corpse.InnerPawn?.RaceProps.IsFlesh ?? false))
                {
                    var comp = corpse.TryGetComp<CompRottable>();
                    return comp != null && comp.Stage != RotStage.Dessicated;
                }
                return target.Thing?.IngestibleNow ?? false;
            }
            return false;
        }
        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            base.PostApplied(targets, map);
            foreach (var target in targets)
            {
                if (!target.HasThing || target.ThingDestroyed)
                {
                    continue;
                }
                int chemfuelCount;
                int duration;
                if (target.Thing is Corpse corpse && corpse.InnerPawn != null)
                {
                    chemfuelCount = Mathf.FloorToInt(corpse.InnerPawn.BodySize * Props.chemfuelPer1BodySizeOfCorpse);
                    duration = (int)(Props.durationFromSize.Evaluate(corpse.InnerPawn.BodySize) * GenDate.TicksPerHour);
                }
                else if (target.Thing.def.ingestible != null)
                {
                    chemfuelCount = Mathf.FloorToInt(target.Thing.def.ingestible.CachedNutrition * target.Thing.stackCount * Props.chemfuelPer1Nutrition);
                    duration = (int)(Props.durationFromNutrition.Evaluate(target.Thing.def.ingestible.CachedNutrition * target.Thing.stackCount) * GenDate.TicksPerHour);
                }
                else
                {
                    continue;
                }
                var pos = target.Thing.Position;
                target.Thing.Destroy();
                var hediff = parent.pawn.health.AddHediff(ApexDefsOf.APM_AbsorbedThing);
                var hediffSpawner = hediff.TryGetComp<CompSpawnThingOnRemove>();
                hediffSpawner.thing = ThingDefOf.Chemfuel;
                hediffSpawner.count = chemfuelCount;
                hediff.TryGetComp<HediffComp_Disappears>().SetDuration(duration);
            }
        }
    }
    public class CompProperties_Absorb : CompProperties_AbilityEffect
    {
        public CompProperties_Absorb() : base()
        {
            this.compClass = typeof(CompAbilityEffect_Absorb);
        }
        public float chemfuelPer1BodySizeOfCorpse = 36f;

        public float chemfuelPer1Nutrition = 1f;

        public SimpleCurve durationFromSize;

        public SimpleCurve durationFromNutrition;
    }
}
