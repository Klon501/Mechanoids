using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class Verb_GazerEmplacementBeam : Verb
    {
        private Building_GazerEmplacement Emplacement
        {
            get { return verbTracker != null ? verbTracker.directOwner as Building_GazerEmplacement : null; }
        }

        public override bool Available()
        {
            string failReason = null;
            return Emplacement != null && Emplacement.CanUseVerb(out failReason);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            string failReason = null;
            return Emplacement != null && Emplacement.CanAttackTargetForVerb(targ, out failReason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            string failReason = null;
            if (Emplacement == null || !Emplacement.CanAttackTargetForVerb(target, out failReason))
            {
                if (showMessages && Emplacement != null && !failReason.NullOrEmpty())
                {
                    Messages.Message(failReason, Emplacement, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return true;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            Emplacement?.DrawVerbTargetingPreview(target);
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (Emplacement == null)
            {
                return false;
            }

            string failReason = null;
            return Emplacement.TryOrderShot(castTarg, out failReason);
        }

        public override bool TryCastShot()
        {
            return false;
        }
    }
}
