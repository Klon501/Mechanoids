using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobGiver_AIGazerLaserFight : JobGiver_AIFightEnemies
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return null;
            }

            return base.TryGiveJob(pawn);
        }

        public override Thing FindAttackTarget(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return null;
            }

            return base.FindAttackTarget(pawn);
        }

        public override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            return CanRunFor(pawn) && base.ExtraTargetValidator(pawn, target);
        }

        public override bool ShouldLoseTarget(Pawn pawn)
        {
            if (!CanRunFor(pawn))
            {
                return true;
            }

            return base.ShouldLoseTarget(pawn);
        }

        private static bool CanRunFor(Pawn pawn)
        {
            return GazerLaserUtility.CanUseLaser(pawn)
                && GazerLaserUtility.AutoLaserEnabled(pawn);
        }
    }
}
