using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_BlindingLaser : CompProperties_AbilityEffectWithDuration
    {
        public HediffDef hediffDef;
        public bool replaceExisting;
        public float severity = -1f;

        public CompProperties_BlindingLaser()
        {
            compClass = typeof(CompAbilityEffect_BlindingLaser);
        }
    }

    public class CompAbilityEffect_BlindingLaser : CompAbilityEffect_WithDuration
    {
        public new CompProperties_BlindingLaser Props => (CompProperties_BlindingLaser)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = target.Pawn;
            if (pawn == null || !CanBlindPawn(pawn))
            {
                return;
            }

            ApplyBlindHediff(pawn);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            Pawn pawn = target.Pawn;
            return pawn == null || CanBlindPawn(pawn);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Pawn != null && CanBlindPawn(target.Pawn);
        }

        private bool CanBlindPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && !(pawn.RaceProps?.IsMechanoid ?? false)
                && pawn.health?.hediffSet != null
                && HasSightSourcePart(pawn);
        }

        private void ApplyBlindHediff(Pawn pawn)
        {
            if (Props.hediffDef == null)
            {
                return;
            }

            List<BodyPartRecord> sightParts = GetSightSourceParts(pawn);
            if (sightParts.Count == 0)
            {
                return;
            }

            if (Props.replaceExisting)
            {
                Hediff existing;
                while ((existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef)) != null)
                {
                    pawn.health.RemoveHediff(existing);
                }
            }

            for (int i = 0; i < sightParts.Count; i++)
            {
                ApplyBlindHediff(pawn, sightParts[i]);
            }
        }

        private void ApplyBlindHediff(Pawn pawn, BodyPartRecord sightPart)
        {
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn, sightPart);
            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = GetDurationSeconds(pawn).SecondsToTicks();
            }

            if (Props.severity >= 0f)
            {
                hediff.Severity = Props.severity;
            }

            pawn.health.AddHediff(hediff, sightPart);
        }

        private static bool HasSightSourcePart(Pawn pawn)
        {
            foreach (BodyPartRecord _ in pawn.health.hediffSet.GetNotMissingParts(tag: BodyPartTagDefOf.SightSource))
            {
                return true;
            }

            return false;
        }

        private static List<BodyPartRecord> GetSightSourceParts(Pawn pawn)
        {
            List<BodyPartRecord> sightParts = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts(tag: BodyPartTagDefOf.SightSource))
            {
                sightParts.Add(part);
            }

            return sightParts;
        }
    }
}