using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class RavagerArtilleryUtility
    {
        public static bool CanUseArtillery(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed && pawn.Spawned && pawn.Map != null && !pawn.Position.Roofed(pawn.Map);
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

        public static bool IsPlayerControlled(Pawn pawn)
        {
            return pawn?.Faction == Faction.OfPlayer;
        }

        public static LocalTargetInfo TargetCell(LocalTargetInfo target)
        {
            return target.IsValid ? new LocalTargetInfo(target.Cell) : LocalTargetInfo.Invalid;
        }

        public static Job MakeArtilleryAttackJob(LocalTargetInfo targetCell, Verb verb)
        {
            Job job = JobMaker.MakeJob(ApexDefsOf.APM_RavagerArtilleryAttack, TargetCell(targetCell));
            job.verbToUse = verb;
            job.maxNumStaticAttacks = 1;
            job.endIfCantShootTargetFromCurPos = true;
            job.expiryInterval = JobGiver_AIFightEnemy.ExpiryInterval_ShooterSucceeded.RandomInRange;
            job.checkOverrideOnExpire = true;
            return job;
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

        public static Pawn FindBestPawnTarget(Pawn pawn, Verb verb, float maxRange)
        {
            if (!CanUseArtillery(pawn) || verb == null || verb.verbProps.IsMeleeAttack)
            {
                return null;
            }

            float maxRangeSquared = maxRange * maxRange;
            Pawn bestTarget = null;
            float bestDistanceSquared = float.MaxValue;
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

                if (distanceSquared < bestDistanceSquared)
                {
                    bestTarget = target;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return bestTarget;
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
