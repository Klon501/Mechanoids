using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Security.Cryptography;
using Verse.AI;

namespace ApexMechanoids
{
    public static class Utils
    {
        public static BodyPartRecord GetNonMissingBodyPart(Pawn pawn, BodyPartDef def, BodyPartGroupDef group = null)
        {
            foreach (var notMissingPart in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (notMissingPart.def == def)
                {
                    if (group != null && !notMissingPart.groups.Contains(group))
                    {
                        continue;
                    }
                    return notMissingPart;
                }
            }

            return null;
        }

        public static List<BodyPartRecord> GetNonMissingBodyParts(Pawn pawn, BodyPartDef def)
        {
            List<BodyPartRecord> matchingParts = new List<BodyPartRecord>();

            foreach (var notMissingPart in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (notMissingPart.def == def)
                {
                    matchingParts.Add(notMissingPart);
                }
            }

            return matchingParts;
        }
        
        #region -- Logs --
        public static void LogMessage(string str) => Log.Message("<color=#9ba08c>[ApexMechanoids]</color> " + str);
        public static void LogWarning(string str) => Log.Warning("<color=#9ba08c>[ApexMechanoids]</color> " + str);
        public static void LogError(string str) => Log.Error("<color=#9ba08c>[ApexMechanoids]</color> " + str);
        public static void LogErrorOnce(string str, int key) => Log.ErrorOnce("<color=#9ba08c>[ApexMechanoids]</color> " + str, key);
        #endregion

        public static void TryDoAbility(Pawn pawn, AbilityDef abilityDef, LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                LogError("Invalid ability target: " + target.ToString());
                return;
            }
            Ability ab = pawn.abilities?.GetAbility(abilityDef);
            if (ab == null)
            {
                LogError("subAbility is null");
                return;
            }
            if (!ab.CanCast)
            {
                LogError("Can't cast subAbility");
                return;
            }
            if (!ab.verb.CanHitTarget(target))
            {
                LogError("Could not hit target: " + target + " from " + pawn.Position);
                return;
            }
            ab.QueueCastingJob(target.Pawn, target.Pawn);
        }

        
        public static bool IsUplinkActiveFor(Pawn mechanitor)
        {

            if (mechanitor == null || mechanitor.Dead || mechanitor.mechanitor == null)
            {
                return false;
            }

            if (!mechanitor.Spawned)
            {
                Thing spawnedParentOrMe = mechanitor.SpawnedParentOrMe;
                if (spawnedParentOrMe is Building_MechCommandCasket)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsUplinkActiveFor(Pawn mechanitor, out Building_MechCommandCasket casket)
        {

            if (mechanitor == null || mechanitor.Dead || mechanitor.mechanitor == null)
            {
                casket = null;
                return false;
            }

            if (!mechanitor.Spawned)
            {
                Thing spawnedParentOrMe = mechanitor.SpawnedParentOrMe;
                if (spawnedParentOrMe is Building_MechCommandCasket)
                {
                    casket = (Building_MechCommandCasket)spawnedParentOrMe;
                    return true;
                }
            }
            casket = null;
            return false;
        }


    }
}
