using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNode_RandomAnimated : PawnRenderNode_Animated
    {
        public PawnRenderNode_RandomAnimated(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }
        public override int GetNextIndex() => Rand.Range(0, Graphics.Count);
    }
}
