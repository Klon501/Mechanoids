using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class MechRepairUtility_Patch
    {
        // A mech already claimed by a repair station is not up for grabs by a colonist.
        [HarmonyPatch(typeof(MechRepairUtility), nameof(MechRepairUtility.CanRepair))]
        internal static class CanRepair_RepairStation
        {
            private static bool Prefix(Pawn mech, ref bool __result)
            {
                if (!Building_RepairStation.IsPawnClaimedByAnyRepairStation(mech))
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        // A colonist repairs shield injuries like any other damage, but rebuilding a destroyed
        // shield is CompAegis's job. JobDriver_RepairMech ends the moment CanRepair goes false, so
        // reporting "nothing to do" here is what keeps a destroyed shield out of vanilla's reach:
        // RepairTick itself is perfectly willing to delete a Hediff_MissingPart and hand the whole
        // shield back in one tick.
        [HarmonyPatch(typeof(MechRepairUtility), nameof(MechRepairUtility.CanRepair))]
        internal static class CanRepair_Aegis
        {
            private static bool Prefix(Pawn mech, ref bool __result)
            {
                CompAegis comp = mech?.TryGetComp<CompAegis>();
                if (comp == null)
                {
                    return true;
                }

                if (HasColonistRepairableDamage(mech, comp.Props.shieldPart)
                    || MechRepairUtility.IsMissingWeapon(mech))
                {
                    return true;
                }

                __result = false;
                return false;
            }

            // Any injury, or any missing part other than a shield.
            private static bool HasColonistRepairableDamage(Pawn mech, BodyPartDef shieldPart)
            {
                List<Hediff> hediffs = mech.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    Hediff hediff = hediffs[i];
                    if (hediff is Hediff_Injury)
                    {
                        return true;
                    }

                    if (hediff is Hediff_MissingPart && hediff.Part != null && hediff.Part.def != shieldPart)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Vanilla heals shield injuries like any other part. Charge extra mech energy for the shield
        // HP restored, on top of the flat cost JobDriver_RepairMech already pays, so shields are more
        // expensive to patch up than plating.
        [HarmonyPatch(typeof(MechRepairUtility), nameof(MechRepairUtility.RepairTick), new[] { typeof(Pawn) })]
        internal static class RepairTick
        {
            private static void Prefix(Pawn mech, out float __state)
            {
                CompAegis comp = mech?.TryGetComp<CompAegis>();
                __state = comp != null ? comp.CurShieldHP : 0f;
            }

            private static void Postfix(Pawn mech, float __state)
            {
                CompAegis comp = mech?.TryGetComp<CompAegis>();
                if (comp == null || mech.needs?.energy == null)
                {
                    return;
                }

                float restored = comp.CurShieldHP - __state;
                if (restored <= 0f)
                {
                    return;
                }

                mech.needs.energy.CurLevel -= restored
                    * mech.GetStatValue(StatDefOf.MechEnergyLossPerHP)
                    * comp.Props.repairEnergyCostMultiplier;
            }
        }
    }
}
