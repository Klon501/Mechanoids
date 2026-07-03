using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;

namespace ApexMechanoids
{
    public class JobDriver_Absorb : JobDriver_CastAbilityMelee
    {
        public override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction((c) => pawn.drawer.renderer.SetAnimation(null));
            foreach (var toil in base.MakeNewToils())
            {
                if (toil.debugName == "CastVerb")
                {
                    toil.initAction += () => pawn.drawer.renderer.SetAnimation(ApexDefsOf.APM_EatingIngestor);
                }
                yield return toil;
            }
        }
    }
}
