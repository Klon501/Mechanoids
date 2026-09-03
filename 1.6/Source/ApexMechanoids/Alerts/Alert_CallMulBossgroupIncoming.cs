using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Port of the vanilla bossgroup incoming alert (QuestNode_Root_Bossgroup), which uses
    /// "AlertBossgroupIncoming"/"AlertBossgroupIncomingDesc". Apex queues its bossgroup as an
    /// incident instead of a quest, so the alert reads the pending state from the incident queue.
    /// </summary>
    public class Alert_CallMulBossgroupIncoming : Alert_Critical
    {
        private static Dictionary<IncidentDef, string> cachedLeaderNames;

        private QueuedIncident pending;
        private string leaderName;

        public Alert_CallMulBossgroupIncoming()
        {
            requireBiotech = true;
        }

        public override AlertReport GetReport()
        {
            pending = null;
            leaderName = null;

            IncidentQueue queue = Find.Storyteller?.incidentQueue;
            if (queue == null || queue.Count == 0)
            {
                return AlertReport.Inactive;
            }

            foreach (QueuedIncident queued in queue)
            {
                FiringIncident firingIncident = queued?.FiringIncident;
                IncidentDef incidentDef = firingIncident?.def;
                if (incidentDef == null || incidentDef.GetModExtension<DefModExtension_Incident_CallMulBossgroup>() == null)
                {
                    continue;
                }
                pending = queued;
                leaderName = LeaderNameOf(incidentDef);
                Map map = firingIncident.parms?.target as Map;
                if (map != null)
                {
                    return AlertReport.CulpritIs(new GlobalTargetInfo(map.Center, map));
                }
                break;
            }

            return pending != null ? AlertReport.Active : AlertReport.Inactive;
        }

        public override string GetLabel()
        {
            return "APM.AlertCallMulBossgroupIncoming".Translate(leaderName.Named("LEADER")).CapitalizeFirst();
        }

        public override TaggedString GetExplanation()
        {
            return "APM.AlertCallMulBossgroupIncomingDesc".Translate(leaderName.Named("LEADER")).CapitalizeFirst();
        }

        private static string LeaderNameOf(IncidentDef incidentDef)
        {
            if (cachedLeaderNames == null)
            {
                cachedLeaderNames = new Dictionary<IncidentDef, string>();
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    List<CompProperties> comps = thingDef.comps;
                    if (comps.NullOrEmpty())
                    {
                        continue;
                    }
                    foreach (CompProperties comp in comps)
                    {
                        if (comp is CompProperties_Useable_CallMulBossgroup props && props.incidentDef != null && !props.leaderName.NullOrEmpty())
                        {
                            cachedLeaderNames[props.incidentDef] = props.leaderName;
                        }
                    }
                }
            }
            if (cachedLeaderNames.TryGetValue(incidentDef, out string name))
            {
                return name;
            }
            DefModExtension_Incident_CallMulBossgroup ext = incidentDef.GetModExtension<DefModExtension_Incident_CallMulBossgroup>();
            if (!ext.bosses.NullOrEmpty())
            {
                return ext.bosses[0].label;
            }
            return incidentDef.label;
        }
    }
}
