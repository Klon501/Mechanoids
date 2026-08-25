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

        // Vanilla spares outside body parts it is about to destroy. ReduceDamageToPreserveOutsideParts
        // rolls the damage def's overkillPctToDestroyPart against how far past the part's remaining
        // health the hit went, measured as a fraction of that part's *maximum* health, and on a
        // failed roll trims the damage down to leave the part on exactly 1 HP.
        //
        // That rule exists so a colonist does not lose a hand to every stray round. On an Aegis it
        // does the opposite of what the mech is for. A shield carries 153 HP at the Aegis's health
        // scale, so an ordinary rifle round that has been through 0.7 sharp armour overshoots by
        // about 3% of that maximum and rolls roughly a one in twenty chance to break through. The
        // other nineteen shots are trimmed to zero damage against a shield already sitting on 1 HP,
        // which still intercepts every frontal attack in full. Measured over 40 consecutive hits the
        // shield took none of them and never broke.
        //
        // A shield is meant to be shot off, so it is exempted and a hit that reaches zero destroys
        // it. Only the damage path is patched; PawnCapacityUtility asks the same question when it
        // scales what a damaged shield swings for, and that is left on vanilla's answer.
        [HarmonyPatch(typeof(DamageWorker_AddInjury), "ReduceDamageToPreserveOutsideParts")]
        internal static class ReduceDamageToPreserveOutsideParts
        {
            private static bool Prefix(float postArmorDamage, DamageInfo dinfo, Pawn pawn, ref float __result)
            {
                if (dinfo.HitPart == null || pawn == null)
                {
                    return true;
                }

                CompAegis comp = pawn.TryGetComp<CompAegis>();
                if (comp == null || !comp.IsShieldPart(dinfo.HitPart))
                {
                    return true;
                }

                __result = postArmorDamage;
                return false;
            }
        }
    }
}
