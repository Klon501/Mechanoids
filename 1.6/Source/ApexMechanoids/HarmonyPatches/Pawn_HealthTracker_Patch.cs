using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class Pawn_HealthTracker_Patch
    {
        // A mech dies once its injuries add up to 150 times its health scale, which on an Aegis is
        // 573. ShouldBeDeadFromLethalDamageThreshold builds that total by walking the hediff list and
        // adding the severity of everything that is a Hediff_Injury. A destroyed body part is a
        // Hediff_MissingPart, which is not one, so a shield that has been blown clean off adds
        // nothing to the total while a shield that is merely chewed up adds its whole shortfall - up
        // to 153, better than a quarter of everything the mech has to give.
        //
        // That asymmetry is what killed the client's Aegis. CompAegis rebuilds a destroyed shield by
        // swapping the missing part hediff for a single injury carrying the entire shortfall, so one
        // regeneration step hands the mech back a shield and charges it 152 points of lethal damage
        // in the same instant. On a mech already carrying heavy body damage that is enough to cross
        // the line, and it dies with nothing having hit it - measured at 458 of 573 going into the
        // step and 610 coming out of it.
        //
        // So shield damage is taken off the mech's death total entirely. Its shields can be ground
        // down and shot off and rebuilt as often as they like; what kills an Aegis is damage to the
        // Aegis. This also settles the same asymmetry in the other direction, where losing a shield
        // used to move the mech further from death than keeping a broken one.
        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromLethalDamageThreshold))]
        internal static class ShouldBeDeadFromLethalDamageThreshold
        {
            private static void Postfix(Pawn_HealthTracker __instance, ref bool __result)
            {
                // Taking damage off the total can only ever move a pawn away from death, so a false
                // here is already the final answer. Every pawn in the game runs this on every hediff
                // change; leaving early is what keeps that cheap.
                if (!__result)
                {
                    return;
                }

                Pawn pawn = __instance.pawn;
                CompAegis comp = pawn?.TryGetComp<CompAegis>();
                if (comp == null)
                {
                    return;
                }

                float total = 0f;
                List<Hediff> hediffs = __instance.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    if (hediffs[i] is Hediff_Injury injury && !comp.IsShieldPart(injury.Part))
                    {
                        total += injury.Severity;
                    }
                }

                __result = total >= __instance.LethalDamageThreshold;
            }
        }
    }
}
