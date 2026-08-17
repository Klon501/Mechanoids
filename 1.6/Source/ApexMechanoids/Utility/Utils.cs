using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace ApexMechanoids
{
    public static class ApexMechColors
    {
        public static readonly Color DominusColor  = new Color(230f / 255f, 130f / 255f,  40f / 255f);
        public static readonly Color CelerusColor  = new Color(160f / 255f,  60f / 255f, 230f / 255f);
        public static readonly Color TerminusColor = new Color(229f / 255f, 211f / 255f, 127f / 255f);
        public static readonly Color PlayerColor = new Color(163f / 255f, 180f / 255f, 187f / 255f);

        public static Color GetAbilityColor(Pawn caster)
        {
            string kind = caster?.kindDef?.defName;
            if (kind != null)
            {
                bool isBoss = kind.EndsWith("_Boss");
                if (kind.Contains("Dominus")) return isBoss ? DominusColor  : PlayerColor;
                if (kind.Contains("Celerus")) return isBoss ? CelerusColor  : PlayerColor;
                if (kind.Contains("Terminus")) return isBoss ? TerminusColor : PlayerColor;
                if (kind.Contains("Vassal"))  return PlayerColor;
            }
            return caster?.Faction?.AllegianceColor ?? Color.white;
        }
    }

    public static class Utils
    {
        public static bool IsAwakeAndNotDormant(Pawn pawn)
        {
            if (pawn == null || pawn.health?.capacities == null || !pawn.Awake() || pawn.IsSelfShutdown() || pawn.IsDeactivated())
            {
                return false;
            }

            // RestUtility.Awake() does not cover mech cluster dormancy.
            CompCanBeDormant dormantComp = pawn.TryGetComp<CompCanBeDormant>();
            return dormantComp == null || dormantComp.Awake;
        }

        public static bool CanRunAutonomousPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Spawned
                && pawn.Map != null
                && IsAwakeAndNotDormant(pawn);
        }

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

            if (mechanitor == null || mechanitor.Dead || !MechanitorUtility.IsMechanitor(mechanitor))
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

            if (mechanitor == null || mechanitor.Dead || !MechanitorUtility.IsMechanitor(mechanitor))
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


        public static void ReplaceBuilding(Building oldBuilding, ThingDef NewBuildingDef)
        {
            if (oldBuilding == null)
            {
                return;
            }
  
            Thing newThing = ThingMaker.MakeThing(NewBuildingDef);
            Building building = newThing as Building;

            if(building == null)
            {
                return;
            }

            Map map = oldBuilding.Map;
            IntVec3 pos = oldBuilding.Position;
            Rot4 rot4 = oldBuilding.Rotation;


            oldBuilding.Kill(null);

            GenSpawn.Spawn(building, pos, map, rot4, WipeMode.Vanish, false);

        }

    }
}
