using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompToxicPurifier : ThingComp
    {
        public CompProperties_ToxicPurifier Props => (CompProperties_ToxicPurifier)props;

        public GameCondition_ToxicPurifier conditionCached;
        public GameCondition_ToxicPurifier gameCondition
        {
            get
            {
                if (conditionCached == null)
                {
                    conditionCached = parent.Map.gameConditionManager.GetActiveCondition(Props.conditionDef) as GameCondition_ToxicPurifier;
                }
                if (conditionCached == null)
                {
                    conditionCached = (GameCondition_ToxicPurifier)GameConditionMaker.MakeCondition(Props.conditionDef);
                    parent.Map.GameConditionManager.RegisterCondition(conditionCached);
                    conditionCached.Permanent = true;
                }
                return conditionCached;
            }
        }

        public float storedToxicity = 0f;

        public CompPowerTrader compPower;

        public bool shouldSprayToxic = false;

        public int sprayTickLeft = -1;
        public bool Active
        {
            get
            {
                if (!parent.Spawned)
                {
                    return false;
                }
                if (compPower != null && !compPower.PowerOn)
                {
                    return false;
                }
                if (!GetCellToUnpollute().IsValid)
                {
                    return false;
                }
                return true;
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.TryGetComp<CompPowerTrader>();
            gameCondition.purifierOnMap.Add(parent);
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (gameCondition.purifierOnMap.Contains(parent))
            {
                gameCondition.purifierOnMap.Remove(parent);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (gameCondition.purifierOnMap.Contains(parent))
            {
                gameCondition.purifierOnMap.Remove(parent);
            }
        }
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (parent.IsHashIntervalTick(Props.interval, delta))
            {
                if (!Active) return;
                Pump();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!shouldSprayToxic) return;
            if (sprayTickLeft > 0)
            {
                sprayTickLeft--;
                if (Rand.Value < 0.6f)
                {
                    Vector3 pos = parent.TrueCenter();
                    pos += Props.effecterVectorOffset;
                    ThrowToxicAirPuffUp(pos, parent.Map);
                }
            }
            else
            {
                shouldSprayToxic = false;
            }
        }
        public static void ThrowToxicAirPuffUp(Vector3 loc, Map map)
        {
            if (loc.ToIntVec3().ShouldSpawnMotesAt(map))
            {
                FleckCreationData dataStatic = FleckMaker.GetDataStatic(loc + new Vector3(Rand.Range(-0.02f, 0.02f), 0f, Rand.Range(-0.02f, 0.02f)), map, ApexDefsOf.APM_AirPuffGreen, 1.5f);
                dataStatic.rotationRate = Rand.RangeInclusive(-240, 240);
                dataStatic.velocityAngle = Rand.Range(-45, 45);
                dataStatic.velocitySpeed = Rand.Range(1.2f, 3.5f);
                map.flecks.CreateFleck(dataStatic);
            }
        }
        private void Pump()
        {
            Map map = parent.Map;
            bool cleanAnyPollution = false;
            if (Props.isCleanRandomTileInMap)
            {
                IEnumerable<IntVec3> cells = parent.Map.AllCells.Where(x => x.IsPolluted(parent.Map));
                if (!cells.EnumerableNullOrEmpty())
                {
                    int num = 0;
                    foreach (var item in cells.InRandomOrder())
                    {
                        cleanAnyPollution |= item.IsPolluted(map);
                        map.pollutionGrid.SetPolluted(item, false);
                        gameCondition.ChangeToxicity(Props.toxicPerTileCleaned);
                        num++;
                        if (num >= Props.tileToUnpollutePerTrigger)
                        {
                            break;
                        }
                    }
                }
                
            }
            else
            {
                IEnumerable<IntVec3> cellToUnpollute = GetCellsToUnpollute();
                if (cellToUnpollute.EnumerableNullOrEmpty())
                {
                    return;
                }
                foreach (var cell in cellToUnpollute)
                {
                    cleanAnyPollution |= cell.IsPolluted(map);
                    map.pollutionGrid.SetPolluted(cell, false);
                    gameCondition.ChangeToxicity(Props.toxicPerTileCleaned);
                }
            }
            if (cleanAnyPollution)
            {
                shouldSprayToxic = true;
                sprayTickLeft = Rand.RangeInclusive(200,500);
            }
            IntVec3 pos = parent.Position;
            if (Props.effecterOffset != null)
            {
                pos += Props.effecterOffset;
            }
            Effecter effecter = Props.pumpEffecterDef?.Spawn(pos, map);
            effecter.Cleanup();
        }

        public IEnumerable<IntVec3> GetCellsToUnpollute()
        {            
            int num = GenRadial.NumCellsInRadius(Props.radius);
            Map map = parent.Map;
            int count = 0;
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = parent.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(map) && intVec.CanUnpollute(map))
                {
                    count++;
                    yield return intVec;
                    if (count >= Props.tileToUnpollutePerTrigger)
                    {
                        break;
                    }
                }
            }
        }
        public IntVec3 GetCellToUnpollute()
        {
            int num = GenRadial.NumCellsInRadius(Props.radius);
            Map map = parent.Map;
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = parent.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(map) && intVec.CanUnpollute(map))
                {
                    return intVec;
                }
            }
            return IntVec3.Invalid;
        }
    }
}
