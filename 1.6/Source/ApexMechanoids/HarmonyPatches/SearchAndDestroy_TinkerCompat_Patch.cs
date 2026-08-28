using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_TinkerCompat_Patch
    {
        private static readonly TinkerSearchAndDestroyJobGiver tinkerJobGiver = new TinkerSearchAndDestroyJobGiver();

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!TinkerRepairUtility.IsTinker(pawn) || !SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsTinkerSupportJob(currentJob))
            {
                SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(currentJob);
                return;
            }

            if (!CanReplaceWithTinkerSupport(__result))
            {
                return;
            }

            Job supportJob = tinkerJobGiver.TryGiveJob(pawn);
            if (supportJob == null)
            {
                return;
            }

            SearchAndDestroyCompatUtility.ProtectApexAbilityJobFromOverride(supportJob);
            __result = new ThinkResult(supportJob, __result.SourceNode, __result.Tag);
        }

        private static bool CanReplaceWithTinkerSupport(ThinkResult result)
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

        private static bool IsTinkerSupportJob(Job job)
        {
            if (job == null)
            {
                return false;
            }

            if (job.def == ApexDefsOf.APM_RepairMech)
            {
                return true;
            }

            AbilityDef abilityDef = job.ability?.def;
            return abilityDef == ApexDefsOf.APM_DefenceMatrix || abilityDef == ApexDefsOf.APM_BlindingLaser;
        }

        private sealed class TinkerSearchAndDestroyJobGiver : JobGiver_AITinkerCombat
        {
            public TinkerSearchAndDestroyJobGiver()
            {
                allowPlayerControlled = true;
                useCombatWaitFallback = false;
            }
        }
    }
}
