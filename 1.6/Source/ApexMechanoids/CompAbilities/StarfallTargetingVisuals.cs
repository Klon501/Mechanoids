using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class Ability_Starfall : Ability
    {
        public Ability_Starfall(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public Ability_Starfall(Pawn pawn, Precept sourcePrecept, AbilityDef def)
            : base(pawn, sourcePrecept, def)
        {
        }

        public override Job GetJob(LocalTargetInfo target, LocalTargetInfo destination)
        {
            return base.GetJob(StarfallTargetingUtility.FreezePawnTargetToCell(target), StarfallTargetingUtility.FreezePawnTargetToCell(destination));
        }
    }

    public class Verb_CastStarfall : Verb_CastAbility
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return base.TryStartCastOn(StarfallTargetingUtility.FreezePawnTargetToCell(castTarg), StarfallTargetingUtility.FreezePawnTargetToCell(destTarg), surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }
    }

    internal static class StarfallTargetingUtility
    {
        public static LocalTargetInfo FreezePawnTargetToCell(LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn)
            {
                return new LocalTargetInfo(target.Cell);
            }

            return target;
        }
    }

    public class Mote_RavagerStarfallTarget : MoteAttached
    {
        private const float MinAlpha = 0.8f;
        private const float MaxAlpha = 1f;
        private const float MinScale = 0.96f;
        private const float MaxScale = 1.04f;
        private const float ScalePulsePeriod = 1.15f;
        private const float AlphaPulsePeriod = 0.72f;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdatePulse();
        }

        public override void Tick()
        {
            base.Tick();
            UpdatePulse();
        }

        private void UpdatePulse()
        {
            float scaleWave = Wave(AgeSecs, ScalePulsePeriod);
            float alphaWave = Wave(AgeSecs + 0.19f, AlphaPulsePeriod);

            Scale = Mathf.Lerp(MinScale, MaxScale, scaleWave);
            instanceColor = new Color(1f, 1f, 1f, Mathf.Lerp(MinAlpha, MaxAlpha, alphaWave));
        }

        private static float Wave(float ageSecs, float period)
        {
            return 0.5f + Mathf.Sin(ageSecs * Mathf.PI * 2f / period) * 0.5f;
        }
    }
}
