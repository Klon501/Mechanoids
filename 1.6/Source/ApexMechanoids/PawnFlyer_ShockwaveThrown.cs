using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class PawnFlyer_ShockwaveThrown : PawnFlyer
    {
        private int stunTicksOnLanding;
        private Thing stunInstigator;

        public void Initialize(int stunTicks, Thing instigator)
        {
            stunTicksOnLanding = stunTicks;
            stunInstigator = instigator;
        }

        public override void RespawnPawn()
        {
            Pawn pawn = FlyingPawn;
            base.RespawnPawn();

            if (pawn != null && pawn.Spawned && !pawn.Dead && stunTicksOnLanding > 0 && pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(stunTicksOnLanding, stunInstigator, addBattleLog: false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stunTicksOnLanding, nameof(stunTicksOnLanding));
            Scribe_References.Look(ref stunInstigator, nameof(stunInstigator));
        }
    }
}
