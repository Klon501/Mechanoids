using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompDrawRotatedAdditionalGraphics : CompDrawAdditionalGraphics
    {
        private new CompProperties_DrawRotatedAdditionalGraphics Props
        {
            get
            {
                return (CompProperties_DrawRotatedAdditionalGraphics)this.props;
            }
        }
        public override void PostDraw()
        {
            foreach (GraphicData graphicData in this.Props.graphics)
            {
                graphicData.Graphic.Draw(this.parent.DrawPos, this.parent.Rotation, this.parent, rotation + Props.extraRotation);
            }
        }
        public float rotation = 0f;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref rotation, nameof(rotation));
        }
    }
    public class CompProperties_DrawRotatedAdditionalGraphics : CompProperties_DrawAdditionalGraphics
    {
        public CompProperties_DrawRotatedAdditionalGraphics() : base()
        {
            this.compClass = typeof(CompDrawRotatedAdditionalGraphics);
        }
        public float extraRotation = 90f;
    }
}
