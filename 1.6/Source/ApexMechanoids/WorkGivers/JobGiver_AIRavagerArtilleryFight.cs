using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AIRavagerArtilleryFight : JobGiver_AIFightEnemies
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!RavagerArtilleryUtility.CanUseArtillery(pawn) || !RavagerArtilleryUtility.AutoFireEnabled(pawn))
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null)
            {
                return null;
            }

            if (enemyTarget is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible())
            {
                return null;
            }

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            if (allowManualCastWeapons)
            {
                Job abilityJob = GetAbilityJob(pawn, enemyTarget);
                if (abilityJob != null)
                {
                    return abilityJob;
                }
            }

            Verb verb = pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
            LocalTargetInfo targetCell = new LocalTargetInfo(enemyTarget.Position);
            if (!RavagerArtilleryUtility.CanFireAtCell(pawn, targetCell, verb))
            {
                return null;
            }

            pawn.pather?.StopDead();
            return RavagerArtilleryUtility.MakeArtilleryAttackJob(targetCell, verb);
        }

        public override Thing FindAttackTarget(Pawn pawn)
        {
            if (!RavagerArtilleryUtility.CanUseArtillery(pawn) || !RavagerArtilleryUtility.AutoFireEnabled(pawn))
            {
                return null;
            }

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = pawn.TryGetAttackVerb(null, allowManualCastWeapons);
            if (verb == null || verb.verbProps.IsMeleeAttack)
            {
                return null;
            }

            float maxRange = verb.EffectiveRange;
            if (targetAcquireRadius > 0f && targetAcquireRadius < maxRange)
            {
                maxRange = targetAcquireRadius;
            }

            return RavagerArtilleryUtility.FindBestPawnTarget(pawn, verb, maxRange);
        }

        public override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            return base.ExtraTargetValidator(pawn, target) && RavagerArtilleryUtility.IsValidPawnTarget(pawn, target);
        }

        public override bool ShouldLoseTarget(Pawn pawn)
        {
            if (!RavagerArtilleryUtility.CanUseArtillery(pawn) || !RavagerArtilleryUtility.AutoFireEnabled(pawn))
            {
                return true;
            }

            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null || enemyTarget.Destroyed || !enemyTarget.Spawned || enemyTarget.Map != pawn.Map)
            {
                return true;
            }

            if (!RavagerArtilleryUtility.IsValidPawnTarget(pawn, enemyTarget))
            {
                return true;
            }

            if (Find.TickManager.TicksGame - pawn.mindState.lastEngageTargetTick > TicksSinceEngageToLoseTarget)
            {
                return true;
            }

            if ((float)(pawn.Position - enemyTarget.Position).LengthHorizontalSquared > targetKeepRadius * targetKeepRadius)
            {
                return true;
            }

            if ((enemyTarget as IAttackTarget)?.ThreatDisabled(pawn) ?? false)
            {
                return true;
            }

            if (RavagerArtilleryUtility.CanFireAtCell(pawn, new LocalTargetInfo(enemyTarget.Position)))
            {
                return false;
            }

            return !pawn.CanReach(enemyTarget, PathEndMode.Touch, Danger.Deadly);
        }
    }
}
