using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_HazeToxicMistController : CompProperties
    {
        public AbilityDef abilityDef;
        public int checkIntervalTicks = 300;
        public bool autoCastForPlayer = false;
        public float threatRadius = 4.9f;
        public int minHostilesToTrigger = 1;
        public bool requireFleshThreat = true;
        public bool blockIfAlliesInRadius = true;

        public CompProperties_HazeToxicMistController()
        {
            compClass = typeof(CompHazeToxicMistController);
        }
    }

    public class CompHazeToxicMistController : ThingComp
    {
        public CompProperties_HazeToxicMistController Props => (CompProperties_HazeToxicMistController)props;

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

            int interval = Props.checkIntervalTicks > 0 ? Props.checkIntervalTicks : 300;
            if (!pawn.IsHashIntervalTick(interval))
            {
                return;
            }

            EnsureAbility();

            if (!ShouldAutoCast(pawn))
            {
                return;
            }

            Ability ability = pawn.abilities?.GetAbility(Props.abilityDef);
            if (ability == null || !ability.CanCast)
            {
                return;
            }

            if (pawn.CurJobDef == ability.def.jobDef)
            {
                return;
            }

            LocalTargetInfo selfTarget = new LocalTargetInfo(pawn);
            if (!ability.CanApplyOn(selfTarget))
            {
                return;
            }

            ability.QueueCastingJob(selfTarget, selfTarget);
        }

        private bool ShouldAutoCast(Pawn pawn)
        {
            return ToxicMistAIUtility.CanAutoCast(
                pawn,
                Props.threatRadius,
                Props.minHostilesToTrigger,
                Props.requireFleshThreat,
                Props.blockIfAlliesInRadius);
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
}
