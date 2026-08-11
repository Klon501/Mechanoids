using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public static class GazerLaserUtility
    {
        private const string SunRayDefName = "APM_SunRay";

        public static bool CanUseLaser(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed && pawn.Spawned && pawn.Map != null && pawn.Awake();
        }

        public static bool AutoLaserEnabled(Pawn pawn)
        {
            if (!IsPlayerControlled(pawn))
            {
                return true;
            }

            return pawn.TryGetComp<CompGazerLaserController>()?.AutoLaserEnabled ?? false;
        }

        public static bool AutoWeaponFireBlocked(Pawn pawn)
        {
            return IsPlayerControlled(pawn) && !AutoLaserEnabled(pawn);
        }

        public static bool AutoAbilityBlockedByLaserToggle(Pawn pawn, Ability ability)
        {
            return ability?.def?.defName == SunRayDefName && AutoAbilityBlockedByLaserToggle(pawn);
        }

        public static bool AutoAbilityBlockedByLaserToggle(Pawn pawn)
        {
            CompGazerLaserController controller = pawn?.TryGetComp<CompGazerLaserController>();
            return IsPlayerControlled(pawn) && controller != null && !controller.AutoLaserEnabled;
        }

        public static bool IsManualLaserJob(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job != null && job.def == JobDefOf.AttackStatic && job.playerForced;
        }

        public static bool IsManualSunRayJob(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            return job != null && job.playerForced && (job.ability?.def?.defName == SunRayDefName || job.verbToUse is Verb_ShootSunBeamAbility);
        }

        public static bool IsPlayerControlled(Pawn pawn)
        {
            return pawn?.Faction == Faction.OfPlayer;
        }
    }

    public class CompProperties_GazerLaserController : CompProperties
    {
        public bool autoLaserEnabledDefault = true;
        public string autoLaserGizmoIconPath = "UI/Gazer/AutoLaser";

        public CompProperties_GazerLaserController()
        {
            compClass = typeof(CompGazerLaserController);
        }
    }

    public class CompGazerLaserController : ThingComp
    {
        private bool autoLaserEnabled;
        private bool initialized;

        public bool AutoLaserEnabled
        {
            get
            {
                EnsureInitialized();
                return autoLaserEnabled;
            }
        }

        private CompProperties_GazerLaserController Props => (CompProperties_GazerLaserController)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureInitialized();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoLaserEnabled, nameof(autoLaserEnabled), false);
            Scribe_Values.Look(ref initialized, nameof(initialized), false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = parent as Pawn;
            if (pawn == null || !GazerLaserUtility.IsPlayerControlled(pawn))
            {
                yield break;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "APM_Gazer_AutoLaser_Label".Translate(),
                defaultDesc = "APM_Gazer_AutoLaser_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.autoLaserGizmoIconPath),
                isActive = () => AutoLaserEnabled,
                toggleAction = delegate
                {
                    EnsureInitialized();
                    autoLaserEnabled = !autoLaserEnabled;
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !GazerLaserUtility.IsPlayerControlled(pawn) || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }

            if (!AutoLaserEnabled)
            {
                return "APM_Gazer_Inspect_AutoLaserDisabled".Translate();
            }

            return null;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            autoLaserEnabled = Props.autoLaserEnabledDefault;
            initialized = true;
        }
    }
}
