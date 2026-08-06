using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilitySpawnToxGas : CompProperties_AbilityEffect
    {
        public float radius = 5f;
        public float gasAmount = 1f;
        // Total ticks over which to spread gas emission (default: 300 ticks = 5s)
        public int spreadTicks = 300;
        // How often (in ticks) to emit a batch of gas
        public int emitIntervalTicks = 5;
        public int cellsToPollute = 0;
        public float pollutionRadius = 0f;
        public EffecterDef pollutionEffecterDef;

        public CompProperties_AbilitySpawnToxGas()
        {
            compClass = typeof(CompAbilityEffect_SpawnToxGas);
        }
    }

    public class CompAbilityEffect_SpawnToxGas : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnToxGas Props => (CompProperties_AbilitySpawnToxGas)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }
            IntVec3 center = pawn.Position;
            if (!center.InBounds(map))
            {
                return;
            }
            MapComponent_GradualGasEmitter emitter = map.GetComponent<MapComponent_GradualGasEmitter>();
            if (emitter == null)
            {
                return;
            }
			
            emitter.AddEmission(new GradualGasEmission(
                center,
                GasType.ToxGas,
                Props.gasAmount,
                Props.spreadTicks,
                Props.emitIntervalTicks
            ));

            PolluteNearbyCells(center, map);
        }

        private void PolluteNearbyCells(IntVec3 center, Map map)
        {
            if (Props.cellsToPollute <= 0 || Props.pollutionRadius <= 0f || map.pollutionGrid == null)
            {
                return;
            }

            List<IntVec3> candidates = new List<IntVec3>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.pollutionRadius, useCenter: true))
            {
                if (!cell.InBounds(map) || cell.IsPolluted(map))
                {
                    continue;
                }

                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain != null && !terrain.canBePolluted)
                {
                    continue;
                }

                candidates.Add(cell);
            }

            int cellsToPollute = Math.Min(Props.cellsToPollute, candidates.Count);
            for (int i = 0; i < cellsToPollute; i++)
            {
                int index = Rand.Range(0, candidates.Count);
                IntVec3 cell = candidates[index];
                candidates.RemoveAt(index);

                map.pollutionGrid.SetPolluted(cell, true);
                Props.pollutionEffecterDef?.Spawn(cell, map).Cleanup();
            }
        }
    }
}
