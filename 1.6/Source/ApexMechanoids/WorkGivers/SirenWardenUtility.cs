using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class SirenWardenUtility
    {
        private const string SirenDefName = "APM_Mech_Siren";
        private const float ResistanceReductionPerInteraction = 1.2f;
        private const float NegotiationPower = 1.2f;

        public static bool CanSirenWork(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && !pawn.Dead && !pawn.Downed && pawn.Spawned && pawn.Map != null && pawn.def?.defName == SirenDefName;
        }

        public static bool CanChatWithPrisoner(Pawn siren, Pawn prisoner, bool forced)
        {
            return CanContinueChatWithPrisoner(siren, prisoner) && siren.CanReserveAndReach(prisoner, PathEndMode.Touch, siren.NormalMaxDanger(), 1, -1, null, forced);
        }

        public static bool CanContinueChatWithPrisoner(Pawn siren, Pawn prisoner)
        {
            if (!CanSirenWork(siren) || prisoner?.guest == null || !prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure || !prisoner.Spawned || prisoner.InMentalState || prisoner.InAggroMentalState || prisoner.IsForbidden(siren) || prisoner.IsFormingCaravan())
            {
                return false;
            }

            PrisonerInteractionModeDef mode = prisoner.guest.ExclusiveInteractionMode;
            if (mode != PrisonerInteractionModeDefOf.AttemptRecruit && mode != PrisonerInteractionModeDefOf.ReduceResistance)
            {
                return false;
            }

            if (!prisoner.guest.ScheduledForInteraction)
            {
                JobFailReason.Is("PrisonerInteractedTooRecently".Translate());
                return false;
            }

            if (mode == PrisonerInteractionModeDefOf.ReduceResistance && prisoner.guest.Resistance <= 0f)
            {
                return false;
            }

            if (!prisoner.Awake() || (prisoner.Downed && !prisoner.InBed()))
            {
                return false;
            }

            return siren.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) == true;
        }

        public static void DoRecruitInteraction(Pawn siren, Pawn prisoner)
        {
            if (siren == null || prisoner?.guest == null || !prisoner.Spawned || !prisoner.Awake())
            {
                return;
            }

            PrisonerInteractionModeDef mode = prisoner.guest.ExclusiveInteractionMode;
            if (mode == PrisonerInteractionModeDefOf.AttemptRecruit && prisoner.guest.Resistance <= 0f)
            {
                RecruitPrisoner(siren, prisoner);
            }
            else if (mode == PrisonerInteractionModeDefOf.AttemptRecruit || mode == PrisonerInteractionModeDefOf.ReduceResistance)
            {
                ReduceResistance(siren, prisoner);
            }

            SetLastInteractTime(prisoner);
        }

        private static void ReduceResistance(Pawn siren, Pawn prisoner)
        {
            float oldResistance = prisoner.guest.resistance;
            if (oldResistance <= 0f)
            {
                return;
            }

            float moodFactor = prisoner.needs?.mood == null ? 1f : Mathf.Lerp(0.2f, 1.5f, prisoner.needs.mood.CurInstantLevelPercentage);
            float reduction = Mathf.Min(oldResistance, ResistanceReductionPerInteraction * moodFactor);
            prisoner.guest.resistance = Mathf.Max(0f, oldResistance - reduction);

            if (siren.Spawned && prisoner.Spawned && siren.Map == prisoner.Map)
            {
                float before = Mathf.Max(0.1f, oldResistance);
                float after = prisoner.guest.resistance > 0f ? Mathf.Max(0.1f, prisoner.guest.resistance) : 0f;
                MoteMaker.ThrowText((siren.DrawPos + prisoner.DrawPos) / 2f, siren.Map, "TextMote_ResistanceReduced".Translate(before.ToString("F1"), after.ToString("F1")), 8f);
            }

            if (prisoner.guest.resistance <= 0f)
            {
                prisoner.guest.SetLastResistanceReduceData(siren, reduction, NegotiationPower, moodFactor, 1f);
                TaggedString message = "MessagePrisonerResistanceBroken".Translate(prisoner.LabelShort, siren.LabelShort, siren.Named("WARDEN"), prisoner.Named("PRISONER"));
                if (prisoner.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.AttemptRecruit))
                {
                    message += " " + "MessagePrisonerResistanceBroken_RecruitAttempsWillBegin".Translate();
                }

                Messages.Message(message, prisoner, MessageTypeDefOf.PositiveEvent);
            }
        }

        private static void RecruitPrisoner(Pawn siren, Pawn prisoner)
        {
            prisoner.guest.SetRecruitmentData(siren);
            RecruitUtility.Recruit(prisoner, siren.Faction ?? Faction.OfPlayer, siren);
            Messages.Message("MessageRecruitSuccess".Translate(siren, prisoner, siren.Named("RECRUITER"), prisoner.Named("RECRUITEE")), prisoner, MessageTypeDefOf.PositiveEvent);
        }

        private static void SetLastInteractTime(Pawn prisoner)
        {
            if (prisoner?.mindState == null)
            {
                return;
            }

            prisoner.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
            prisoner.mindState.interactionsToday++;
        }
    }
}
