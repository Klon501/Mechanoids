using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class CompProperties_GazerShockwaveController : CompProperties
    {
        public AbilityDef shockwaveAbilityDef;
        public int checkIntervalTicks = 30;
        public float meleeThreatRadius = 4f;
        public int minHostilesToTrigger = 1;

        public CompProperties_GazerShockwaveController()
        {
            compClass = typeof(CompGazerShockwaveController);
        }
    }

    public class CompGazerShockwaveController : ThingComp
    {
        public CompProperties_GazerShockwaveController Props => (CompProperties_GazerShockwaveController)props;

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

            if (!pawn.IsHashIntervalTick(Props.checkIntervalTicks > 0 ? Props.checkIntervalTicks : 30))
            {
                return;
            }

            EnsureAbility();

            if (pawn.Faction == Faction.OfPlayer)
            {
                return;
            }

            if (pawn.CurJob?.playerForced == true)
            {
                return;
            }

            if (!ShouldAutoCast(pawn))
            {
                return;
            }

            Ability ability = pawn.abilities?.GetAbility(Props.shockwaveAbilityDef);
            if (ability == null || !ability.CanCast)
            {
                return;
            }

            if (pawn.CurJob?.ability == ability)
            {
                return;
            }

            LocalTargetInfo selfTarget = pawn;
            if (!ability.AICanTargetNow(selfTarget) || !ability.CanApplyOn(selfTarget))
            {
                return;
            }

            Job job = ability.GetJob(selfTarget, selfTarget);
            if (job == null)
            {
                return;
            }

            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, cancelBusyStances: true);
        }

        private void EnsureAbility()
        {
            Pawn pawn = Pawn;
            if (pawn?.abilities == null || Props.shockwaveAbilityDef == null)
            {
                return;
            }

            if (pawn.abilities.GetAbility(Props.shockwaveAbilityDef) == null)
            {
                pawn.abilities.GainAbility(Props.shockwaveAbilityDef);
            }
        }

        private bool ShouldAutoCast(Pawn pawn)
        {
            return ShockwaveAIUtility.HasRequiredHostilesNear(pawn, Props.meleeThreatRadius, Props.minHostilesToTrigger);
        }
    }
}
