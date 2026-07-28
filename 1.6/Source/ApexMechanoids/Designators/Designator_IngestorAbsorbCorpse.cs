using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class Designator_IngestorAbsorbCorpse : Designator
    {
        public override DesignationDef Designation => ApexDefsOf.APM_IngestorAbsorbCorpse;

        public Designator_IngestorAbsorbCorpse()
        {
            defaultLabel = "APM_Ingestor_MarkCorpses_Label".Translate();
            defaultDesc = "APM_Ingestor_MarkCorpses_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Ingestor/MarkCorpse");
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Haul;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!cell.InBounds(Map))
            {
                return false;
            }

            bool hasCorpseRejection = false;
            AcceptanceReport corpseRejection = "APM_IngestorAbsorb_MustTargetCorpse".Translate();
            foreach (Thing thing in cell.GetThingList(Map))
            {
                AcceptanceReport report = CanDesignateThing(thing);
                if (report.Accepted)
                {
                    return true;
                }

                if (!hasCorpseRejection && thing is Corpse)
                {
                    corpseRejection = report;
                    hasCorpseRejection = true;
                }
            }

            return corpseRejection;
        }

        public override void DesignateSingleCell(IntVec3 cell)
        {
            foreach (Thing thing in cell.GetThingList(Map))
            {
                if (CanDesignateThing(thing).Accepted)
                {
                    DesignateThing(thing);
                }
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing thing)
        {
            if (!(thing is Corpse corpse))
            {
                return "APM_IngestorAbsorb_MustTargetCorpse".Translate();
            }

            AcceptanceReport report = IngestorCorpseProcessingUtility.CanAbsorbCorpse(corpse);
            if (!report.Accepted)
            {
                return report;
            }

            if (Map.designationManager.DesignationOn(corpse, Designation) != null)
            {
                return "APM_IngestorAbsorb_AlreadyMarked".Translate();
            }

            return true;
        }

        public override void DesignateThing(Thing thing)
        {
            if (thing is Corpse corpse && Map.designationManager.DesignationOn(corpse, Designation) == null)
            {
                Map.designationManager.AddDesignation(new Designation(corpse, Designation));
            }
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
