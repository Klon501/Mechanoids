using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ApexMechanoids
{

	public class HediffCompProperties_SeverityFromCorpses : HediffCompProperties
	{
		public float severityOffsetPerCorpse;

		public float severityOffsetNoCorpses;

		public int squareRange;

		public int checkInterval;

		public HediffCompProperties_SeverityFromCorpses()
		{
			compClass = typeof(HediffComp_SeverityFromCorpses);
		}
	}

	public class HediffComp_SeverityFromCorpses : HediffComp
	{
		public HediffCompProperties_SeverityFromCorpses Props => (HediffCompProperties_SeverityFromCorpses)props;

		private int interval;

		public override void CompPostTick(ref float severityAdjustment)
		{
			base.CompPostTick(ref severityAdjustment);
			interval--;
			if (interval <= 0)
			{
				interval = Props.checkInterval;
				if (Pawn.Spawned)
				{
					Map map = Pawn.Map;
					int num = 0;
					foreach(IntVec3 cell in CellRect.FromCell(Pawn.Position).ExpandedBy(Props.squareRange).ClipInsideMap(map))
					{
						if(cell.GetFirstThing<Corpse>(map) != null)
						{
							num++;
						}
					}
					if(num > 0)
					{
						severityAdjustment += Props.severityOffsetPerCorpse * num;
						return;
					}
				}
				severityAdjustment += Props.severityOffsetNoCorpses;
			}
		}
	}
}