using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class HediffComp_IngestorBiomassProcessor : HediffComp
    {
        private List<IngestorBiomassBatch> batches = new List<IngestorBiomassBatch>();
        private ThingDef legacyThingDef;
        private int legacyCount;
        private int legacyTicksToDisappear = -1;

        private HediffCompProperties_IngestorBiomassProcessor Props => (HediffCompProperties_IngestorBiomassProcessor)props;

        public bool HasBatches => batches != null && batches.Count > 0;

        public override bool CompShouldRemove => !HasBatches;

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (!HasBatches)
                {
                    return null;
                }

                int totalCount = TotalOutputCount();
                string thingLabel = batches[0].ThingLabel;
                string time = NextBatchTicks().ToStringTicksToPeriod(allowSeconds: true, shortForm: true);

                if (batches.Count == 1)
                {
                    return "APM_IngestorBiomass_LabelSingle".Translate(totalCount, thingLabel, time);
                }

                return "APM_IngestorBiomass_LabelMultiple".Translate(batches.Count, totalCount, thingLabel, time);
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                if (!HasBatches)
                {
                    return null;
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("APM_IngestorBiomass_TooltipHeader".Translate());
                int maxRows = Math.Max(1, Props.maxTooltipBatches);
                int rows = Math.Min(batches.Count, maxRows);
                for (int i = 0; i < rows; i++)
                {
                    IngestorBiomassBatch batch = batches[i];
                    stringBuilder.AppendLine("  - " + "APM_IngestorBiomass_TooltipBatch".Translate(
                        batch.count,
                        batch.ThingLabel,
                        batch.ticksLeft.ToStringTicksToPeriod(allowSeconds: true, shortForm: true)));
                }

                int hiddenRows = batches.Count - rows;
                if (hiddenRows > 0)
                {
                    stringBuilder.AppendLine("  - " + "APM_IngestorBiomass_TooltipMore".Translate(hiddenRows));
                }

                return stringBuilder.ToString().TrimEndNewlines();
            }
        }

        public void AddBatch(ThingDef thingDef, int count, int durationTicks)
        {
            if (thingDef == null || count <= 0 || durationTicks <= 0)
            {
                return;
            }

            if (batches == null)
            {
                batches = new List<IngestorBiomassBatch>();
            }
            batches.Add(new IngestorBiomassBatch(thingDef, count, durationTicks));
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!HasBatches)
            {
                return;
            }

            if (Pawn?.MapHeld == null || !Pawn.PositionHeld.IsValid)
            {
                return;
            }

            for (int i = batches.Count - 1; i >= 0; i--)
            {
                IngestorBiomassBatch batch = batches[i];
                batch.ticksLeft -= delta;
                if (batch.ticksLeft > 0)
                {
                    continue;
                }

                if (IngestorCorpseProcessingUtility.TrySpawnThingNear(Pawn.PositionHeld, Pawn.MapHeld, batch.thingDef, batch.count))
                {
                    batches.RemoveAt(i);
                }
                else
                {
                    batch.ticksLeft = 1;
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref batches, "batches", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Defs.Look(ref legacyThingDef, "thingToSpawn");
                Scribe_Values.Look(ref legacyCount, "count", 0);
                Scribe_Values.Look(ref legacyTicksToDisappear, "ticksToDisappear", -1);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (batches == null)
                {
                    batches = new List<IngestorBiomassBatch>();
                }
                if (legacyThingDef != null && legacyCount > 0)
                {
                    AddBatch(legacyThingDef, legacyCount, Math.Max(1, legacyTicksToDisappear));
                    legacyThingDef = null;
                    legacyCount = 0;
                    legacyTicksToDisappear = -1;
                }
            }
        }

        private int TotalOutputCount()
        {
            int total = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                total += batches[i].count;
            }

            return total;
        }

        private int NextBatchTicks()
        {
            int nextTicks = int.MaxValue;
            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i].ticksLeft < nextTicks)
                {
                    nextTicks = batches[i].ticksLeft;
                }
            }

            return Math.Max(1, nextTicks);
        }
    }

    public class HediffCompProperties_IngestorBiomassProcessor : HediffCompProperties
    {
        public int maxTooltipBatches = 8;

        public HediffCompProperties_IngestorBiomassProcessor()
        {
            compClass = typeof(HediffComp_IngestorBiomassProcessor);
        }
    }

    public class IngestorBiomassBatch : IExposable
    {
        public ThingDef thingDef;
        public int count;
        public int ticksLeft;
        public int totalTicks;

        public string ThingLabel => thingDef?.label ?? "APM_IngestorBiomass_UnknownOutput".Translate();

        public IngestorBiomassBatch()
        {
        }

        public IngestorBiomassBatch(ThingDef thingDef, int count, int durationTicks)
        {
            this.thingDef = thingDef;
            this.count = count;
            ticksLeft = Math.Max(1, durationTicks);
            totalTicks = ticksLeft;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
            Scribe_Values.Look(ref totalTicks, "totalTicks", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ticksLeft = Math.Max(1, ticksLeft);
                totalTicks = Math.Max(ticksLeft, totalTicks);
            }
        }
    }

    [Obsolete("Use HediffComp_IngestorBiomassProcessor.")]
    public class CompSpawnThingOnRemove : HediffComp_IngestorBiomassProcessor
    {
    }
}