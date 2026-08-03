using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Analytics;
using Verse;
using Verse.Noise;

namespace ApexMechanoids
{
	public enum PurifierMode
	{
		WastepacksOverGround = 0,
		GroundOverWastepacks = 1,
		OnlyWastepacks = 2,
        OnlyGround = 3
	}

	[StaticConstructorOnStartup]
	public static class PurifierModeUtility
	{
		public static readonly Texture2D WastepacksOverGround = ContentFinder<Texture2D>.Get("UI/Gizmos/WastepacksOverGround");
		public static readonly Texture2D GroundOverWastepacks = ContentFinder<Texture2D>.Get("UI/Gizmos/GroundOverWastepacks");
		public static readonly Texture2D OnlyWastepacks = ContentFinder<Texture2D>.Get("UI/Gizmos/OnlyWastepacks");
		public static readonly Texture2D OnlyGround = ContentFinder<Texture2D>.Get("UI/Gizmos/OnlyGround");
		public static string GetLabel(this PurifierMode mode)
		{
            switch (mode)
            {
                case PurifierMode.OnlyGround:
                    return "APM_ToxicPurifier_Mode_OnlyGround".Translate();
                case PurifierMode.OnlyWastepacks:
					return "APM_ToxicPurifier_Mode_OnlyWastepacks".Translate();
                case PurifierMode.WastepacksOverGround:
					return "APM_ToxicPurifier_Mode_WastepacksOverGround".Translate();
                default:
					return "APM_ToxicPurifier_Mode_GroundOverWastepacks".Translate();
			}
		}

		public static Texture2D GetIcon(this PurifierMode mode)
		{
			switch (mode)
			{
				case PurifierMode.OnlyGround:
					return OnlyGround;
				case PurifierMode.OnlyWastepacks:
					return OnlyWastepacks;
				case PurifierMode.WastepacksOverGround:
					return WastepacksOverGround;
				default:
					return GroundOverWastepacks;
			}
		}
	}

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

		public bool Full => wastepacksCount >= Props.wastepackCapacity;

		public float FillPercent => (float)wastepacksCount / (float)Props.wastepackCapacity;

        public PurifierMode mode = PurifierMode.GroundOverWastepacks;

        public CompPowerTrader compPower;

        public bool shouldSprayToxic = false;

        public int sprayTickLeft = -1;

        public int wastepacksCount;

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
            Thing t = ThingMaker.MakeThing(ThingDefOf.Wastepack);
            t.stackCount = wastepacksCount;
            GenPlace.TryPlaceThing(t, parent.Position, map, ThingPlaceMode.Near);
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
                if(wastepacksCount > 0 && (mode == PurifierMode.WastepacksOverGround || mode == PurifierMode.OnlyWastepacks))
                {
                    wastepacksCount--;
					Pump();
                    return;
				}
                if(mode == PurifierMode.OnlyWastepacks)
                {
                    return;
                }
				IntVec3 cell = GetCellToUnpollute();
				if (cell.IsValid)
				{
					parent.Map.pollutionGrid.SetPolluted(cell, false);
					Pump();
					return;
				}
				if (mode == PurifierMode.OnlyGround)
				{
					return;
				}
				if (wastepacksCount > 0)
				{
					wastepacksCount--;
					Pump();
					return;
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

        private void Pump()
        {
            Map map = parent.Map;
			GameCondition.ChangeToxicity(Props.toxicPerTileCleaned);
			shouldSprayToxic = true;
			sprayTickLeft = Rand.RangeInclusive(200, 500);
            Effecter effecter = Props.pumpEffecterDef?.Spawn(parent, map, Props.EffectOffsetForRot(parent.Rotation));
            effecter.Cleanup();
        }

		public IntVec3 GetCellToUnpollute()
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

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "APM_ToxicPurifier_ModeChangeLabel".Translate() + ": " + mode.GetLabel();
			command_Action.defaultDesc = "APM_ToxicPurifier_ModeChangeDesc".Translate();
			command_Action.icon = mode.GetIcon();
			command_Action.action = delegate
            {
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				for(int i = 0; i < 4; i++)
				{
					PurifierMode localMode = (PurifierMode)i;
					list.Add(new FloatMenuOption(localMode.GetLabel(), delegate
					{
                        mode = localMode;
					}, localMode.GetIcon(), Color.white));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			};
			yield return command_Action;
		}

		public override string CompInspectStringExtra()
		{
			return "ContainedThings".Translate(ThingDefOf.Wastepack) + ": " + wastepacksCount.ToString() + " / " + Props.wastepackCapacity.ToString();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref wastepacksCount, "wastepacksCount");
			Scribe_Values.Look(ref mode, "mode");
			Scribe_Values.Look(ref shouldSprayToxic, "shouldSprayToxic");
			Scribe_Values.Look(ref sprayTickLeft, "sprayTickLeft", defaultValue: -1);
		}
    }
}
