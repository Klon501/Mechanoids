using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_SirenCompat_Patch
    {
        private static readonly JobGiver_AISirenLureFight sirenJobGiver = new JobGiver_AISirenLureFight
        {
            targetAcquireRadius = 30f,
            recentFirefightTicks = SirenLureUtility.DefaultRecentFirefightTicks
        };

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!SirenLureUtility.IsSiren(pawn) || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsSirenLureJob(currentJob))
            {
                ProtectSirenLureJob(currentJob);
                return;
            }

            if (!CanReplaceWithSirenLure(__result))
            {
                return;
            }

            Job lureJob = sirenJobGiver.TryGiveJob(pawn);
            if (lureJob == null)
            {
                return;
            }

            ProtectSirenLureJob(lureJob);
            __result = new ThinkResult(lureJob, __result.SourceNode, __result.Tag);
        }

        private static bool CanReplaceWithSirenLure(ThinkResult result)
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
                || job.def == JobDefOf.AttackStatic
                || job.def == JobDefOf.Wait
                || job.def == JobDefOf.Wait_Combat;
        }

        private static bool IsSirenLureJob(Job job)
        {
            return job?.ability?.def?.defName == "APM_Ability_SirenLure"
                || job?.def?.defName == "APM_SirenLureChannel";
        }

        private static void ProtectSirenLureJob(Job job)
        {
            if (job == null)
            {
                return;
            }

            SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(job);
            if (job.def?.defName == "APM_SirenLureChannel")
            {
                job.expiryInterval = 0;
                job.checkOverrideOnExpire = false;
            }
        }
    }
}
