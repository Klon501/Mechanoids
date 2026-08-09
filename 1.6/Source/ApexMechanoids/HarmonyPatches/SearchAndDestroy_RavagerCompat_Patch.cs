using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids.HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("MemeGoddess.SearchAndDestroy")]
    public static class SearchAndDestroy_RavagerCompat_Patch
    {
        private const string RavagerDefName = "APM_Mech_Ravager";
        private const string StarfallDefName = "APM_Starfall";

        private static readonly RavagerSearchAndDestroyJobGiver ravagerJobGiver = new RavagerSearchAndDestroyJobGiver();

        [HarmonyPostfix]
        private static void DetermineNextJobPostfix(Pawn_JobTracker __instance, ref ThinkResult __result)
        {
            Pawn pawn = SearchAndDestroyCompatUtility.GetPawn(__instance);
            if (!EnabledForRavager(pawn))
            {
                return;
            }

            Job currentJob = __result.Job;
            if (IsRavagerStarfallJob(currentJob))
            {
                if (!currentJob.playerForced && RavagerArtilleryUtility.TryMakeBestStarfallJob(pawn, 80f, out Job scoredStarfallJob))
                {
                    AllowStarfallWarmup(scoredStarfallJob);
                    __result = new ThinkResult(scoredStarfallJob, __result.SourceNode, __result.Tag);
                }
                else
                {
                    AllowStarfallWarmup(currentJob);
                }

                return;
            }

            if (!CanReplaceWithRavagerCombat(__result))
            {
                return;
            }

            Job ravagerJob = ravagerJobGiver.TryGiveJob(pawn);
            if (ravagerJob == null)
            {
                return;
            }

            AllowStarfallWarmup(ravagerJob);
            __result = new ThinkResult(ravagerJob, __result.SourceNode, __result.Tag);
        }

        private static bool EnabledForRavager(Pawn pawn)
        {
            return pawn?.def?.defName == RavagerDefName && SearchAndDestroyCompatUtility.SearchAndDestroyEnabledFor(pawn);
        }

        private static bool CanReplaceWithRavagerCombat(ThinkResult result)
        {
            Job job = result.Job;
            if (job == null)
            {
                return true;
            }

            if (result.FromQueue || job.playerForced)
            {
                return false;
            }

            return job.def == JobDefOf.Wait_Combat || job.def == JobDefOf.Wait;
        }

        private static bool IsRavagerStarfallJob(Job job)
        {
            return job != null && (job.ability?.def?.defName == StarfallDefName || job.verbToUse is Verb_CastStarfall);
        }

        private static void AllowStarfallWarmup(Job job)
        {
            if (!IsRavagerStarfallJob(job))
            {
                return;
            }

            job.expiryInterval = 0;
            job.checkOverrideOnExpire = false;
        }

        private sealed class RavagerSearchAndDestroyJobGiver : JobGiver_AIRavagerArtilleryFight
        {
            public RavagerSearchAndDestroyJobGiver()
            {
                targetAcquireRadius = 80f;
                targetKeepRadius = 90f;
            }
        }
    }
}
