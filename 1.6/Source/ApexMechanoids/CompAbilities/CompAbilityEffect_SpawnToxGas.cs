using RimWorld;
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
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.radius, true))
            {
                if (!cell.InBounds(map)) continue;
                emitter.AddEmission(new GradualGasEmission(
                    cell,
                    GasType.ToxGas,
                    Props.gasAmount,
                    Props.spreadTicks,
                    Props.emitIntervalTicks
                ));
            }
        }
    }
}
