using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AISirenLureFight : ThinkNode_JobGiver
    {
        public float targetAcquireRadius = 30f;
        public int recentFirefightTicks = SirenLureUtility.DefaultRecentFirefightTicks;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AISirenLureFight obj = (JobGiver_AISirenLureFight)base.DeepCopy(resolve);
            obj.targetAcquireRadius = targetAcquireRadius;
            obj.recentFirefightTicks = recentFirefightTicks;
            return obj;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SirenLureUtility.TryMakeBestAILureJob(pawn, targetAcquireRadius, recentFirefightTicks, out Job job))
            {
                return null;
            }

            return job;
        }
    }

    public static class SirenLureUtility
    {
        private const string SirenDefName = "APM_Mech_Siren";
        private const string LureAbilityDefName = "APM_Ability_SirenLure";

        public const int DefaultRecentFirefightTicks = 600;

        public static bool IsSiren(Pawn pawn)
        {
            return pawn?.def?.defName == SirenDefName;
        }

        public static bool CanStartLureOnTarget(Pawn targetPawn, Pawn caster, AbilityDef abilityDef, JobDef channelJobDef, bool scanOtherSirenJobs = true)
        {
            if (!CanAffectTarget(targetPawn, caster) || targetPawn.CurJobDef == JobDefOf.GotoMindControlled)
            {
                return false;
            }

            return !scanOtherSirenJobs || !HasOtherLureJobOnTarget(targetPawn, caster, abilityDef, channelJobDef);
        }

        public static bool TryMakeBestAILureJob(Pawn pawn, float targetAcquireRadius, int recentFirefightTicks, out Job job)
        {
            job = null;
            Ability ability = GetLureAbility(pawn);
            JobDef channelJobDef = GetChannelJobDef(ability);
            if (!TryFindBestAILureTarget(pawn, ability, channelJobDef, targetAcquireRadius, recentFirefightTicks, out Pawn target))
            {
                return false;
            }

            LocalTargetInfo targetInfo = target;
            job = ability.GetJob(targetInfo, targetInfo);
            if (job == null)
            {
                return false;
            }

            job.expiryInterval = 0;
            job.checkOverrideOnExpire = false;
            return true;
        }

        private static bool TryFindBestAILureTarget(Pawn pawn, Ability ability, JobDef channelJobDef, float maxRange, int recentFirefightTicks, out Pawn target)
        {
            target = null;
            if (!CanUseLure(pawn, ability))
            {
                return false;
            }

            if (maxRange <= 0f || maxRange > ability.verb.EffectiveRange)
            {
                maxRange = ability.verb.EffectiveRange;
            }

            Pawn bestAnyTarget = null;
            Pawn bestRangedTarget = null;
            float bestAnyScore = float.MinValue;
            float bestRangedScore = float.MinValue;
            bool activeFirefight = false;
            List<IAttackTarget> potentialTargets = pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);

            for (int i = 0; i < potentialTargets.Count; i++)
            {
                IAttackTarget attackTarget = potentialTargets[i];
                Pawn candidate = attackTarget.Thing as Pawn;
                if (!IsValidAILureTarget(pawn, candidate, attackTarget, ability, channelJobDef, maxRange))
                {
                    continue;
                }

                bool rangedThreat = HasRangedThreatVerb(candidate as IAttackTargetSearcher);
                bool activeRangedThreat = rangedThreat && IsActiveFirefightThreat(candidate, attackTarget, recentFirefightTicks);
                float score = TargetScore(pawn, candidate, attackTarget, activeRangedThreat);

                if (activeRangedThreat)
                {
                    activeFirefight = true;
                }
                if (rangedThreat && (bestRangedTarget == null || score > bestRangedScore))
                {
                    bestRangedTarget = candidate;
                    bestRangedScore = score;
                }
                if (bestAnyTarget == null || score > bestAnyScore)
                {
                    bestAnyTarget = candidate;
                    bestAnyScore = score;
                }
            }

            target = activeFirefight ? bestRangedTarget : bestAnyTarget;
            return target != null;
        }

        private static bool IsValidAILureTarget(Pawn caster, Pawn targetPawn, IAttackTarget attackTarget, Ability ability, JobDef channelJobDef, float maxRange)
        {
            if (targetPawn == null || attackTarget == null || attackTarget.Thing != targetPawn)
            {
                return false;
            }
            if (!targetPawn.HostileTo(caster) || targetPawn.IsPsychologicallyInvisible() || targetPawn.Fogged())
            {
                return false;
            }
            if (attackTarget.ThreatDisabled(caster) || !AttackTargetFinder.IsAutoTargetable(attackTarget))
            {
                return false;
            }
            if (!CanStartLureOnTarget(targetPawn, caster, ability?.def, channelJobDef, scanOtherSirenJobs: false))
            {
                return false;
            }

            LocalTargetInfo targetInfo = targetPawn;
            if (!ability.def.verbProperties.targetParams.CanTarget(targetPawn))
            {
                return false;
            }

            float distanceSquared = (caster.Position - targetPawn.Position).LengthHorizontalSquared;
            if (distanceSquared > maxRange * maxRange)
            {
                return false;
            }

            float verbMinRange = ability.verb.verbProps.EffectiveMinRange(targetInfo, caster);
            if (verbMinRange > 0f && distanceSquared < verbMinRange * verbMinRange)
            {
                return false;
            }

            return ability.verb.CanHitTarget(targetInfo);
        }

        public static bool CanAffectTarget(Pawn targetPawn, Pawn caster)
        {
            if (caster == null || caster.Destroyed || caster.Dead || caster.Downed || !caster.Spawned || caster.Map == null)
            {
                return false;
            }
            if (targetPawn == null || targetPawn == caster || targetPawn.Destroyed || targetPawn.Dead || targetPawn.Downed || !targetPawn.Spawned || targetPawn.Map != caster.Map)
            {
                return false;
            }
            if (!(targetPawn.RaceProps?.IsFlesh ?? false) || targetPawn.RaceProps.IsMechanoid)
            {
                return false;
            }
            if (targetPawn.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f)
            {
                return false;
            }
            return targetPawn.health?.capacities != null && targetPawn.health.capacities.CapableOf(PawnCapacityDefOf.Hearing);
        }

        private static bool HasOtherLureJobOnTarget(Pawn targetPawn, Pawn caster, AbilityDef abilityDef, JobDef channelJobDef)
        {
            if (caster?.Map == null)
            {
                return false;
            }

            IReadOnlyList<Pawn> pawns = caster.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == null || other == caster)
                {
                    continue;
                }

                Job job = other.CurJob;
                if (job == null)
                {
                    continue;
                }

                if (abilityDef != null && job.ability?.def == abilityDef && job.targetA.Thing == targetPawn)
                {
                    return true;
                }
                if (channelJobDef != null && job.def == channelJobDef && job.targetA.Thing == targetPawn)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanUseLure(Pawn pawn, Ability ability)
        {
            return pawn != null
                && IsSiren(pawn)
                && Utils.CanRunAutonomousPawn(pawn)
                && pawn.abilities != null
                && pawn.CurJob?.ability == null
                && ability?.pawn == pawn
                && ability.def?.defName == LureAbilityDefName
                && ability.def.aiCanUse
                && ability.CanCast
                && ability.verb != null;
        }

        private static Ability GetLureAbility(Pawn pawn)
        {
            List<Ability> abilities = pawn?.abilities?.AllAbilitiesForReading;
            if (abilities == null)
            {
                return null;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                Ability ability = abilities[i];
                if (ability?.def?.defName == LureAbilityDefName)
                {
                    return ability;
                }
            }

            return null;
        }

        private static JobDef GetChannelJobDef(Ability ability)
        {
            return ability?.CompOfType<CompAbilityEffect_SirenLureChannel>()?.ChannelProps?.jobDef;
        }

        private static bool HasRangedThreatVerb(IAttackTargetSearcher searcher)
        {
            Verb verb = searcher?.CurrentEffectiveVerb;
            return verb != null && verb.verbProps != null && verb.verbProps.Ranged;
        }

        private static bool IsActiveFirefightThreat(Pawn target, IAttackTarget attackTarget, int recentFirefightTicks)
        {
            if (target == null)
            {
                return false;
            }
            if (target.IsAttacking() || attackTarget.TargetCurrentlyAimingAt.IsValid)
            {
                return true;
            }
            if (recentFirefightTicks <= 0 || target.mindState == null)
            {
                return false;
            }

            int ticksGame = Find.TickManager.TicksGame;
            return (target.mindState.lastAttackTargetTick > 0 && ticksGame - target.mindState.lastAttackTargetTick <= recentFirefightTicks)
                || (target.mindState.lastRangedHarmTick > 0 && ticksGame - target.mindState.lastRangedHarmTick <= recentFirefightTicks);
        }

        private static float TargetScore(Pawn caster, Pawn target, IAttackTarget attackTarget, bool activeRangedThreat)
        {
            float distanceSquared = (caster.Position - target.Position).LengthHorizontalSquared;
            float score = -distanceSquared;
            if (target == caster.mindState?.enemyTarget)
            {
                score += 500f;
            }
            if (activeRangedThreat)
            {
                score += 1000f;
            }
            if (target.IsAttacking())
            {
                score += 200f;
            }
            if (attackTarget.TargetCurrentlyAimingAt.IsValid)
            {
                score += 150f;
            }
            if (target.kindDef != null)
            {
                score += target.kindDef.combatPower * 0.25f;
            }
            return score;
        }
    }
}
