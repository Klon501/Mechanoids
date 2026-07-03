using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class PawnRenderNode_Animated : PawnRenderNode
    {
        public PawnRenderNode_Animated(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }
        public new PawnRenderNodeProperties_Animated Props => (PawnRenderNodeProperties_Animated)props;
        public override Graphic PrimaryGraphic => Graphics[CurrentIndex];
        private int currentGraphicInitialTick = -999999;
        private int currentIndex = -1;
        protected int CurrentIndex
        {
            get
            {
                var tick = Find.TickManager.TicksGame;
                if (currentIndex < 0)
                {
                    currentGraphicInitialTick = tick;
                    return currentIndex = GetNextIndex();
                }
                var animationLength = Props.ticksPerTexPaths[currentIndex];
                if (Math.Abs(tick - currentGraphicInitialTick) > animationLength)
                {
                    currentGraphicInitialTick = tick;
                    return currentIndex = GetNextIndex();
                }
                return currentIndex;

            }
        }
        public virtual int GetNextIndex() => (currentIndex + 1) % Graphics.Count;
        public override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (HasGraphic(pawn))
            {
                foreach(string texPath in Props.texPaths)
                {
                    if (texPath.NullOrEmpty())
                    {
                        continue;
                    }
                    Shader shader = this.ShaderFor(pawn);
                    if (shader == null)
                    {
                        continue;
                    }
                    yield return GraphicDatabase.Get<Graphic_Multi>(texPath, shader, Vector2.one, this.ColorFor(pawn));
                }
            }
        }
    }
    public class PawnRenderNodeProperties_Animated : PawnRenderNodeProperties
    {
        public int ticksPerTexture = 60;
        public List<int> ticksPerTexPaths;
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (ticksPerTexPaths == null)
            {
                ticksPerTexPaths = new List<int>();
            }
            for (int i = ticksPerTexPaths.Count; i < texPaths.Count; i++)
            {
                ticksPerTexPaths.Add(ticksPerTexture);
            }
        }
    }
}
