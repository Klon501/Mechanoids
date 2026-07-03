using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public static class TerminusOverdriveCapeState
    {
        public static void NotifyCapeVisibilityChanged(Pawn pawn)
        {
            pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public static bool ShouldHideCape(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return pawn.health?.hediffSet?.HasHediff(ApexDefsOf.APM_Hediff_TerminusOverdrive) ?? false;
        }
    }

    public static class TerminusOverdriveCapeUtility
    {
        private const string TerminusBossKindDefName = "APM_Mech_Terminus_Boss";
        private const float VisualSouthOffset = 0.32f;

        public static void SpawnBurst(Pawn caster, ThingDef moteDef, int burstCount, FloatRange speedRange, FloatRange angleOffsetRange, float spawnRadius, float sideOffsetDistance, float verticalOffset)
        {
            if (caster == null || !caster.Spawned || moteDef == null)
            {
                return;
            }

            Map map = caster.Map;
            if (map == null || !caster.Position.ShouldSpawnMotesAt(map))
            {
                return;
            }

            bool isBossVariant = caster.kindDef?.defName == TerminusBossKindDefName;
            Color colorOne = isBossVariant ? Color.white : (caster.Faction?.AllegianceColor ?? caster.DrawColor);

            for (int i = 0; i < burstCount; i++)
            {
                bool throwRight = Rand.Bool;
                float moveAngle = (throwRight ? 90f : 270f) + Rand.Range(angleOffsetRange.min, angleOffsetRange.max);
                float horizontalOffset = (throwRight ? 1f : -1f) * sideOffsetDistance;
                float horizontalJitter = Rand.Range(-spawnRadius, spawnRadius) * 1.6f;
                float verticalJitter = Rand.Range(-spawnRadius, spawnRadius) * 0.9f;
                Vector3 spawnPos = caster.DrawPos + new Vector3(horizontalOffset + horizontalJitter, 0f, VisualSouthOffset + verticalOffset + verticalJitter);

                Mote_TerminusCapeThrown mote = ThingMaker.MakeThing(moteDef) as Mote_TerminusCapeThrown;
                if (mote == null)
                {
                    continue;
                }

                float moveSpeed = Rand.Range(speedRange.min, speedRange.max);
                float startRotation = Rand.Range(-18f, 18f);
                float startRotationRate = Rand.Range(-18f, 18f);
                int settleAfterTicks = Rand.RangeInclusive(36, 64);
                mote.Launch(spawnPos, moveAngle, moveSpeed, startRotation, startRotationRate, colorOne, !throwRight, isBossVariant, settleAfterTicks);
                GenSpawn.Spawn(mote, spawnPos.ToIntVec3(), map);
            }
        }
    }

    public class CompAbility_TerminusOverdriveCapeWarmup : AbilityComp
    {
        private int ticksUntilNextSpawn;

        public CompProperties_TerminusOverdriveCapeWarmup Props => (CompProperties_TerminusOverdriveCapeWarmup)props;

        public override void CompTick()
        {
            base.CompTick();

            Pawn caster = parent.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            if (!parent.wasCastingOnPrevTick)
            {
                ticksUntilNextSpawn = 0;
                return;
            }

            if (ticksUntilNextSpawn > 0)
            {
                ticksUntilNextSpawn--;
                return;
            }

            ticksUntilNextSpawn = Props.spawnIntervalTicks;
            TerminusOverdriveCapeUtility.SpawnBurst(caster, Props.moteDef, Props.burstCount, Props.speedRange, Props.angleOffsetRange, Props.spawnRadius, Props.sideOffsetDistance, Props.verticalOffset);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextSpawn, nameof(ticksUntilNextSpawn));
        }
    }

    public class CompProperties_TerminusOverdriveCapeWarmup : AbilityCompProperties
    {
        public CompProperties_TerminusOverdriveCapeWarmup()
        {
            compClass = typeof(CompAbility_TerminusOverdriveCapeWarmup);
        }

        public ThingDef moteDef;
        public int spawnIntervalTicks = 10;
        public int burstCount = 1;
        public float spawnRadius = 0.18f;
        public float sideOffsetDistance = 0.45f;
        public float verticalOffset = 0.14f;
        public FloatRange speedRange = new FloatRange(0.18f, 0.3f);
        public FloatRange angleOffsetRange = new FloatRange(-28f, 28f);
    }

    public class CompAbilityEffect_TerminusOverdriveCapeBurst : CompAbilityEffect
    {
        public new CompProperties_TerminusOverdriveCapeBurst Props => (CompProperties_TerminusOverdriveCapeBurst)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            TerminusOverdriveCapeState.NotifyCapeVisibilityChanged(parent.pawn);
            TerminusOverdriveCapeUtility.SpawnBurst(parent.pawn, Props.moteDef, Props.burstCount, Props.speedRange, Props.angleOffsetRange, Props.spawnRadius, Props.sideOffsetDistance, Props.verticalOffset);
        }
    }

    public class CompProperties_TerminusOverdriveCapeBurst : CompProperties_AbilityEffect
    {
        public CompProperties_TerminusOverdriveCapeBurst()
        {
            compClass = typeof(CompAbilityEffect_TerminusOverdriveCapeBurst);
        }

        public ThingDef moteDef;
        public int burstCount = 1;
        public float spawnRadius = 0.06f;
        public float sideOffsetDistance = 0f;
        public float verticalOffset = 0.06f;
        public FloatRange speedRange = new FloatRange(0.9f, 1.15f);
        public FloatRange angleOffsetRange = new FloatRange(-10f, 10f);
    }

    public class HediffComp_TerminusOverdriveCapeVisibility : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            TerminusOverdriveCapeState.NotifyCapeVisibilityChanged(Pawn);
        }

        public override void CompPostPostRemoved()
        {
            TerminusOverdriveCapeState.NotifyCapeVisibilityChanged(Pawn);
        }
    }

    public class HediffCompProperties_TerminusOverdriveCapeVisibility : HediffCompProperties
    {
        public HediffCompProperties_TerminusOverdriveCapeVisibility()
        {
            compClass = typeof(HediffComp_TerminusOverdriveCapeVisibility);
        }
    }
}