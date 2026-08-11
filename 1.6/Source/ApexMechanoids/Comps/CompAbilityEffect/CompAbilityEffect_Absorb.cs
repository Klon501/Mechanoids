using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompAbilityEffect_Absorb : CompAbilityEffect
    {
        public new CompProperties_Absorb Props => (CompProperties_Absorb)props;

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return IngestorCorpseProcessingUtility.CanAbsorbThing(target.Thing).Accepted;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            AcceptanceReport report = IngestorCorpseProcessingUtility.CanAbsorbThing(target.Thing);
            if (!report.Accepted && throwMessages && !report.Reason.NullOrEmpty())
            {
                Messages.Message(report.Reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
            }

            return report.Accepted;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            AcceptanceReport report = IngestorCorpseProcessingUtility.CanAbsorbThing(target.Thing);
            return report.Accepted ? null : report.Reason;
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            base.PostApplied(targets, map);
            Pawn pawn = parent?.pawn;
            if (pawn?.health == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                LocalTargetInfo target = targets[i];
                if (!target.HasThing || target.ThingDestroyed)
                {
                    continue;
                }

                Thing thing = target.Thing;
                AcceptanceReport report = IngestorCorpseProcessingUtility.TryGetAbsorbOutput(thing, Props, out ThingDef outputThingDef, out int outputCount, out int durationTicks);
                if (!report.Accepted)
                {
                    continue;
                }

                HediffComp_IngestorBiomassProcessor processor = IngestorCorpseProcessingUtility.GetOrCreateBiomassProcessor(pawn);
                if (processor == null)
                {
                    continue;
                }

                if (thing is Corpse corpse)
                {
                    IngestorCorpseProcessingUtility.TryRemoveAbsorbDesignation(corpse);
                    if (IngestorCorpseProcessingUtility.ShouldStripCorpseBeforeAbsorb(pawn, corpse))
                    {
                        corpse.Strip(false);
                    }
                }

                thing.Destroy();
                processor.AddBatch(outputThingDef, outputCount, durationTicks);
            }
        }
    }

    public class CompProperties_Absorb : CompProperties_AbilityEffect
    {
        public float chemfuelPer1BodySizeOfCorpse = 36f;
        public float chemfuelPer1Nutrition = 1f;
        public SimpleCurve durationFromSize;
        public SimpleCurve durationFromNutrition;

        public CompProperties_Absorb()
        {
            compClass = typeof(CompAbilityEffect_Absorb);
        }
    }
}
