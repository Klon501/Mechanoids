using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AIDashAbilityFight : JobGiver_AIFightEnemies
    {
        public List<AbilityDef> abilities;
        public bool allowPlayerControlled = false;
        public int approachJobExpiryInterval = 30;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AIDashAbilityFight obj = (JobGiver_AIDashAbilityFight)base.DeepCopy(resolve);
            obj.abilities = abilities;
            obj.allowPlayerControlled = allowPlayerControlled;
            obj.approachJobExpiryInterval = approachJobExpiryInterval;
            return obj;
        }

        public bool OwnsDashAbility(Pawn pawn)
        {
            return FindAbility(pawn, requireCastable: false) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return null;
            }

            Ability dash = FindAbility(pawn, requireCastable: true);
            if (dash == null)
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null || (enemyTarget is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible()))
            {
                return null;
            }

            LocalTargetInfo targetInfo = enemyTarget;
            if (!dash.AICanTargetNow(targetInfo))
            {
                return null;
            }

            if (dash.verb.CanHitTarget(targetInfo))
            {
                return dash.GetJob(targetInfo, targetInfo);
            }

            if (!TryFindShootingPosition(pawn, out IntVec3 dest, dash.verb) || !dest.IsValid || dest == pawn.Position)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.expiryInterval = approachJobExpiryInterval;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = true;
            job.collideWithPawns = true;
            return job;
        }

        public override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState?.enemyTarget;
            Verb verb = verbToUse ?? FindAbility(pawn, requireCastable: true)?.verb;
            if (enemyTarget == null || verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }

            return CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = verb,
                maxRangeFromTarget = verb.EffectiveRange,
                wantCoverFromTarget = false
            }, out dest);
        }

        private Ability FindAbility(Pawn pawn, bool requireCastable)
        {
            if (abilities == null || pawn?.abilities == null)
            {
                return null;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                AbilityDef abilityDef = abilities[i];
                if (abilityDef == null)
                {
                    continue;
                }

                Ability ability = pawn.abilities.GetAbility(abilityDef);
                if (ability == null || ability.verb == null)
                {
                    continue;
                }

                if (!requireCastable || ability.CanCast)
                {
                    return ability;
                }
            }

            return null;
        }

        private bool CanRunFor(Pawn pawn)
        {
            return pawn != null
                && Utils.CanRunAutonomousPawn(pawn)
                && pawn.Faction != null
                && pawn.abilities != null
                && pawn.mindState != null
                && pawn.CurJob?.ability == null
                && (!pawn.IsPlayerControlled || allowPlayerControlled)
                && pawn.health?.capacities != null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }
    }
}
