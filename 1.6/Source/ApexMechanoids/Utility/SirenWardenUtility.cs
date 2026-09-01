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
        private const float BaseResistanceReductionPerInteraction = 1f;
        private const float BaseWillReductionPerInteraction = 1f;
        private const float BaseCertaintyReductionPerInteraction = 0.06f;

        private static readonly SimpleCurve ResistanceImpactFactorCurve_Mood = new SimpleCurve
        {
            new CurvePoint(0f, 0.2f),
            new CurvePoint(0.5f, 1f),
            new CurvePoint(1f, 1.5f)
        };

        public static bool CanSirenWork(Pawn pawn)
        {
            return Utils.CanRunAutonomousPawn(pawn) && pawn.def?.defName == SirenDefName;
        }

        public static bool CanSirenSing(Pawn siren)
        {
            return CanSirenWork(siren) && siren.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) == true;
        }

        public static bool HasReachableInteractablePosition(Pawn siren, Pawn target)
        {
            if (siren?.Map == null || target?.Map != siren.Map)
            {
                return false;
            }

            return SocialInteractionUtility.IsGoodPositionForInteraction(siren, target) || SocialInteractionUtility.BestInteractableCell(siren, target).IsValid;
        }

        public static bool CanChatWithPrisoner(Pawn siren, Pawn prisoner, bool forced)
        {
            return CanStartChatWithPrisoner(siren, prisoner) && siren.CanReserve(prisoner, 1, -1, null, forced) && HasReachableInteractablePosition(siren, prisoner);
        }

        public static bool CanStartChatWithPrisoner(Pawn siren, Pawn prisoner, bool showFailReason = true)
        {
            if (!CanContinueChatWithPrisoner(siren, prisoner) || !ScheduledForInteraction(prisoner, showFailReason))
            {
                return false;
            }

            PrisonerInteractionModeDef mode = prisoner.guest.ExclusiveInteractionMode;
            return mode != PrisonerInteractionModeDefOf.ReduceResistance || prisoner.guest.Resistance > 0f;
        }

        public static bool CanContinueChatWithPrisoner(Pawn siren, Pawn prisoner)
        {
            if (!PrisonerAvailableForSong(siren, prisoner))
            {
                return false;
            }

            PrisonerInteractionModeDef mode = prisoner.guest.ExclusiveInteractionMode;
            return mode == PrisonerInteractionModeDefOf.AttemptRecruit || mode == PrisonerInteractionModeDefOf.ReduceResistance;
        }

        public static void DoRecruitInteraction(Pawn siren, Pawn prisoner)
        {
            if (!CanStartChatWithPrisoner(siren, prisoner, showFailReason: false))
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

        public static bool CanEnslavePrisoner(Pawn siren, Pawn prisoner, bool forced)
        {
            return CanStartEnslavePrisoner(siren, prisoner) && siren.CanReserve(prisoner, 1, -1, null, forced) && HasReachableInteractablePosition(siren, prisoner);
        }

        public static bool CanStartEnslavePrisoner(Pawn siren, Pawn prisoner, bool showFailReason = true)
        {
            if (!CanContinueEnslavePrisoner(siren, prisoner) || !ScheduledForInteraction(prisoner, showFailReason))
            {
                return false;
            }

            if (prisoner.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.ReduceWill) && prisoner.guest.will <= 0f)
            {
                return false;
            }

            return new HistoryEvent(HistoryEventDefOf.EnslavedPrisoner, siren.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job();
        }

        public static bool CanContinueEnslavePrisoner(Pawn siren, Pawn prisoner)
        {
            if (!ModsConfig.IdeologyActive || !PrisonerAvailableForSong(siren, prisoner))
            {
                return false;
            }

            PrisonerInteractionModeDef mode = prisoner.guest.ExclusiveInteractionMode;
            return mode == PrisonerInteractionModeDefOf.Enslave || mode == PrisonerInteractionModeDefOf.ReduceWill;
        }

        public static void DoEnslaveInteraction(Pawn siren, Pawn prisoner)
        {
            if (!CanStartEnslavePrisoner(siren, prisoner, showFailReason: false))
            {
                return;
            }

            if (prisoner.guest.will > 0f)
            {
                ReduceWill(siren, prisoner);
            }
            else if (prisoner.guest.IsInteractionDisabled(PrisonerInteractionModeDefOf.ReduceWill))
            {
                EnslavePrisoner(siren, prisoner);
            }

            SetLastInteractTime(prisoner);
        }

        public static Ideo ConversionIdeoFor(Pawn prisoner)
        {
            return prisoner?.guest?.ideoForConversion ?? Faction.OfPlayer?.ideos?.PrimaryIdeo;
        }

        public static bool CanConvertPrisoner(Pawn siren, Pawn prisoner, bool forced)
        {
            return CanStartConvertPrisoner(siren, prisoner) && siren.CanReserve(prisoner, 1, -1, null, forced) && HasReachableInteractablePosition(siren, prisoner);
        }

        public static bool CanStartConvertPrisoner(Pawn siren, Pawn prisoner, bool showFailReason = true)
        {
            return CanContinueConvertPrisoner(siren, prisoner) && ScheduledForInteraction(prisoner, showFailReason);
        }

        public static bool CanContinueConvertPrisoner(Pawn siren, Pawn prisoner)
        {
            if (!ModsConfig.IdeologyActive || Find.IdeoManager.classicMode || !PrisonerAvailableForSong(siren, prisoner))
            {
                return false;
            }

            if (prisoner.guest.ExclusiveInteractionMode != PrisonerInteractionModeDefOf.Convert || !prisoner.RaceProps.Humanlike || prisoner.ideo == null || prisoner.DevelopmentalStage.Baby())
            {
                return false;
            }

            Ideo targetIdeo = ConversionIdeoFor(prisoner);
            return targetIdeo != null && prisoner.Ideo != targetIdeo;
        }

        public static void DoConvertInteraction(Pawn siren, Pawn prisoner)
        {
            if (!CanStartConvertPrisoner(siren, prisoner, showFailReason: false))
            {
                return;
            }

            Ideo targetIdeo = ConversionIdeoFor(prisoner);
            Ideo oldIdeo = prisoner.Ideo;
            Precept_Role oldRole = oldIdeo?.GetRole(prisoner);

            if (prisoner.ideo.IdeoConversionAttempt(CertaintyReductionFor(siren, prisoner), targetIdeo))
            {
                TaggedString letterText = "LetterConvertIdeoAttempt_Success".Translate(siren.Named("INITIATOR"), prisoner.Named("RECIPIENT"), targetIdeo.Named("IDEO"), oldIdeo.Named("OLDIDEO")).Resolve();
                if (oldRole != null)
                {
                    letterText += "\n\n" + "LetterRoleLostLetterIdeoChangedPostfix".Translate(prisoner.Named("PAWN"), oldRole.Named("ROLE"), oldIdeo.Named("OLDIDEO")).Resolve();
                }

                Find.LetterStack.ReceiveLetter("LetterLabelConvertIdeoAttempt_Success".Translate(), letterText, LetterDefOf.PositiveEvent, new LookTargets(prisoner, siren));
            }

            SetLastInteractTime(prisoner);
        }

        public static bool CanSuppressSlave(Pawn siren, Pawn slave, bool forced)
        {
            return CanContinueSuppressSlave(siren, slave) && slave.guest.ScheduledForSlaveSuppression && siren.CanReserve(slave, 1, -1, null, forced) && HasReachableInteractablePosition(siren, slave);
        }

        public static bool CanContinueSuppressSlave(Pawn siren, Pawn slave)
        {
            if (!ModsConfig.IdeologyActive || !CanSirenSing(siren) || slave?.guest == null || !slave.IsSlaveOfColony || !slave.guest.SlaveIsSecure || !slave.Spawned)
            {
                return false;
            }

            if (slave.InMentalState || slave.InAggroMentalState || slave.IsForbidden(siren) || slave.IsFormingCaravan() || slave.Downed || !slave.Awake())
            {
                return false;
            }

            if (slave.guest.slaveInteractionMode != SlaveInteractionModeDefOf.Suppress || slave.needs == null)
            {
                return false;
            }

            return slave.needs.TryGetNeed(out Need_Suppression suppression) && suppression.CanBeSuppressedNow;
        }

        public static bool DoSuppressInteraction(Pawn siren, Pawn slave)
        {
            if (!CanContinueSuppressSlave(siren, slave))
            {
                return false;
            }

            SlaveRebellionUtility.IncrementInteractionSuppression(siren, slave);
            return true;
        }

        public static void SetLastSuppressionTime(Pawn slave)
        {
            if (slave?.mindState != null)
            {
                slave.mindState.lastSlaveSuppressedTick = Find.TickManager.TicksGame;
            }
        }

        private static bool PrisonerAvailableForSong(Pawn siren, Pawn prisoner)
        {
            if (!CanSirenSing(siren) || prisoner?.guest == null || !prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure || !prisoner.Spawned || prisoner.InMentalState || prisoner.InAggroMentalState || prisoner.IsForbidden(siren) || prisoner.IsFormingCaravan())
            {
                return false;
            }

            return prisoner.Awake() && (!prisoner.Downed || prisoner.InBed());
        }

        private static bool ScheduledForInteraction(Pawn prisoner, bool showFailReason)
        {
            if (prisoner.guest.ScheduledForInteraction)
            {
                return true;
            }

            if (showFailReason)
            {
                JobFailReason.Is("PrisonerInteractedTooRecently".Translate());
            }

            return false;
        }

        private static void ReduceResistance(Pawn siren, Pawn prisoner)
        {
            float oldResistance = prisoner.guest.resistance;
            if (oldResistance <= 0f)
            {
                return;
            }

            float moodFactor = ResistanceImpactFactorCurve_Mood.Evaluate(prisoner.needs?.mood == null ? 1f : prisoner.needs.mood.CurInstantLevelPercentage);
            float negotiationFactor = siren.GetStatValue(StatDefOf.NegotiationAbility);
            float resistanceReduce = BaseResistanceReductionPerInteraction * negotiationFactor * moodFactor;
            prisoner.guest.resistance = Mathf.Max(0f, oldResistance - Mathf.Min(resistanceReduce, oldResistance));

            if (siren.Spawned && prisoner.Spawned && siren.Map == prisoner.Map)
            {
                float before = Mathf.Max(0.1f, oldResistance);
                float after = prisoner.guest.resistance > 0f ? Mathf.Max(0.1f, prisoner.guest.resistance) : 0f;
                MoteMaker.ThrowText((siren.DrawPos + prisoner.DrawPos) / 2f, siren.Map, "TextMote_ResistanceReduced".Translate(before.ToString("F1"), after.ToString("F1")), 8f);
            }

            if (prisoner.guest.resistance <= 0f)
            {
                prisoner.guest.SetLastResistanceReduceData(siren, resistanceReduce, negotiationFactor, moodFactor, 1f);
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

        private static void ReduceWill(Pawn siren, Pawn prisoner)
        {
            float oldWill = prisoner.guest.will;
            float reduction = Mathf.Min(oldWill, BaseWillReductionPerInteraction * siren.GetStatValue(StatDefOf.NegotiationAbility));
            prisoner.guest.will = Mathf.Max(0f, oldWill - reduction);

            if (siren.Spawned && prisoner.Spawned && siren.Map == prisoner.Map)
            {
                MoteMaker.ThrowText((siren.DrawPos + prisoner.DrawPos) / 2f, siren.Map, "TextMote_WillReduced".Translate(oldWill.ToString("F1"), prisoner.guest.will.ToString("F1")), 8f);
            }

            if (prisoner.guest.will <= 0f)
            {
                TaggedString message = "MessagePrisonerWillBroken".Translate(siren, prisoner);
                if (prisoner.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.AttemptRecruit))
                {
                    message += " " + "MessagePrisonerWillBroken_RecruitAttempsWillBegin".Translate();
                }

                Messages.Message(message, prisoner, MessageTypeDefOf.PositiveEvent);
            }
        }

        private static void EnslavePrisoner(Pawn siren, Pawn prisoner)
        {
            QuestUtility.SendQuestTargetSignals(prisoner.questTags, "Enslaved", prisoner.Named("SUBJECT"));
            if (!GenGuest.TryEnslavePrisoner(siren, prisoner))
            {
                return;
            }

            Find.LetterStack.ReceiveLetter("LetterLabelEnslavementSuccess".Translate() + ": " + prisoner.LabelCap, "LetterEnslavementSuccess".Translate(siren, prisoner), LetterDefOf.PositiveEvent, new LookTargets(prisoner, siren));
        }

        private static float CertaintyReductionFor(Pawn siren, Pawn prisoner)
        {
            float reduction = BaseCertaintyReductionPerInteraction * siren.GetStatValue(StatDefOf.ConversionPower) * prisoner.GetStatValue(StatDefOf.CertaintyLossFactor) * Find.Storyteller.difficulty.CertaintyReductionFactor(siren, prisoner);
            Precept_Role role = prisoner.Ideo?.GetRole(prisoner);
            if (role != null)
            {
                reduction *= role.def.certaintyLossFactor;
            }

            return reduction;
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
