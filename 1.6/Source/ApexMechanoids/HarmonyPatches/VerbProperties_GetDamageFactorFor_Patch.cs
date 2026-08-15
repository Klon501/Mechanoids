using HarmonyLib;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class VerbProperties_GetDamageFactorFor_Patch
    {
        // An Aegis fights with its shields, so a wrecked shield ought to cost it something to swing.
        //
        // Vanilla already scales a tool by the average health of the parts it is linked to, but both
        // shield tools are marked ensureLinkedBodyPartsGroupAlwaysUsable, and that puts a floor of 0.4
        // under the scaling: a shield blown clean off still swings at 40% of full power. The floor is
        // there so a pawn cannot end up with no usable melee verb at all, which for this mech would be
        // every verb it has, so it cannot simply be dropped.
        //
        // Instead the floor moves down to destroyedShieldDamageFactor. The verb stays usable, since a
        // factor of exactly zero would take it out of the melee list, and the mech keeps fighting -
        // just not on the strength of a shield it no longer has.
        [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.GetDamageFactorFor),
            new[] { typeof(Tool), typeof(Pawn), typeof(HediffComp_VerbGiver) })]
        internal static class GetDamageFactorFor
        {
            private static void Postfix(Tool tool, Pawn attacker, HediffComp_VerbGiver hediffCompSource,
                ref float __result, VerbProperties __instance)
            {
                // The cheap tests first: this runs on every melee verb of every pawn.
                if (tool?.linkedBodyPartsGroup == null || attacker == null || __result <= 0f)
                {
                    return;
                }

                // A verb coming off a hediff scales on that hediff's own part and never reaches the
                // linked-group branch, so there is no floor there to move.
                if (hediffCompSource?.parent?.Part != null)
                {
                    return;
                }

                if (!__instance.AdjustedEnsureLinkedBodyPartsGroupAlwaysUsable(tool))
                {
                    return;
                }

                CompAegis comp = attacker.TryGetComp<CompAegis>();
                if (comp == null || !comp.IsShieldGroup(tool.linkedBodyPartsGroup))
                {
                    return;
                }

                float natural = PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(
                    attacker.health.hediffSet, tool.linkedBodyPartsGroup);
                __result *= AegisShieldDamageRules.FloorAdjustment(natural, comp.Props.destroyedShieldDamageFactor);
            }
        }
    }
}
