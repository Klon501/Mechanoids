using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Analytics;
using Verse;

namespace ApexMechanoids
{
    public class CompToxicPurifier : ThingComp
    {
        public CompProperties_ToxicPurifier Props => (CompProperties_ToxicPurifier)props;

        public GameCondition_ToxicPurifier conditionCached;
        public GameCondition_ToxicPurifier GameCondition
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

        public CompPowerTrader compPower;

        public bool shouldSprayToxic = false;

        public int sprayTickLeft = -1;
        private bool Active
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
                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.TryGetComp<CompPowerTrader>();
			if (!GameCondition.purifiersOnMap.Contains(parent))
			{
				GameCondition.purifiersOnMap.Add(parent);
			}
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (GameCondition.purifiersOnMap.Contains(parent))
            {
				GameCondition.purifiersOnMap.Remove(parent);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (GameCondition.purifiersOnMap.Contains(parent))
            {
				GameCondition.purifiersOnMap.Remove(parent);
            }
        }

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			if (!Props.clearWholeMap && parent.Spawned)
			{
				GenDraw.DrawRadiusRing(parent.Position, Props.radius);
			}
		}
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (parent.IsHashIntervalTick(Props.interval, delta))
            {
				if (!Active) return;
				IntVec3 cell = GetCellToUnpollute();
                if (cell.IsValid)
                {
					Pump(cell);
				}
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
                    ThrowToxicAirPuffUp(parent.TrueCenter() + Props.EffectOffsetForRot(parent.Rotation), parent.Map);
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

        private void Pump(IntVec3 cell)
        {
            Map map = parent.Map;
			map.pollutionGrid.SetPolluted(cell, false);
			GameCondition.ChangeToxicity(Props.toxicPerTileCleaned);
			shouldSprayToxic = true;
			sprayTickLeft = Rand.RangeInclusive(200, 500);
            Effecter effecter = Props.pumpEffecterDef?.Spawn(parent, map, Props.EffectOffsetForRot(parent.Rotation));
            effecter.Cleanup();
        }

		private IntVec3 GetCellToUnpollute()
		{
			Map map = parent.Map;
			if (Props.clearWholeMap)
            {
                CellRect cellRect = CellRect.FromCell(parent.Position);
                int count = Mathf.RoundToInt((float)Mathf.Max(map.Size.x, map.Size.z) / 2f);
                bool flag = true;
                while (flag)
                {
                    flag = false;
					foreach (IntVec3 cell in cellRect.EdgeCells)
                    {
						if (cell.InBounds(map))
						{
                            flag = true;
                            if (cell.CanUnpollute(map))
                            {
                                return cell;
                            }
						}
					}
                    cellRect = cellRect.ExpandedBy(1);
				}
				return IntVec3.Invalid;
			}
			int num = GenRadial.NumCellsInRadius(Props.radius);
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

		public override void PostExposeData()
		{
			base.PostExposeData();
            Scribe_Values.Look(ref shouldSprayToxic, "shouldSprayToxic");
			Scribe_Values.Look(ref sprayTickLeft, "sprayTickLeft", defaultValue: -1);
		}
    }
}
