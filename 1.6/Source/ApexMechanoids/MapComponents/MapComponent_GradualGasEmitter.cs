using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class GradualGasEmission : IExposable
    {
        public IntVec3 center;
        public GasType gasType;
        public float gasPerInterval;
        public int tickInterval;
        public int ticksRemaining;
        private int ticksSinceLastEmit;

        public GradualGasEmission() { }

        public GradualGasEmission(IntVec3 center, GasType gasType, float totalGasAmount, int totalTicks, int tickInterval)
        {
            this.center = center;
            this.gasType = gasType;
            this.tickInterval = tickInterval;
            this.ticksRemaining = totalTicks;
            int numIntervals = totalTicks / tickInterval;
            if (numIntervals < 1) numIntervals = 1;
            this.gasPerInterval = totalGasAmount / numIntervals;
            this.ticksSinceLastEmit = tickInterval;
        }

        public bool Tick(Map map)
        {
            if (ticksRemaining <= 0) return false;
            ticksSinceLastEmit++;
            ticksRemaining--;
            if (ticksSinceLastEmit >= tickInterval)
            {
                ticksSinceLastEmit = 0;
                if (center.InBounds(map))
                {
                    GasUtility.AddGas(center, map, gasType, gasPerInterval);
                }
            }
            return ticksRemaining > 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref gasType, "gasType");
            Scribe_Values.Look(ref gasPerInterval, "gasPerInterval");
            Scribe_Values.Look(ref tickInterval, "tickInterval");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining");
            Scribe_Values.Look(ref ticksSinceLastEmit, "ticksSinceLastEmit");
        }
    }

    public class MapComponent_GradualGasEmitter : MapComponent
    {
        private List<GradualGasEmission> emissions = new List<GradualGasEmission>();

        public MapComponent_GradualGasEmitter(Map map) : base(map) { }

        public void AddEmission(GradualGasEmission emission)
        {
            emissions.Add(emission);
        }

        public override void MapComponentTick()
        {
            for (int i = emissions.Count - 1; i >= 0; i--)
            {
                if (!emissions[i].Tick(map))
                {
                    emissions.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref emissions, "emissions", LookMode.Deep);
            if (emissions == null)
            {
                emissions = new List<GradualGasEmission>();
            }
        }
    }
}
