using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace ApexMechanoids
{
	public class Mote_Whip : MoteDualAttached
	{
		public new Graphic graphicInt;

		public int num = 1;
		public override Graphic Graphic
		{
			get
			{
				if (graphicInt == null)
				{
					if (def.graphicData == null)
					{
						return BaseContent.BadGraphic;
					}
					GraphicData data = new GraphicData();
					data.CopyFrom(def.graphicData);
					data.texPath = data.texPath.Remove(data.texPath.Length - 1) + num.ToString();
					data.drawSize = new Vector2(def.graphicData.drawSize.x, flipped ? -def.graphicData.drawSize.y : def.graphicData.drawSize.y);
					graphicInt = data.GraphicColoredFor(this);
				}
				return graphicInt;
			}
		}

		public bool flag = false;
		public bool flipped = false;

		public override void Tick()
		{
			if(num < 15)
			{
				if (flag)
				{
					num++;
					graphicInt = null;
				}
				flag = !flag;
			}
			base.Tick();
		}
	}

	public class Verb_MeleeWhip : Verb_MeleeAttack
	{
		public override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
		{
			if (CasterPawn != null)
				CasterPawn.rotationTracker.FaceCell(target.Cell);
			Verb_Whip.Cast(Caster, target, out var result, EquipmentSource, tool);
			return result;
		}
	}

	public class Verb_Whip : Verb
	{
		public static readonly float WhipWidth = 3.6f;
		public override void DrawHighlight(LocalTargetInfo target)
		{
			base.DrawHighlight(target);
			if (!target.IsValid)
			{
				return;
			}
			List<IntVec3> cells = AffectedCells(Caster, target, WhipWidth);
			if (cells.Any())
			{
				GenDraw.DrawFieldEdges(cells);
			}
		}

		private static List<IntVec3> AffectedCells(Thing caster, LocalTargetInfo target, float lineWidthEnd)
		{
			List<IntVec3> tmpCells = new List<IntVec3>();
			Vector3 vector = caster.Position.ToVector3Shifted().Yto0();
			IntVec3 intVec = target.Cell.ClampInsideMap(caster.Map);
			IntVec3 pos = caster.Position;
			float range = pos.DistanceTo(intVec) + 0.5f;
			if (pos == intVec)
			{
				return tmpCells;
			}
			float lengthHorizontal = (intVec - pos).LengthHorizontal;
			float num = (float)(intVec.x - pos.x) / lengthHorizontal;
			float num2 = (float)(intVec.z - pos.z) / lengthHorizontal;
			intVec.x = Mathf.RoundToInt((float)pos.x + num * range);
			intVec.z = Mathf.RoundToInt((float)pos.z + num2 * range);
			float target2 = Vector3.SignedAngle(intVec.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up);
			float num3 = lineWidthEnd / 2f;
			float num4 = Mathf.Sqrt(Mathf.Pow((intVec - pos).LengthHorizontal, 2f) + Mathf.Pow(num3, 2f));
			float num5 = 57.29578f * Mathf.Asin(num3 / num4);
			int num6 = GenRadial.NumCellsInRadius(range);
			for (int i = 0; i < num6; i++)
			{
				IntVec3 intVec2 = pos + GenRadial.RadialPattern[i];
				if (CanUseCell(intVec2) && Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(intVec2.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up), target2)) <= num5)
				{
					tmpCells.Add(intVec2);
				}
			}
			List<IntVec3> list = GenSight.BresenhamCellsBetween(pos, intVec);
			for (int j = 0; j < list.Count; j++)
			{
				IntVec3 intVec3 = list[j];
				if (!tmpCells.Contains(intVec3) && CanUseCell(intVec3))
				{
					tmpCells.Add(intVec3);
				}
			}
			IntVec3 cell = intVec - pos;
			if (cell == IntVec3.East || cell == IntVec3.West)
			{
				tmpCells.Add(intVec + IntVec3.South);
				tmpCells.Add(intVec + IntVec3.North);
			}
			return tmpCells;
			bool CanUseCell(IntVec3 c)
			{
				if (!c.InBounds(caster.Map))
				{
					return false;
				}
				if (c == pos)
				{
					return false;
				}
				if (!c.InHorDistOf(pos, range))
				{
					return false;
				}
				if (!GenSight.LineOfSight(pos, c, caster.Map, skipFirstCell: true))
				{
					return false;
				}
				return true;
			}
		}

		public override bool TryCastShot()
		{
			if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
			{
				return false;
			}
			if (CasterPawn != null)
				CasterPawn.rotationTracker.FaceCell(currentTarget.Cell);
			Cast(Caster, currentTarget, out var _, EquipmentSource, EquipmentSource.def.tools.FirstOrDefault((x)=>x.capacities.Any((y)=>y.defName == "APM_Whip")));
			lastShotTick = Find.TickManager.TicksGame;
			return true;
		}

		public static ThingDef moteDef;

		public static void Cast(Thing caster, LocalTargetInfo target, out DamageWorker.DamageResult result, Thing source = null, Tool tool = null)
		{
			Map map = caster.Map;
			if (moteDef == null)
			{
				moteDef = DefDatabase<ThingDef>.GetNamed("APM_Mote_Whip");
			}
			float whipAngle = caster.TrueCenter().AngleToFlat(target.CenterVector3);
				Vector3 vec = new Vector3(0.5f, 0f, 0f).RotatedBy(whipAngle);
				Mote whipMote = MoteMaker.MakeInteractionOverlay(moteDef, caster, new TargetInfo(target.Cell, map), Vector3.zero, vec);
			if (whipMote is Mote_Whip moteWhip)
			{
				moteWhip.flipped = whipAngle >= 180f;
			}
			result = null;
			if (target.HasThing)
			{
				result = target.Thing.TakeDamage(GetDamageInfo(caster, target, source, tool.power, tool));
			}
			List<IntVec3> cells = AffectedCells(caster, target, WhipWidth);
			if (cells.Any())
			{
				foreach (IntVec3 cell in cells)
				{
					foreach (Thing t in cell.GetThingList(map).ToList())
					{
						if (target.Thing != t && ((t.Faction == null && t is Building) || t.HostileTo(caster)))
						{
							t.TakeDamage(GetDamageInfo(caster, t, source, tool.power * 0.5f, tool));
						}
					}
				}
			}
		}

		public static DamageInfo GetDamageInfo(Thing caster, LocalTargetInfo target, Thing source = null, float amount = 10f, Tool tool = null)
		{
			Vector3 direction = (target.Thing.Position - caster.Position).ToVector3();
			bool instigatorGuilty = !(caster is Pawn pawn) || !pawn.Drafted;
			DamageInfo damageInfo = new DamageInfo(DamageDefOf.Cut, amount, amount * 0.015f, -1f, caster, null, source?.def, DamageInfo.SourceCategory.ThingOrUnknown, null, instigatorGuilty);
			damageInfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
			damageInfo.SetAngle(direction);
			damageInfo.SetTool(tool);
			if(source != null && source.TryGetQuality(out var quality))
			{
				damageInfo.SetWeaponQuality(quality);
			}
			return damageInfo;
		}

		public override bool Available()
		{
			if (!base.Available())
			{
				return false;
			}
			return true;
		}
	}
}
