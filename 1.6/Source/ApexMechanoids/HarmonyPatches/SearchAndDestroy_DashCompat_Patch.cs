using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_DashCompat_Patch
    {
        private const int fallbackJobExpiryInterval = 30;

        private static JobGiver_AIDashAbilityFight dashJobGiver;
        private static JobGiver_AIFightEnemies fallbackJobGiver;

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (pawn == null || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            EnsureJobGivers();
            if (!dashJobGiver.OwnsDashAbility(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsDashAbilityJob(currentJob))
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(currentJob);
                return;
            }

            if (!CanReplaceWithDash(__result))
            {
                return;
            }

            Job dashJob = dashJobGiver.TryGiveJob(pawn);
            if (dashJob != null)
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(dashJob);
                __result = new ThinkResult(dashJob, __result.SourceNode, __result.Tag);
                return;
            }

            Job fallbackJob = fallbackJobGiver.TryGiveJob(pawn);
            if (fallbackJob != null)
            {
                fallbackJob.expiryInterval = fallbackJobExpiryInterval;
                __result = new ThinkResult(fallbackJob, __result.SourceNode, __result.Tag);
            }
        }

        private static void EnsureJobGivers()
        {
            if (dashJobGiver != null)
            {
                return;
            }

            dashJobGiver = new JobGiver_AIDashAbilityFight
            {
                abilities = new List<AbilityDef> { ApexDefsOf.APM_ShieldCharge, ApexDefsOf.APM_Bladehopp },
                allowPlayerControlled = true,
                targetAcquireRadius = 65f,
                targetKeepRadius = 72f
            };

            fallbackJobGiver = new JobGiver_AIFightEnemies
            {
                targetAcquireRadius = 65f,
                targetKeepRadius = 72f
            };
        }

        private static bool CanReplaceWithDash(ThinkResult result)
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

        private static bool IsDashAbilityJob(Job job)
        {
            AbilityDef abilityDef = job?.ability?.def;
            return abilityDef == ApexDefsOf.APM_ShieldCharge || abilityDef == ApexDefsOf.APM_Bladehopp;
        }
    }
}
