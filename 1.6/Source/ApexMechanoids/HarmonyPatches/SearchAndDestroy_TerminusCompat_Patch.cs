using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_TerminusCompat_Patch
    {
        private static readonly JobGiver_AITerminusHookFight terminusJobGiver = new JobGiver_AITerminusHookFight
        {
            targetAcquireRadius = 30f,
            minHookDistance = TerminusHookUtility.DefaultMinAIHookDistance
        };

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!TerminusHookUtility.IsTerminus(pawn) || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsTerminusHookJob(currentJob))
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(currentJob);
                return;
            }

            if (!CanReplaceWithTerminusHook(__result))
            {
                return;
            }

            Job hookJob = terminusJobGiver.TryGiveJob(pawn);
            if (hookJob == null)
            {
                return;
            }

            SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(hookJob);
            __result = new ThinkResult(hookJob, __result.SourceNode, __result.Tag);
        }

        private static bool CanReplaceWithTerminusHook(ThinkResult result)
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

        private static bool IsTerminusHookJob(Job job)
        {
            return job?.ability?.def?.defName == "APM_HookPawn";
        }
    }
}
