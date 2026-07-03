using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilitySpawnSmokescreen : CompProperties_AbilityEffect
    {
        public ThingDef gasDef;
        public float radius = 5f;
        public float maxRadius = 15f;
        public int spreadIntervalTicks = 15;

        public CompProperties_AbilitySpawnSmokescreen()
        {
            compClass = typeof(CompAbilityEffect_SpawnSmokescreen);
        }
    }

    public class CompAbilityEffect_SpawnSmokescreen : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnSmokescreen Props => (CompProperties_AbilitySpawnSmokescreen)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            Map map = pawn.Map;
            if (map == null || Props.gasDef == null)
            {
                return;
            }
            GasSpreadManager.StartSpread(pawn.Position, map, Props.gasDef, Props.maxRadius, Props.spreadIntervalTicks);
        }
    }

    public static class GasSpreadManager
    {
        private class SpreadJob : MapComponent
        {
            private ThingDef gasDef;
            private IntVec3 center;
            private float maxRadius;
            private int spreadIntervalTicks;
            private float currentRadius;
            private int ticksUntilNextSpread;

            public SpreadJob(Map map) : base(map) { }

            public void Init(IntVec3 center, ThingDef gasDef, float maxRadius, int spreadIntervalTicks)
            {
                this.center = center;
                this.gasDef = gasDef;
                this.maxRadius = maxRadius;
                this.spreadIntervalTicks = spreadIntervalTicks;
                this.currentRadius = 0f;
                this.ticksUntilNextSpread = 0;
            }

            public override void MapComponentTick()
            {
                if (gasDef == null)
                {
                    return;
                }
                if (currentRadius > maxRadius)
                {
                    gasDef = null;
                    return;
                }
                ticksUntilNextSpread--;
                if (ticksUntilNextSpread > 0)
                {
                    return;
                }
                ticksUntilNextSpread = spreadIntervalTicks;
                SpawnRing(currentRadius);
                currentRadius += 1f;
            }

            private void SpawnRing(float radius)
            {
                float outerSq = (radius + 1f) * (radius + 1f);
                float innerSq = radius * radius;
                IntVec3[] cells = GenRadial.RadialPattern;
                for (int i = 0; i < cells.Length; i++)
                {
                    IntVec3 offset = cells[i];
                    float distSq = offset.x * offset.x + offset.z * offset.z;
                    if (distSq < innerSq || distSq >= outerSq)
                    {
                        continue;
                    }
                    IntVec3 cell = center + offset;
                    if (!cell.InBounds(map) || !cell.Walkable(map))
                    {
                        continue;
                    }
                    if (cell.GetGas(map) == null)
                    {
                        GenSpawn.Spawn(gasDef, cell, map);
                    }
                }
            }
        }

        public static void StartSpread(IntVec3 center, Map map, ThingDef gasDef, float maxRadius, int spreadIntervalTicks)
        {
            SpreadJob job = new SpreadJob(map);
            job.Init(center, gasDef, maxRadius, spreadIntervalTicks);
            map.components.Add(job);
        }
    }
}
