using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    [DefOf]
    public static class APM_HediffDefOf
    {
        public static HediffDef APM_Hediff_Unity;
    }

    [StaticConstructorOnStartup]
    public static class UnityStatCache
    {
        public static readonly HashSet<StatDef> AffectedStats = new HashSet<StatDef>();
        public static readonly Dictionary<string, StatDef> LabelToStat = new Dictionary<string, StatDef>();

        static UnityStatCache()
        {
            LongEventHandler.ExecuteWhenFinished(BuildCache);
        }

        private static void BuildCache()
        {
            HediffDef def = APM_HediffDefOf.APM_Hediff_Unity;
            if (def?.stages == null) return;

            for (int i = 0; i < def.stages.Count; i++)
            {
                HediffStage stage = def.stages[i];
                if (stage.statOffsets != null)
                {
                    for (int j = 0; j < stage.statOffsets.Count; j++)
                    {
                        StatDef s = stage.statOffsets[j].stat;
                        AffectedStats.Add(s);
                        if (!LabelToStat.ContainsKey(s.LabelCap))
                            LabelToStat[s.LabelCap] = s;
                    }
                }
                if (stage.statFactors != null)
                {
                    for (int j = 0; j < stage.statFactors.Count; j++)
                    {
                        StatDef s = stage.statFactors[j].stat;
                        AffectedStats.Add(s);
                        if (!LabelToStat.ContainsKey(s.LabelCap))
                            LabelToStat[s.LabelCap] = s;
                    }
                }
            }
        }
    }
}

