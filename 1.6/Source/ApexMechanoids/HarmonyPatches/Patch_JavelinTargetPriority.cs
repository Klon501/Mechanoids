using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    /// <summary>
    /// Put on a weapon ThingDef to make its wielder pick the heaviest thing in the fight and stay on
    /// it, rather than shooting at whatever happens to be nearest. Meant for slow-cycling anti-armour
    /// weapons, where firing at a light target is not a small mistake but a wasted ten seconds.
    /// </summary>
    public class DefModExtension_JavelinTargetPriority : DefModExtension
    {
        public float neutralCombatPower = 150f;
        public float scorePerCombatPower = 0.06f;
        public float maxCombatPowerScore = 40f;
        public float minCombatPowerScore = -12f;

        public float scorePerArmourRating = 15f;
        public float maxArmourScore = 18f;

        public float scorePerLockStack = 12f;
        public float maxLockScore = 36f;

        // Which stacking hediff counts as "already softened this one up". Leave null to score on
        // toughness alone.
        public HediffDef lockHediff;

        // Read once per scored target rather than per field access.
        public JavelinTargetPriorityParams Params => new JavelinTargetPriorityParams
        {
            neutralCombatPower = neutralCombatPower,
            scorePerCombatPower = scorePerCombatPower,
            maxCombatPowerScore = maxCombatPowerScore,
            minCombatPowerScore = minCombatPowerScore,
            scorePerArmourRating = scorePerArmourRating,
            maxArmourScore = maxArmourScore,
            scorePerLockStack = scorePerLockStack,
            maxLockScore = maxLockScore
        };
    }

    /// <summary>
    /// Vanilla's shooting score is distance-led: it starts at 60, subtracts the range, and gives a
    /// +40 stickiness bonus only while the last shot at that target is under 300 ticks old. The
    /// javelin's shot cycle is 558 ticks plus flight time, so that stickiness has always expired by
    /// the time it picks again, and the distance term then hands it the nearest target - which is how
    /// a mech with a 9.3-second anti-armour cycle ends up spending missiles on the flimsiest thing on
    /// the map while something heavy walks past.
    ///
    /// The offset is added after vanilla's TargetPriorityFactor multiply, so it is not scaled by it.
    /// Everything without the mod extension is untouched.
    /// </summary>
    [HarmonyPatch(typeof(AttackTargetFinder), "GetShootingTargetScore")]
    internal static class Patch_AttackTargetFinder_GetShootingTargetScore
    {
        private static void Postfix(IAttackTarget target, Verb verb, ref float __result)
        {
            DefModExtension_JavelinTargetPriority props =
                verb?.EquipmentSource?.def?.GetModExtension<DefModExtension_JavelinTargetPriority>();
            if (props == null || target?.Thing == null)
            {
                return;
            }

            __result += JavelinTargetPriority.ScoreOffset(Profile(target.Thing, props), props.Params);
        }

        private static JavelinTargetProfile Profile(Thing thing, DefModExtension_JavelinTargetPriority props)
        {
            if (!(thing is Pawn pawn))
            {
                return new JavelinTargetProfile { isPawn = false };
            }

            return new JavelinTargetProfile
            {
                isPawn = true,
                // combatPower is the only threat rating every pawn kind carries, and it is what the
                // storyteller itself budgets raids with, so it already encodes "a gazer is worth
                // seven tinkers" without this mod having to keep its own table.
                combatPower = pawn.kindDef?.combatPower ?? props.neutralCombatPower,
                armourRating = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp),
                lockStacks = LockStacks(pawn, props)
            };
        }

        private static int LockStacks(Pawn pawn, DefModExtension_JavelinTargetPriority props)
        {
            if (props.lockHediff == null || pawn.health?.hediffSet == null)
            {
                return 0;
            }

            return (pawn.health.hediffSet.GetFirstHediffOfDef(props.lockHediff) as Hediff_JavelinMissileLock)?.Stacks ?? 0;
        }
    }
}
