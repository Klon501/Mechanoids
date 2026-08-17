using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_ConquerorSacrificeController : CompProperties
    {
        public AbilityDef abilityDef;
        public HediffDef sacrificeHediff;
        public List<PawnKindDef> kindDefs = new List<PawnKindDef>();
        public int checkIntervalTicks = 120;
        public float aiMaxHealthPct = 0.3f;
        public float radius = 12f;
        public int minBuffTargets = 2;
        public bool autoCastForPlayer = false;

        public CompProperties_ConquerorSacrificeController()
        {
            compClass = typeof(CompConquerorSacrificeController);
        }
    }

    public class CompConquerorSacrificeController : ThingComp
    {
        public CompProperties_ConquerorSacrificeController Props => (CompProperties_ConquerorSacrificeController)props;

        private Pawn Pawn => parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureAbility();
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = Pawn;
            if (!Utils.CanRunAutonomousPawn(pawn))
            {
                return;
            }

            if (!Props.autoCastForPlayer && pawn.Faction == Faction.OfPlayer)
            {
                return;
            }

            int interval = Props.checkIntervalTicks > 0 ? Props.checkIntervalTicks : 120;
            if (!pawn.IsHashIntervalTick(interval))
            {
                return;
            }

            EnsureAbility();

            Ability ability = pawn.abilities?.GetAbility(Props.abilityDef);
            if (ability == null || !ability.CanCast || pawn.CurJob?.ability == ability)
            {
                return;
            }

            if (!ConquerorSacrificeUtility.CanAutoSacrifice(pawn, Props, out _))
            {
                return;
            }

            LocalTargetInfo selfTarget = pawn;
            if (!ability.CanApplyOn(selfTarget))
            {
                return;
            }

            ability.QueueCastingJob(selfTarget, selfTarget);
        }

        private void EnsureAbility()
        {
            Pawn pawn = Pawn;
            if (pawn?.abilities == null || Props.abilityDef == null)
            {
                return;
            }

            if (pawn.abilities.GetAbility(Props.abilityDef) == null)
            {
                pawn.abilities.GainAbility(Props.abilityDef);
            }
        }
    }

    public class CompProperties_ConquerorSacrificeGiveHediff : CompProperties_AbilityGiveHediff
    {
        public List<PawnKindDef> kindDefs = new List<PawnKindDef>();

        public CompProperties_ConquerorSacrificeGiveHediff()
        {
            compClass = typeof(CompAbilityEffect_ConquerorSacrificeGiveHediff);
        }
    }

    public class CompAbilityEffect_ConquerorSacrificeGiveHediff : CompAbilityEffect_GiveHediff
    {
        public new CompProperties_ConquerorSacrificeGiveHediff Props => (CompProperties_ConquerorSacrificeGiveHediff)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn == parent.pawn)
            {
                return;
            }

            if (!ConquerorSacrificeUtility.IsValidBuffTarget(parent.pawn, targetPawn, Props.kindDefs, Props.hediffDef))
            {
                return;
            }

            base.Apply(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
            {
                return false;
            }

            if (targetPawn == parent.pawn)
            {
                return base.Valid(target, throwMessages);
            }

            return base.Valid(target, throwMessages)
                && ConquerorSacrificeUtility.IsValidBuffTarget(parent.pawn, targetPawn, Props.kindDefs, Props.hediffDef);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }
    }

    internal static class ConquerorSacrificeUtility
    {
        public static bool CanAutoSacrifice(Pawn caster, CompProperties_ConquerorSacrificeController props, out int validBuffTargets)
        {
            validBuffTargets = 0;
            if (!IsValidCaster(caster) || props == null || props.abilityDef == null || props.sacrificeHediff == null)
            {
                return false;
            }

            if (caster.health?.summaryHealth == null || caster.health.summaryHealth.SummaryHealthPercent > props.aiMaxHealthPct)
            {
                return false;
            }

            if (HasHediff(caster, props.sacrificeHediff))
            {
                return false;
            }

            List<Pawn> pawns = caster.Map.mapPawns.SpawnedPawnsInFaction(caster.Faction);
            float radius = props.radius > 0f ? props.radius : 12f;
            float radiusSq = radius * radius;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn == caster || pawn.Map != caster.Map || !pawn.Spawned || pawn.Dead)
                {
                    continue;
                }

                if (!IsConquerorKind(pawn, props.kindDefs))
                {
                    continue;
                }

                if (HasHediff(pawn, props.sacrificeHediff) || IsCastingAbility(pawn, props.abilityDef))
                {
                    return false;
                }

                if (pawn.Position.DistanceToSquared(caster.Position) > radiusSq)
                {
                    continue;
                }

                if (IsWorthBuffing(pawn, props.sacrificeHediff))
                {
                    validBuffTargets++;
                }
            }

            return validBuffTargets >= props.minBuffTargets;
        }

        public static bool IsValidBuffTarget(Pawn caster, Pawn target, List<PawnKindDef> kindDefs, HediffDef sacrificeHediff)
        {
            if (!IsValidCaster(caster) || target == null || target == caster || target.Map != caster.Map || target.Faction != caster.Faction)
            {
                return false;
            }

            if (!IsConquerorKind(target, kindDefs))
            {
                return false;
            }

            return IsWorthBuffing(target, sacrificeHediff);
        }

        private static bool IsValidCaster(Pawn pawn)
        {
            return pawn != null
                && Utils.CanRunAutonomousPawn(pawn);
        }

        private static bool IsWorthBuffing(Pawn pawn, HediffDef sacrificeHediff)
        {
            return pawn != null
                && pawn.Spawned
                && !pawn.Destroyed
                && !pawn.Dead
                && !pawn.Downed
                && Utils.IsAwakeAndNotDormant(pawn)
                && !HasHediff(pawn, sacrificeHediff);
        }

        private static bool IsConquerorKind(Pawn pawn, List<PawnKindDef> kindDefs)
        {
            if (pawn?.kindDef == null)
            {
                return false;
            }

            if (kindDefs.NullOrEmpty())
            {
                return pawn.def?.defName?.StartsWith("APM_Mech_Conqueror", StringComparison.Ordinal) == true;
            }

            for (int i = 0; i < kindDefs.Count; i++)
            {
                if (pawn.kindDef == kindDefs[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasHediff(Pawn pawn, HediffDef hediffDef)
        {
            return pawn?.health?.hediffSet != null
                && hediffDef != null
                && pawn.health.hediffSet.HasHediff(hediffDef);
        }

        private static bool IsCastingAbility(Pawn pawn, AbilityDef abilityDef)
        {
            return pawn?.CurJob?.ability?.def == abilityDef
                || pawn?.abilities?.GetAbility(abilityDef)?.Casting == true;
        }
    }
}
