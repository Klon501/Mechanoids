using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class RavagerArtilleryUtility
    {
        private const string StarfallDefName = "APM_Starfall";
        private const float TargetEnemyHitScore = 1000f;
        private const float TargetDistanceScore = 4f;
        private const float TargetFriendlyHitPenalty = 5000f;
        private const int AutonomousArtilleryJobExpiryTicks = 240;

        private static readonly List<Pawn> tmpTargetPawns = new List<Pawn>();
        private static AbilityDef cachedStarfallDef;

        private static AbilityDef StarfallDef
        {
            get
            {
                if (cachedStarfallDef == null)
                {
                    cachedStarfallDef = DefDatabase<AbilityDef>.GetNamedSilentFail(StarfallDefName);
                }

                return cachedStarfallDef;
            }
        }

        public static bool CanUseArtillery(Pawn pawn)
        {
            return Utils.CanRunAutonomousPawn(pawn) && !pawn.Position.Roofed(pawn.Map);
        }

        public static bool AutoFireEnabled(Pawn pawn)
        {
            if (!IsPlayerControlled(pawn))
            {
                return true;
            }

            return pawn.TryGetComp<CompRavagerArtilleryController>()?.AutoFireEnabled ?? false;
        }

        public static bool IsManualArtilleryJob(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job != null && job.def == ApexDefsOf.APM_RavagerArtilleryAttack && job.playerForced;
        }

        public static bool AutoAbilityBlockedByArtilleryToggle(Pawn pawn, Ability ability)
        {
            return ability?.def?.defName == StarfallDefName && AutoAbilityBlockedByArtilleryToggle(pawn);
        }

        public static bool AutoAbilityBlockedByArtilleryToggle(Pawn pawn)
        {
            CompRavagerArtilleryController controller = pawn?.TryGetComp<CompRavagerArtilleryController>();
            return IsPlayerControlled(pawn) && controller != null && !controller.AutoFireEnabled;
        }

        public static bool IsManualStarfallJob(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job != null && job.playerForced && (job.ability?.def?.defName == StarfallDefName || job.verbToUse is Verb_CastStarfall);
        }

        public static bool IsPlayerControlled(Pawn pawn)
        {
            return pawn?.Faction == Faction.OfPlayer;
        }

        public static LocalTargetInfo TargetCell(LocalTargetInfo target)
        {
            return target.IsValid ? new LocalTargetInfo(target.Cell) : LocalTargetInfo.Invalid;
        }

        public static Job MakeArtilleryAttackJob(LocalTargetInfo targetCell, Verb verb, bool autonomousTargetRefresh = false)
        {
            Job job = JobMaker.MakeJob(ApexDefsOf.APM_RavagerArtilleryAttack, TargetCell(targetCell));
            job.verbToUse = verb;
            job.maxNumStaticAttacks = 1;
            job.endIfCantShootTargetFromCurPos = true;
            if (autonomousTargetRefresh)
            {
                job.expiryInterval = AutonomousArtilleryJobExpiryTicks;
                job.checkOverrideOnExpire = true;
            }
            else
            {
                job.expiryInterval = 0;
                job.checkOverrideOnExpire = false;
            }
            return job;
        }

        public static bool TryMakeBestStarfallJob(Pawn pawn, float maxRange, out Job job)
        {
            job = null;
            Ability ability = GetStarfallAbility(pawn);
            if (ability == null)
            {
                return false;
            }

            LocalTargetInfo targetCell;
            if (!TryFindBestStarfallTarget(pawn, ability, maxRange, out targetCell))
            {
                return false;
            }

            job = ability.GetJob(targetCell, targetCell);
            if (job == null)
            {
                return false;
            }

            job.expiryInterval = 0;
            job.checkOverrideOnExpire = false;
            return true;
        }

        public static bool CanFireAtCell(Pawn pawn, LocalTargetInfo target, Verb verb = null)
        {
            if (!CanUseArtillery(pawn) || !target.IsValid || !target.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            Verb attackVerb = verb ?? pawn.TryGetAttackVerb(null, !pawn.IsColonist && !pawn.IsColonySubhuman);
            if (attackVerb == null || attackVerb.verbProps.IsMeleeAttack)
            {
                return false;
            }

            RoofDef roof = target.Cell.GetRoof(pawn.Map);
            return (roof == null || !roof.isThickRoof) && attackVerb.CanHitTarget(target.Cell);
        }

        public static Ability GetStarfallAbility(Pawn pawn)
        {
            AbilityDef starfallDef = StarfallDef;
            if (starfallDef == null || pawn?.abilities == null)
            {
                return null;
            }

            return pawn.abilities.GetAbility(starfallDef);
        }

        public static bool TryFindBestStarfallTarget(Pawn pawn, Ability ability, float maxRange, out LocalTargetInfo targetCell)
        {
            targetCell = LocalTargetInfo.Invalid;
            if (!CanUseStarfall(pawn, ability))
            {
                return false;
            }

            Verb verb = ability.verb;
            if (maxRange <= 0f || maxRange > verb.EffectiveRange)
            {
                maxRange = verb.EffectiveRange;
            }

            StarfallImpactProfile profile = StarfallImpactProfile.For(ability);
            GatherValidTargets(pawn, verb, maxRange, tmpTargetPawns);
            try
            {
                if (tmpTargetPawns.Count == 0)
                {
                    return false;
                }

                bool found = false;
                float bestScore = float.MinValue;
                IntVec3 bestCell = IntVec3.Invalid;
                for (int i = 0; i < tmpTargetPawns.Count; i++)
                {
                    TryScoreStarfallCandidate(pawn, ability, tmpTargetPawns[i].Position, profile, tmpTargetPawns, ref found, ref bestScore, ref bestCell);
                }

                float pairDistanceSquared = profile.PairCandidateDistance * profile.PairCandidateDistance;
                for (int i = 0; i < tmpTargetPawns.Count; i++)
                {
                    for (int j = i + 1; j < tmpTargetPawns.Count; j++)
                    {
                        if ((float)(tmpTargetPawns[i].Position - tmpTargetPawns[j].Position).LengthHorizontalSquared > pairDistanceSquared)
                        {
                            continue;
                        }

                        TryScoreStarfallCandidate(pawn, ability, MidpointCell(tmpTargetPawns[i].Position, tmpTargetPawns[j].Position), profile, tmpTargetPawns, ref found, ref bestScore, ref bestCell);
                    }
                }

                if (!found)
                {
                    return false;
                }

                targetCell = new LocalTargetInfo(bestCell);
                return true;
            }
            finally
            {
                tmpTargetPawns.Clear();
            }
        }

        public static bool TryFindBestArtilleryTarget(Pawn pawn, Verb verb, float maxRange, out LocalTargetInfo targetCell)
        {
            targetCell = LocalTargetInfo.Invalid;
            if (!CanUseArtillery(pawn) || verb == null || verb.verbProps.IsMeleeAttack)
            {
                return false;
            }

            if (maxRange <= 0f || maxRange > verb.EffectiveRange)
            {
                maxRange = verb.EffectiveRange;
            }

            float impactRadius = ProjectileExplosionRadius(verb);
            GatherValidTargets(pawn, verb, maxRange, tmpTargetPawns);
            try
            {
                if (tmpTargetPawns.Count == 0)
                {
                    return false;
                }

                bool found = false;
                float bestScore = float.MinValue;
                IntVec3 bestCell = IntVec3.Invalid;
                for (int i = 0; i < tmpTargetPawns.Count; i++)
                {
                    TryScoreRadialCandidate(pawn, verb, tmpTargetPawns[i].Position, impactRadius, tmpTargetPawns, ref found, ref bestScore, ref bestCell);
                }

                float pairDistanceSquared = impactRadius * impactRadius * 4f;
                for (int i = 0; i < tmpTargetPawns.Count; i++)
                {
                    for (int j = i + 1; j < tmpTargetPawns.Count; j++)
                    {
                        if ((float)(tmpTargetPawns[i].Position - tmpTargetPawns[j].Position).LengthHorizontalSquared > pairDistanceSquared)
                        {
                            continue;
                        }

                        TryScoreRadialCandidate(pawn, verb, MidpointCell(tmpTargetPawns[i].Position, tmpTargetPawns[j].Position), impactRadius, tmpTargetPawns, ref found, ref bestScore, ref bestCell);
                    }
                }

                if (!found)
                {
                    return false;
                }

                targetCell = new LocalTargetInfo(bestCell);
                return true;
            }
            finally
            {
                tmpTargetPawns.Clear();
            }
        }

        public static Pawn FindBestPawnTarget(Pawn pawn, Verb verb, float maxRange)
        {
            if (!CanUseArtillery(pawn) || verb == null || verb.verbProps.IsMeleeAttack)
            {
                return null;
            }

            float impactRadius = ProjectileExplosionRadius(verb);
            Pawn bestTarget = null;
            float bestScore = float.MinValue;
            GatherValidTargets(pawn, verb, maxRange, tmpTargetPawns);
            try
            {
                for (int i = 0; i < tmpTargetPawns.Count; i++)
                {
                    Pawn target = tmpTargetPawns[i];
                    int enemyHits = CountRadialEnemyHits(target.Position, impactRadius, tmpTargetPawns);
                    int friendlyHits = CountRadialFriendlyHits(pawn, target.Position, impactRadius);
                    float score = TargetScore(pawn, target.Position, enemyHits, friendlyHits);

                    if (score > bestScore)
                    {
                        bestTarget = target;
                        bestScore = score;
                    }
                }

                return bestTarget;
            }
            finally
            {
                tmpTargetPawns.Clear();
            }
        }

        public static bool IsValidPawnTarget(Pawn pawn, Thing target)
        {
            Pawn targetPawn = target as Pawn;
            if (pawn == null || targetPawn == null || targetPawn.Dead || targetPawn.Downed || !targetPawn.Spawned || targetPawn.Map != pawn.Map)
            {
                return false;
            }

            if (!targetPawn.HostileTo(pawn) || targetPawn.IsPsychologicallyInvisible())
            {
                return false;
            }

            if (targetPawn is IAttackTarget attackTarget && attackTarget.ThreatDisabled(pawn))
            {
                return false;
            }

            RoofDef roof = targetPawn.Position.GetRoof(targetPawn.Map);
            return roof == null || !roof.isThickRoof;
        }

        private static bool CanUseStarfall(Pawn pawn, Ability ability)
        {
            return CanUseArtillery(pawn)
                && ability != null
                && ability.def?.defName == StarfallDefName
                && ability.def.aiCanUse
                && ability.CanCast
                && ability.verb != null
                && pawn.stances?.FullBodyBusy != true
                && !AutoAbilityBlockedByArtilleryToggle(pawn, ability);
        }

        private static void GatherValidTargets(Pawn pawn, Verb verb, float maxRange, List<Pawn> targets)
        {
            targets.Clear();
            if (!CanUseArtillery(pawn) || verb == null || pawn.Map == null)
            {
                return;
            }

            float maxRangeSquared = maxRange * maxRange;
            IReadOnlyList<Pawn> spawnedPawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn target = spawnedPawns[i];
                if (!IsValidPawnTarget(pawn, target))
                {
                    continue;
                }

                float distanceSquared = (pawn.Position - target.Position).LengthHorizontalSquared;
                if (distanceSquared > maxRangeSquared)
                {
                    continue;
                }

                float minRange = verb.verbProps.EffectiveMinRange(target, pawn);
                if (minRange > 0f && distanceSquared < minRange * minRange)
                {
                    continue;
                }

                if (!CanFireAtCell(pawn, new LocalTargetInfo(target.Position), verb))
                {
                    continue;
                }

                targets.Add(target);
            }
        }

        private static void TryScoreStarfallCandidate(Pawn pawn, Ability ability, IntVec3 cell, StarfallImpactProfile profile, List<Pawn> validTargets, ref bool found, ref float bestScore, ref IntVec3 bestCell)
        {
            if (!cell.IsValid || !cell.InBounds(pawn.Map))
            {
                return;
            }

            LocalTargetInfo targetCell = new LocalTargetInfo(cell);
            if (!CanFireAtCell(pawn, targetCell, ability.verb) || !ability.AICanTargetNow(targetCell))
            {
                return;
            }

            int enemyHits = CountStarfallEnemyHits(pawn, cell, profile, validTargets);
            if (enemyHits <= 0)
            {
                return;
            }

            int friendlyHits = CountStarfallFriendlyHits(pawn, cell, profile);
            float score = TargetScore(pawn, cell, enemyHits, friendlyHits);
            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestCell = cell;
            }
        }

        private static void TryScoreRadialCandidate(Pawn pawn, Verb verb, IntVec3 cell, float impactRadius, List<Pawn> validTargets, ref bool found, ref float bestScore, ref IntVec3 bestCell)
        {
            if (!cell.IsValid || !cell.InBounds(pawn.Map))
            {
                return;
            }

            LocalTargetInfo targetCell = new LocalTargetInfo(cell);
            if (!CanFireAtCell(pawn, targetCell, verb))
            {
                return;
            }

            int enemyHits = CountRadialEnemyHits(cell, impactRadius, validTargets);
            if (enemyHits <= 0)
            {
                return;
            }

            int friendlyHits = CountRadialFriendlyHits(pawn, cell, impactRadius);
            float score = TargetScore(pawn, cell, enemyHits, friendlyHits);
            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestCell = cell;
            }
        }

        private static int CountStarfallEnemyHits(Pawn pawn, IntVec3 targetCell, StarfallImpactProfile profile, List<Pawn> validTargets)
        {
            int count = 0;
            for (int i = 0; i < validTargets.Count; i++)
            {
                if (StarfallWouldHit(pawn, targetCell, validTargets[i].Position, profile))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStarfallFriendlyHits(Pawn pawn, IntVec3 targetCell, StarfallImpactProfile profile)
        {
            int count = 0;
            IReadOnlyList<Pawn> spawnedPawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn target = spawnedPawns[i];
                if (IsFriendlyFireRisk(pawn, target) && StarfallWouldHit(pawn, targetCell, target.Position, profile))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool StarfallWouldHit(Pawn pawn, IntVec3 targetCell, IntVec3 pawnCell, StarfallImpactProfile profile)
        {
            Vector3 center = targetCell.ToVector3Shifted();
            Vector3 pawnPosition = pawnCell.ToVector3Shifted();
            if (InRadius(center, pawnPosition, profile.ExplosionRadius))
            {
                return true;
            }

            Vector3 direction = center - pawn.Position.ToVector3Shifted();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = new Vector3(1f, 0f, 0f);
            }
            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            return InRadius(center + side * profile.SplitSpreadDistance, pawnPosition, profile.ExplosionRadius)
                || InRadius(center - side * profile.SplitSpreadDistance, pawnPosition, profile.ExplosionRadius);
        }

        private static int CountRadialEnemyHits(IntVec3 targetCell, float radius, List<Pawn> validTargets)
        {
            int count = 0;
            Vector3 center = targetCell.ToVector3Shifted();
            for (int i = 0; i < validTargets.Count; i++)
            {
                if (InRadius(center, validTargets[i].Position.ToVector3Shifted(), radius))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRadialFriendlyHits(Pawn pawn, IntVec3 targetCell, float radius)
        {
            int count = 0;
            Vector3 center = targetCell.ToVector3Shifted();
            IReadOnlyList<Pawn> spawnedPawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn target = spawnedPawns[i];
                if (IsFriendlyFireRisk(pawn, target) && InRadius(center, target.Position.ToVector3Shifted(), radius))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsFriendlyFireRisk(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || target == pawn || target.Dead || !target.Spawned || target.Map != pawn.Map)
            {
                return false;
            }

            return !target.HostileTo(pawn);
        }

        private static float TargetScore(Pawn pawn, IntVec3 targetCell, int enemyHits, int friendlyHits)
        {
            float distance = Mathf.Sqrt((pawn.Position - targetCell).LengthHorizontalSquared);
            return enemyHits * TargetEnemyHitScore + distance * TargetDistanceScore - friendlyHits * TargetFriendlyHitPenalty;
        }

        private static bool InRadius(Vector3 center, Vector3 target, float radius)
        {
            Vector3 offset = target - center;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private static IntVec3 MidpointCell(IntVec3 first, IntVec3 second)
        {
            return new IntVec3(Mathf.RoundToInt((first.x + second.x) * 0.5f), 0, Mathf.RoundToInt((first.z + second.z) * 0.5f));
        }

        private static float ProjectileExplosionRadius(Verb verb)
        {
            float radius = verb?.verbProps?.defaultProjectile?.projectile?.explosionRadius ?? 0f;
            return radius > 0f ? radius : 0.5f;
        }

        private struct StarfallImpactProfile
        {
            public float ExplosionRadius;
            public float SplitSpreadDistance;
            public float PairCandidateDistance;

            public static StarfallImpactProfile For(Ability ability)
            {
                CompAbilityEffect_Starfall starfall = ability?.CompOfType<CompAbilityEffect_Starfall>();
                ThingDef carrier = starfall?.Props?.carrierProjectileDef;
                DefModExtension_StarfallCarrier modExt = carrier?.GetModExtension<DefModExtension_StarfallCarrier>();
                float explosionRadius = modExt?.splitProjectileDef?.projectile?.explosionRadius ?? 0f;
                if (explosionRadius <= 0f)
                {
                    explosionRadius = 2.85f;
                }

                float splitSpreadDistance = modExt?.splitSpreadDistance ?? 2.8f;
                float spreadJitter = modExt?.splitSpreadJitter ?? 0f;
                float forwardJitter = modExt?.splitForwardJitter ?? 0f;
                return new StarfallImpactProfile
                {
                    ExplosionRadius = explosionRadius,
                    SplitSpreadDistance = splitSpreadDistance,
                    PairCandidateDistance = (explosionRadius + splitSpreadDistance + spreadJitter + forwardJitter) * 2f
                };
            }
        }
    }

    public class JobDriver_RavagerArtilleryAttack : JobDriver
    {
        private int numAttacksMade;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref numAttacksMade, nameof(numAttacksMade), 0);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            Toil attack = ToilMaker.MakeToil(nameof(JobDriver_RavagerArtilleryAttack));
            attack.initAction = delegate
            {
                pawn.pather?.StopDead();
            };
            attack.tickIntervalAction = delegate(int delta)
            {
                if (!job.targetA.IsValid)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (numAttacksMade >= 1 && !pawn.stances.FullBodyBusy)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (pawn.stances.FullBodyBusy)
                {
                    return;
                }

                Verb verb = job.verbToUse ?? pawn.TryGetAttackVerb(null, !pawn.IsColonist && !pawn.IsColonySubhuman);
                LocalTargetInfo targetCell = RavagerArtilleryUtility.TargetCell(job.targetA);
                if (numAttacksMade == 0 && !job.playerForced && !TryRefreshAutonomousTargetCell(verb, out targetCell))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!RavagerArtilleryUtility.CanFireAtCell(pawn, targetCell, verb))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (verb.TryStartCastOn(targetCell))
                {
                    numAttacksMade++;
                }
                else if (!pawn.stances.FullBodyBusy)
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            attack.defaultCompleteMode = ToilCompleteMode.Never;
            attack.activeSkill = () => Toils_Combat.GetActiveSkillForToil(attack);
            yield return attack;
        }

        private bool TryRefreshAutonomousTargetCell(Verb verb, out LocalTargetInfo targetCell)
        {
            targetCell = LocalTargetInfo.Invalid;
            if (verb == null)
            {
                return false;
            }

            if (!RavagerArtilleryUtility.TryFindBestArtilleryTarget(pawn, verb, verb.EffectiveRange, out targetCell))
            {
                return false;
            }

            job.targetA = targetCell;
            return true;
        }
    }

    public class Verb_RavagerArtillery : Verb_Shoot
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (CasterIsPawn && RavagerArtilleryUtility.IsPlayerControlled(CasterPawn) && !RavagerArtilleryUtility.AutoFireEnabled(CasterPawn) && !RavagerArtilleryUtility.IsManualArtilleryJob(CasterPawn))
            {
                return false;
            }

            LocalTargetInfo targetCell = RavagerArtilleryUtility.TargetCell(castTarg);
            if (CasterIsPawn && !RavagerArtilleryUtility.CanFireAtCell(CasterPawn, targetCell, this))
            {
                return false;
            }

            LocalTargetInfo destinationCell = destTarg.IsValid ? RavagerArtilleryUtility.TargetCell(destTarg) : targetCell;
            return base.TryStartCastOn(targetCell, destinationCell, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            if (!CasterIsPawn)
            {
                base.OrderForceTarget(target);
                return;
            }

            LocalTargetInfo targetCell = RavagerArtilleryUtility.TargetCell(target);
            float minRange = verbProps.EffectiveMinRange(targetCell, CasterPawn);
            if ((float)CasterPawn.Position.DistanceToSquared(targetCell.Cell) < minRange * minRange && CasterPawn.Position.AdjacentTo8WayOrInside(targetCell.Cell))
            {
                Messages.Message("MessageCantShootInMelee".Translate(), CasterPawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!RavagerArtilleryUtility.CanFireAtCell(CasterPawn, targetCell, this))
            {
                Messages.Message("CannotHitTarget".Translate(), CasterPawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Job job = RavagerArtilleryUtility.MakeArtilleryAttackJob(targetCell, this);
            job.playerForced = true;
            job.endIfCantShootInMelee = true;
            CasterPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    public class CompProperties_RavagerArtilleryController : CompProperties
    {
        public bool autoFireEnabledDefault = true;
        public string autoFireGizmoIconPath = "UI/Ravager/AutoArtillery";

        public CompProperties_RavagerArtilleryController()
        {
            compClass = typeof(CompRavagerArtilleryController);
        }
    }

    public class CompRavagerArtilleryController : ThingComp
    {
        private bool autoFireEnabled;
        private bool initialized;

        public bool AutoFireEnabled
        {
            get
            {
                EnsureInitialized();
                return autoFireEnabled;
            }
        }

        private CompProperties_RavagerArtilleryController Props => (CompProperties_RavagerArtilleryController)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureInitialized();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoFireEnabled, nameof(autoFireEnabled), false);
            Scribe_Values.Look(ref initialized, nameof(initialized), false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = parent as Pawn;
            if (pawn == null || !RavagerArtilleryUtility.IsPlayerControlled(pawn))
            {
                yield break;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "APM_Ravager_AutoArtillery_Label".Translate(),
                defaultDesc = "APM_Ravager_AutoArtillery_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.autoFireGizmoIconPath),
                isActive = () => AutoFireEnabled,
                toggleAction = delegate
                {
                    EnsureInitialized();
                    autoFireEnabled = !autoFireEnabled;
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !RavagerArtilleryUtility.IsPlayerControlled(pawn) || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }

            if (!AutoFireEnabled)
            {
                return "APM_Ravager_Inspect_AutoArtilleryDisabled".Translate();
            }

            if (pawn.Position.Roofed(pawn.Map))
            {
                return "APM_Ravager_Inspect_AutoArtilleryBlockedRoof".Translate();
            }

            Thing target = pawn.mindState?.enemyTarget;
            if (target == null || target.Destroyed || !target.Spawned || target.Map != pawn.Map)
            {
                return null;
            }

            RoofDef targetRoof = target.Position.GetRoof(pawn.Map);
            if (targetRoof != null && targetRoof.isThickRoof)
            {
                return "APM_Ravager_Inspect_AutoArtilleryBlockedTargetRoof".Translate();
            }

            Verb verb = pawn.TryGetAttackVerb(target, !pawn.IsColonist && !pawn.IsColonySubhuman);
            if (!RavagerArtilleryUtility.CanFireAtCell(pawn, new LocalTargetInfo(target.Position), verb))
            {
                return "APM_Ravager_Inspect_AutoArtilleryCannotHit".Translate();
            }

            return null;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            autoFireEnabled = Props.autoFireEnabledDefault;
            initialized = true;
        }
    }
}
