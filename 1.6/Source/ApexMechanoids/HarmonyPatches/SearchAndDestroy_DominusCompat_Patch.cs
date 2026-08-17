using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_DominusCompat_Patch
    {
        private static readonly DominusSearchAndDestroyDuelJobGiver duelJobGiver = new DominusSearchAndDestroyDuelJobGiver();
        private static readonly DominusSearchAndDestroyFallbackJobGiver fallbackJobGiver = new DominusSearchAndDestroyFallbackJobGiver();

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!DuelUtility.IsDominus(pawn) || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsDominusDuelJob(currentJob))
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(currentJob);
                return;
            }

            if (!CanReplaceWithDominusCombat(__result))
            {
                return;
            }

            Job duelJob = duelJobGiver.TryGiveJob(pawn);
            if (duelJob != null)
            {
                if (IsDominusDuelJob(duelJob))
                {
                    SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(duelJob);
                }

                __result = new ThinkResult(duelJob, __result.SourceNode, __result.Tag);
                return;
            }

            Job fallbackJob = fallbackJobGiver.TryGiveJob(pawn);
            if (fallbackJob != null)
            {
                __result = new ThinkResult(fallbackJob, __result.SourceNode, __result.Tag);
            }
        }

        private static bool CanReplaceWithDominusCombat(ThinkResult result)
        {
            Job job = result.Job;
            if (job == null)
            {
                return true;
            }

            if (result.FromQueue || job.playerForced || job.ability != null)
            {
                return false;
            }

            return job.def == JobDefOf.Goto
                || job.def == JobDefOf.AttackMelee
                || job.def == JobDefOf.Wait
                || job.def == JobDefOf.Wait_Combat;
        }

        private static bool IsDominusDuelJob(Job job)
        {
            AbilityDef abilityDef = job?.ability?.def;
            return abilityDef == ApexDefsOf.APM_Mech_Duel || abilityDef == ApexDefsOf.APM_Mech_Duel_Boss;
        }

        private sealed class DominusSearchAndDestroyDuelJobGiver : JobGiver_AIDominusDuelFight
        {
            public DominusSearchAndDestroyDuelJobGiver()
            {
                targetAcquireRadius = 30f;
                targetKeepRadius = 35f;
            }
        }

        private sealed class DominusSearchAndDestroyFallbackJobGiver : JobGiver_AIFightEnemies
        {
            public DominusSearchAndDestroyFallbackJobGiver()
            {
                targetAcquireRadius = 30f;
                targetKeepRadius = 35f;
            }
        }
    }
}
