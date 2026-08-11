using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class SeveringEdge_Mote : MoteThrown, VEF.Graphics.IAnimationOneTime
    {
        public int CurrentIndex()
        {
            var data = (VEF.Graphics.GraphicData_Animated)this.def.graphicData;
            var graphics = (VEF.Graphics.Graphic_Animated)this.def.graphicData.Graphic;
            var currentTick = Find.TickManager.TicksGame;
            return Mathf.Clamp((currentTick - this.spawnedTick) / data.ticksPerFrame, 0, graphics.SubGraphicCount);
        }
    }
}
