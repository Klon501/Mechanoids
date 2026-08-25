using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompIngestorAbsorbSettings : ThingComp
    {
        public bool stripCorpseBeforeAbsorb;
        public bool onlyProcessMarkedCorpses;

        public CompProperties_IngestorAbsorbSettings Props => (CompProperties_IngestorAbsorbSettings)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureCorpseProcessingWorkEnabled();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stripCorpseBeforeAbsorb, "stripCorpseBeforeAbsorb", false);
            Scribe_Values.Look(ref onlyProcessMarkedCorpses, "onlyProcessMarkedCorpses", false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!(parent is Pawn pawn) || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            EnsureCorpseProcessingWorkEnabled();

            yield return new Command_Toggle
            {
                defaultLabel = (stripCorpseBeforeAbsorb ? "APM_Ingestor_StripMode_Disable_Label" : "APM_Ingestor_StripMode_Enable_Label").Translate(),
                defaultDesc = (stripCorpseBeforeAbsorb ? "APM_Ingestor_StripMode_Disable_Desc" : "APM_Ingestor_StripMode_Enable_Desc").Translate(),
                icon = ContentFinder<Texture2D>.Get(stripCorpseBeforeAbsorb ? Props.stripKeepIconPath : Props.stripDestroyIconPath),
                isActive = () => stripCorpseBeforeAbsorb,
                toggleAction = delegate
                {
                    stripCorpseBeforeAbsorb = !stripCorpseBeforeAbsorb;
                }
            };

            yield return new Command_Toggle
            {
                defaultLabel = (onlyProcessMarkedCorpses ? "APM_Ingestor_ProcessMode_All_Label" : "APM_Ingestor_ProcessMode_Marked_Label").Translate(),
                defaultDesc = (onlyProcessMarkedCorpses ? "APM_Ingestor_ProcessMode_All_Desc" : "APM_Ingestor_ProcessMode_Marked_Desc").Translate(),
                icon = ContentFinder<Texture2D>.Get(onlyProcessMarkedCorpses ? Props.processAnyIconPath : Props.processMarkedIconPath),
                isActive = () => onlyProcessMarkedCorpses,
                toggleAction = delegate
                {
                    onlyProcessMarkedCorpses = !onlyProcessMarkedCorpses;
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "APM_Ingestor_MarkCorpses_Label".Translate(),
                defaultDesc = "APM_Ingestor_MarkCorpses_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.markIconPath),
                action = delegate
                {
                    Find.DesignatorManager.Select(new Designator_IngestorAbsorbCorpse());
                }
            };
        }

        private void EnsureCorpseProcessingWorkEnabled()
        {
            if (!(parent is Pawn pawn) || pawn.def != ApexDefsOf.APM_Mech_Ingestor || pawn.Faction != Faction.OfPlayer || pawn.workSettings == null)
            {
                return;
            }

            pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            WorkTypeDef workType = ApexDefsOf.APM_IngestorCorpseProcessing;
            if (workType != null && !pawn.WorkTypeIsDisabled(workType) && pawn.workSettings.GetPriority(workType) <= 0)
            {
                pawn.workSettings.SetPriority(workType, 2);
            }
        }
    }

    public class CompProperties_IngestorAbsorbSettings : CompProperties
    {
        public string stripDestroyIconPath = "UI/Ingestor/StripModeDestroyApparel";
        public string stripKeepIconPath = "UI/Ingestor/StripModeKeepApparel";
        public string processMarkedIconPath = "UI/Ingestor/ProcessModeMarkedCorpse";
        public string processAnyIconPath = "UI/Ingestor/ProcessModeAnyCorpse";
        public string markIconPath = "UI/Ingestor/MarkCorpse";

        public CompProperties_IngestorAbsorbSettings()
        {
            compClass = typeof(CompIngestorAbsorbSettings);
        }
    }
}
