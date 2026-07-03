using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_TrueSight : CompProperties
    {
        public float radius = 12f;
        public int interval = 60;

        public CompProperties_TrueSight() => compClass = typeof(CompTrueSight);
    }

    public class CompTrueSight : ThingComp
    {
        public CompProperties_TrueSight Props => (CompProperties_TrueSight)props;

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (!parent.Spawned) return;
            if (!parent.IsHashIntervalTick(Props.interval, delta)) return;

            foreach (Thing t in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
            {
                if (t is Pawn pawn && pawn != parent && pawn.HostileTo(parent))
                {
                    HediffComp_Invisibility invisComp = pawn.GetInvisibilityComp();
                    if (invisComp != null)
                        invisComp.DisruptInvisibility();
                }
            }
        }
    }
}
