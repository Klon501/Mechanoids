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
                var graphics = graphicData.Graphic;
                if (Props.usePlayerMechsColor && parent.Faction == Faction.OfPlayer)
                {
                    graphics = graphics.GetColoredVersion(graphics.Shader, Find.FactionManager.OfPlayer.AllegianceColor, graphics.ColorTwo);
                }
                graphics.Draw(this.parent.DrawPos, this.parent.Rotation, this.parent, rotation + Props.extraRotation);
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
        public bool usePlayerMechsColor = false;
    }
}
