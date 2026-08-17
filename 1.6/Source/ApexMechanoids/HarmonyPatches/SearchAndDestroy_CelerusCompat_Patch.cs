using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_CelerusCompat_Patch
    {
        private static readonly CelerusSearchAndDestroyJobGiver celerusJobGiver = new CelerusSearchAndDestroyJobGiver();
        private static readonly CelerusSearchAndDestroyFallbackJobGiver fallbackJobGiver = new CelerusSearchAndDestroyFallbackJobGiver();

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!CelerusRaidUtility.IsCelerus(pawn) || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsCelerusAbilityJob(currentJob))
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(currentJob);
                return;
            }

            if (!CanReplaceWithCelerusRaid(__result))
            {
                return;
            }

            Job celerusJob = celerusJobGiver.TryGiveJob(pawn);
            if (celerusJob != null)
            {
                if (IsCelerusAbilityJob(celerusJob))
                {
                    SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(celerusJob);
                }

                __result = new ThinkResult(celerusJob, __result.SourceNode, __result.Tag);
                return;
            }

            Job fallbackJob = fallbackJobGiver.TryGiveJob(pawn);
            if (fallbackJob != null)
            {
                __result = new ThinkResult(fallbackJob, __result.SourceNode, __result.Tag);
            }
        }

        private static bool CanReplaceWithCelerusRaid(ThinkResult result)
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

        private static bool IsCelerusAbilityJob(Job job)
        {
            return CelerusRaidUtility.IsCelerusAbility(job?.ability?.def);
        }

        private sealed class CelerusSearchAndDestroyJobGiver : JobGiver_AICelerusAbilityFight
        {
            public CelerusSearchAndDestroyJobGiver()
            {
                allowPlayerControlled = true;
                targetAcquireRadius = 65f;
                targetKeepRadius = 72f;
            }
        }

        private sealed class CelerusSearchAndDestroyFallbackJobGiver : JobGiver_AIFightEnemies
        {
            public CelerusSearchAndDestroyFallbackJobGiver()
            {
                targetAcquireRadius = 65f;
                targetKeepRadius = 72f;
            }
        }
    }
}
