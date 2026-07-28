using HarmonyLib;
using RimWorld;
using System.Linq;
using System.Reflection;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class MechRepairUtility_Patch
    {
        // Allow vanilla/mod repair jobs to fix Aegis shields, but stop colonists from getting stuck
        // when the ONLY remaining damage is a destroyed (missing) shield part, which vanilla repair
        // cannot rebuild (CompAegis slowly regenerates those instead).
        [HarmonyLib.HarmonyPatch(typeof(MechRepairUtility), nameof(MechRepairUtility.CanRepair))]
        internal static class CanRepair
        {
            private static bool Prefix(Pawn mech, ref bool __result)
            {
                if (Building_RepairStation.IsPawnClaimedByAnyRepairStation(mech))
                {
                    __result = false;
                    return false;
                }

                CompAegis comp = mech?.TryGetComp<CompAegis>();
                if (comp == null || comp.Ext == null)
                {
                    return true;
                }

                // Repairable by a colonist = any injury (including shield injuries) or any missing
                // NON-shield part. Missing shield parts alone are left to CompAegis.
                bool hasRepairable = mech.health.hediffSet.hediffs.Any(h =>
                    h is Hediff_Injury ||
                    (h is Hediff_MissingPart mp && mp.Part != null && mp.Part.def != comp.Ext.shieldPart));

                if (!hasRepairable)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        // Vanilla repair heals shield injuries like any other part. Charge extra mech energy for the
        // shield HP restored so shields are more expensive to repair than normal armour.
        [HarmonyLib.HarmonyPatch]
        internal static class RepairTick
        {
            // MechRepairUtility.RepairTick has multiple overloads in 1.6, so name-only matching is
            // ambiguous. Resolve the single-Pawn overload (falling back to any RepairTick) explicitly.
            private static MethodBase TargetMethod()
            {
                MethodInfo[] candidates = AccessTools.GetDeclaredMethods(typeof(MechRepairUtility))
                    .Where(m => m.Name == "RepairTick")
                    .ToArray();

                return candidates.FirstOrDefault(m =>
                        {
                            ParameterInfo[] ps = m.GetParameters();
                            return ps.Length == 1 && ps[0].ParameterType == typeof(Pawn);
                        })
                    ?? candidates.FirstOrDefault();
            }

            private static void Prefix(Pawn mech, out float __state)
            {
                __state = 0f;
                CompAegis comp = mech?.TryGetComp<CompAegis>();
                if (comp != null && comp.Ext != null)
                {
                    __state = comp.CurShieldHP;
                }
            }

            private static void Postfix(Pawn mech, float __state)
            {
                CompAegis comp = mech?.TryGetComp<CompAegis>();
                if (comp == null || comp.Ext == null || mech.needs?.energy == null)
                {
                    return;
                }

                float restored = comp.CurShieldHP - __state;
                if (restored <= 0f)
                {
                    return;
                }

                float extraEnergy = restored
                    * mech.GetStatValue(StatDefOf.MechEnergyLossPerHP)
                    * comp.Ext.repairEnergyCostMultiplier;

                mech.needs.energy.CurLevel -= extraEnergy;
            }
        }
    }
}
