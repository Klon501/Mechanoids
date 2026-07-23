using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ApexMechanoids
{
    public class MentalState_Duel : MentalState
    {
        public Thing attachedThing;
        public Pawn duelStarter;
        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            pawn.mindState.enemyTarget = this.causedByPawn;
            if (!(this.causedByPawn.MentalState is MentalState_Duel))
            {
                this.duelStarter = this.causedByPawn;
                this.causedByPawn.mindState.mentalStateHandler.TryStartMentalState(this.def, reason: reason, forced: true, forceWake: true, causedByMood: false, otherPawn: this.pawn);
                this.causedByPawn.mindState.mentalStateHandler.CurState.forceRecoverAfterTicks = this.forceRecoverAfterTicks;
            }
            else
                {
                    this.duelStarter = this.pawn;
                    bool isBoss = this.pawn.kindDef?.defName?.EndsWith("_Boss") ?? false;
                EffecterDef startEffecter = isBoss ? ApexEffecterDefsOf.APM_DuelStart_Boss : ApexEffecterDefsOf.APM_DuelStart;
                startEffecter.Spawn(Vector3.Lerp(pawn.DrawPos, causedByPawn.DrawPos, 0.5f).ToIntVec3(), pawn.Map).Cleanup();
            }
            pawn.health.AddHediff(ApexDefsOf.APM_InDuel);
        }
        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;
        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            pawn.mindState.enemyTarget = this.causedByPawn;
            if (this.causedByPawn.DeadOrDowned)
            {
                this.RecoverFromState();
            }
        }
        const float severityPerWin = 1f / 8f; // 7 stages
        public override void PostEnd()
        {
            base.PostEnd();
            if (pawn.HostileTo(causedByPawn) || pawn.HostileTo(causedByPawn.Faction))
            {
                if (causedByPawn.DeadOrDowned)
                {
                    HealthUtility.AdjustSeverity(pawn, ApexDefsOf.APM_DuelWinner, severityPerWin);
                }
                else if (!pawn.DeadOrDowned)
                {
                    pawn.health.AddHediff(ApexDefsOf.APM_DuelDraw);
                } 
            }
            Hediff inDuelHediff = pawn.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_InDuel);
            if (inDuelHediff != null)
            {
                pawn.health.RemoveHediff(inDuelHediff);
            }
            if (!attachedThing.DestroyedOrNull())
            {
                attachedThing.Destroy(DestroyMode.KillFinalize);
            }
            if (!pawn.DeadOrDowned && (pawn.drafter?.ShowDraftGizmo ?? false))
            {
                pawn.drafter.Drafted = true;
            }


            var duelTarget = pawn == duelStarter ? causedByPawn : pawn;
            bool starterIsBoss = duelStarter != null && (duelStarter.kindDef?.defName?.EndsWith("_Boss") ?? false);

            if (duelTarget.DeadOrDowned)
            {
                EffecterDef winEffecter = starterIsBoss ? ApexEffecterDefsOf.APM_DuelWin_Boss : ApexEffecterDefsOf.APM_DuelWin;
                winEffecter.Spawn(pawn, pawn.Map).Cleanup();
            }
            else if (duelStarter != null && duelStarter.DeadOrDowned)
            {
                ApexEffecterDefsOf.APM_DuelLose.Spawn(pawn, pawn.Map).Cleanup();
            }
            else if (pawn == duelStarter)
            {
                EffecterDef drawEffecter = starterIsBoss ? ApexEffecterDefsOf.APM_DuelDraw_Boss : ApexEffecterDefsOf.APM_DuelDraw;
                drawEffecter.Spawn(pawn, pawn.Map).Cleanup();
            }
        }

        public override TaggedString GetBeginLetterText()
        {
            if (this.causedByPawn == null)
            {
                Log.Error("No target. This should have been checked in this mental state's worker.");
                return "";
            }
            return this.def.beginLetter.Formatted(this.pawn.NameShortColored, this.causedByPawn.NameShortColored, this.duelStarter.Named("INITIATOR"), (duelStarter == this.pawn ? causedByPawn : pawn).Named("TARGET")).AdjustedFor(this.pawn, "PAWN", true).Resolve().CapitalizeFirst();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref attachedThing, nameof(attachedThing));
            Scribe_References.Look(ref duelStarter, nameof(duelStarter));
        }
    }
}
