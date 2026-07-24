using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class CompPlayerFactionColoredAdditionalGraphics : CompDrawAdditionalGraphics
    {
        private new CompProperties_PlayerFactionColoredAdditionalGraphics Props
        {
            get
            {
                return (CompProperties_PlayerFactionColoredAdditionalGraphics)this.props;
            }
        }
        public override void PostDraw()
        {
            foreach (GraphicData graphicData in this.Props.graphics)
            {
                var graphics = graphicData.Graphic;
                if (parent.Faction == Faction.OfPlayer)
                {
                    graphics = graphics.GetColoredVersion(graphics.Shader, Find.FactionManager.OfPlayer.AllegianceColor, graphics.ColorTwo);
                }
                graphics.Draw(this.parent.DrawPos, this.parent.Rotation, this.parent);
            }
        }
    }
    public class CompProperties_PlayerFactionColoredAdditionalGraphics : CompProperties_DrawAdditionalGraphics
    {
        public CompProperties_PlayerFactionColoredAdditionalGraphics() : base()
        {
            this.compClass = typeof(CompPlayerFactionColoredAdditionalGraphics);
        }
    }
}
