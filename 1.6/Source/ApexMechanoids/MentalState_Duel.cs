using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using ApexMechanoids.HarmonyPatches;

namespace ApexMechanoids
{
    public class MentalState_Duel : MentalState
    {
        private const float SeverityPerWin = 1f / 8f; // 7 stages

        public Thing attachedThing;
        public Pawn duelStarter;
        private bool restoreSearchAndDestroy;

        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            restoreSearchAndDestroy = SearchAndDestroyDuelStateMemory.Consume(pawn);
            if (!restoreSearchAndDestroy && SearchAndDestroyCompatUtility.TryGetSearchAndDestroyEnabledRaw(pawn, out bool searchAndDestroyEnabled))
            {
                restoreSearchAndDestroy = searchAndDestroyEnabled;
            }

            if (!DuelUtility.IsValidActiveDuelOpponent(pawn, causedByPawn))
            {
                RecoverFromState();
                return;
            }

            pawn.mindState.enemyTarget = causedByPawn;
            if (!(causedByPawn.MentalState is MentalState_Duel))
            {
                duelStarter = causedByPawn;
                causedByPawn.mindState?.mentalStateHandler?.TryStartMentalState(def, reason: reason, forced: true, forceWake: true, causedByMood: false, otherPawn: pawn);
                if (causedByPawn.MentalState is MentalState_Duel targetState)
                {
                    targetState.forceRecoverAfterTicks = forceRecoverAfterTicks;
                }
            }
            else
            {
                duelStarter = pawn;
                bool isBoss = pawn.kindDef?.defName?.EndsWith("_Boss") ?? false;
                EffecterDef startEffecter = isBoss ? ApexEffecterDefsOf.APM_DuelStart_Boss : ApexEffecterDefsOf.APM_DuelStart;
                startEffecter.Spawn(Vector3.Lerp(pawn.DrawPos, causedByPawn.DrawPos, 0.5f).ToIntVec3(), pawn.Map).Cleanup();
            }

            if (pawn.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_InDuel) == null)
            {
                pawn.health.AddHediff(ApexDefsOf.APM_InDuel);
            }
        }

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (!DuelUtility.IsValidActiveDuelOpponent(pawn, causedByPawn))
            {
                RecoverFromState();
                return;
            }

            pawn.mindState.enemyTarget = causedByPawn;
        }

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
                if (restoreSearchAndDestroy)
                {
                    SearchAndDestroyCompatUtility.TrySetSearchAndDestroyEnabled(pawn, true);
                }
            }

            if (!pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            Pawn duelTarget = pawn == duelStarter ? causedByPawn : pawn;
            bool starterIsBoss = duelStarter != null && (duelStarter.kindDef?.defName?.EndsWith("_Boss") ?? false);

            if (!pawn.DeadOrDowned)
            {
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
        }

        public override TaggedString GetBeginLetterText()
        {
            if (causedByPawn == null)
            {
                return "";
            }
            return this.def.beginLetter.Formatted(this.pawn.NameShortColored, this.causedByPawn.NameShortColored, this.duelStarter.Named("INITIATOR"), (duelStarter == this.pawn ? causedByPawn : pawn).Named("TARGET")).AdjustedFor(this.pawn, "PAWN", true).Resolve().CapitalizeFirst();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref attachedThing, nameof(attachedThing));
            Scribe_References.Look(ref duelStarter, nameof(duelStarter));
            Scribe_Values.Look(ref restoreSearchAndDestroy, nameof(restoreSearchAndDestroy), false);
        }
    }
}
