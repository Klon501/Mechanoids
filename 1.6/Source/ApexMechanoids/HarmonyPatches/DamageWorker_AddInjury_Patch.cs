using HarmonyLib;
using Verse;

namespace ApexMechanoids.HarmonyPatches
{
    internal static class DamageWorker_AddInjury_Patch
    {
        // GetExactPartFromDamageInfo is the one place that settles the final hit part, so a single
        // postfix here is enough to make an Aegis catch the hit on a shield.
        //
        // It used to take two patches to do this: a prefix here stashed the chosen shield in public
        // statics and a postfix on HediffSet.GetRandomNotMissingPart read them back out. That meant
        // patching a method every pawn in the game calls on every damage roll, and leaving shared
        // state live across the whole damage call, where nested damage could clear it out from under
        // the outer one.
        [HarmonyPatch(typeof(DamageWorker_AddInjury), "GetExactPartFromDamageInfo")]
        internal static class GetExactPartFromDamageInfo
        {
            private static void Postfix(DamageInfo dinfo, Pawn pawn, ref BodyPartRecord __result)
            {
                // A caller that named the part it wants keeps it: surgery, targeted abilities, and
                // damage propagating into inner parts all arrive with HitPart already set.
                if (__result == null || dinfo.HitPart != null)
                {
                    return;
                }

                CompAegis comp = pawn.TryGetComp<CompAegis>();
                if (comp == null)
                {
                    return;
                }

                // Which shield covers the mech depends on where the attacker is standing, so both
                // pawns have to actually be on a map together for the question to mean anything.
                if (!(dinfo.Instigator is Pawn instigator)
                    || !instigator.Spawned
                    || !pawn.Spawned
                    || instigator.Map != pawn.Map)
                {
                    return;
                }

                BodyPartRecord shield = comp.ShieldInterceptingAttackFrom(instigator);
                if (shield != null)
                {
                    __result = shield;
                }
            }
        }
    }
}
