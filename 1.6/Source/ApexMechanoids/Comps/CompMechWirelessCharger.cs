using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    public class CompMechWirelessCharger : ThingComp
    {
        public CompRefuelable refuelableComp;
        private CompWasteProducer wasteProducer;
        private CompThingContainer container;

        private float wasteProduced;

        public CompProperties_MechWirelessCharger Props => (CompProperties_MechWirelessCharger)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelableComp = parent.GetComp<CompRefuelable>();
            wasteProducer = parent.GetComp<CompWasteProducer>();
            container = parent.GetComp<CompThingContainer>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % 600 == 0 && parent.Spawned && (refuelableComp?.HasFuel ?? false) && !(container?.Full ?? false))
            {
                ChargeMechs();
            }
        }

        public void ChargeMechs()
        {
            List<Pawn> mechs = parent.Map.mapPawns.PawnsInFaction(parent.Faction).Where((Pawn p) => p.Spawned && p.RaceProps.IsMechanoid && !p.Dead && ((p.needs?.energy?.CurLevel ?? float.PositiveInfinity) < (p.needs?.energy?.MaxLevel ?? float.NegativeInfinity)) && p.PositionHeld.DistanceTo(parent.Position) <= Props.radius).OrderBy((Pawn p) => p.needs.energy.CurLevel).ToList();
            int mechsCharged = 0;
            foreach (Pawn mech in mechs)
            {
                Need_MechEnergy energy = mech.needs.energy;
                if (energy.CurLevel < energy.MaxLevel)
                {
                    float energyToAdd = Mathf.Min(energy.MaxLevel - energy.CurLevel, Mathf.Min(refuelableComp.Fuel, Props.fuelPerCharge) / Props.fuelPerCharge * Props.powerPerCharge);
                    energy.CurLevel += energyToAdd;
                    float fuelToConsume = (energyToAdd / Props.powerPerCharge) * Props.fuelPerCharge;
                    refuelableComp.ConsumeFuel(fuelToConsume);
                    wasteProduced += mech.GetStatValue(StatDefOf.WastepacksPerRecharge) * (energyToAdd / mech.needs.energy.MaxLevel);
                    if (wasteProduced >= 1)
                    {
                        GenerateWastePack();
                    }
                    mechsCharged++;
                    if (mechsCharged >= Props.maxMechPerCharge)
                    {
                        break;
                    }
                }
            }
        }

        public void GenerateWastePack()
        {
            if (wasteProducer != null)
            {
                wasteProducer.ProduceWaste(Mathf.RoundToInt(wasteProduced));
                wasteProduced = 0f;
                if (parent.Map != null)
                {
                    EffecterDefOf.MechChargerWasteProduced.Spawn(parent.Position, parent.Map).Cleanup();
                }
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            GenerateWastePack();
            base.PostDestroy(mode, previousMap);
        }
    }
}
