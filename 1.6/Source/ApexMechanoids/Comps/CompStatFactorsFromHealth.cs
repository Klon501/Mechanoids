using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ApexMechanoids
{
	public class CompProperties_StatFactorsFromHealth : CompProperties
	{
		public class StatFactorFromHealth
		{
			public StatFactorFromHealth() { }

			public StatDef stat;

			public SimpleCurve curve;
		}

		public List<StatFactorFromHealth> statFactors;

		public CompProperties_StatFactorsFromHealth()
		{
			compClass = typeof(CompStatFactorsFromHealth);
		}
	}
	public class CompStatFactorsFromHealth : ThingComp
	{
		public Pawn Pawn => parent as Pawn;

		public CompProperties_StatFactorsFromHealth Props => (CompProperties_StatFactorsFromHealth)props;

		public override void CompTick()
		{
			base.CompTick();
			if (Pawn.IsHashIntervalTick(120)) // Update every 60 ticks (1 second)
			{
				UpdateStatHediffs();
			}
		}

		private void UpdateStatHediffs()
		{
			if (Props.statFactors == null) return;

			foreach(var statFactor in Props.statFactors)
			{
				if (statFactor.stat == null || statFactor.curve == null) continue;

				string hediffDefName = $"APM_StatFactor_{statFactor.stat.defName}";
				HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
				
				if (hediffDef == null)
				{
					// Create hediff def dynamically if it doesn't exist
					hediffDef = CreateHediffDef(statFactor.stat);
				}

				// Get or add hediff
				Hediff existingHediff = Pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
				float healthPercent = Pawn.health.summaryHealth.SummaryHealthPercent;
				float factor = statFactor.curve.Evaluate(healthPercent);
				
				if (factor != 1f) // Only add hediff if there's actually a modifier
				{
					if (existingHediff == null)
					{
						existingHediff = HediffMaker.MakeHediff(hediffDef, Pawn);
						Pawn.health.AddHediff(existingHediff);
					}
					
					// Update the severity to reflect the current factor
					existingHediff.Severity = Math.Abs(factor - 1f) * 10; // Scale for visibility
				}
				else if (existingHediff != null)
				{
					// Remove hediff if no modifier
					Pawn.health.RemoveHediff(existingHediff);
				}
			}
		}

		private HediffDef CreateHediffDef(StatDef statDef)
		{
			string defName = $"APM_StatFactor_{statDef.defName}";
			
			var hediffDef = new HediffDef
			{
				defName = defName,
				label = $"{statDef.label} from health",
				description = $"This mechanoid's {statDef.label} is affected by its health condition.",
				hediffClass = typeof(Hediff_StatFactorFromHealth),
				defaultLabelColor = UnityEngine.Color.cyan,
				isBad = false,
				stages = new List<HediffStage>
				{
					new HediffStage
					{
						minSeverity = 0.01f,
						label = "health affected"
					}
				}
			};

			// Add to def database
			DefDatabase<HediffDef>.Add(hediffDef);
			return hediffDef;
		}

		public override float GetStatFactor(StatDef stat)
		{
			float num = base.GetStatFactor(stat);
			foreach(CompProperties_StatFactorsFromHealth.StatFactorFromHealth item in Props.statFactors)
			{
				if(item.stat == null)
					{
						Log.Error("[ApexMechanoids] CompStatFactorsFromHealth: statFactor has null stat def.");
					}
				if(item.stat == stat)
				{
					float num2 = Pawn.health.summaryHealth.SummaryHealthPercent;
					num = item.curve.Evaluate(num2);
					return num;
				}
			}
			return num;
		}
	}

	public class Hediff_StatFactorFromHealth : HediffWithComps
	{
		public override string TipStringExtra
		{
			get
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(base.TipStringExtra);
				
				// Get the stat factor information
				if (pawn?.GetComp<CompStatFactorsFromHealth>() is CompStatFactorsFromHealth comp)
				{
					foreach(var statFactor in comp.Props.statFactors)
					{
						if ($"APM_StatFactor_{statFactor.stat.defName}" == def.defName)
						{
							float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;
							float factor = statFactor.curve.Evaluate(healthPercent);
							float percentageChange = (factor - 1f) * 100f;
							
							string changeText = percentageChange > 0 ? "+" + percentageChange.ToString("F0") : percentageChange.ToString("F0");
							sb.AppendLine($"{statFactor.stat.LabelCap}: {changeText}%");
							break;
						}
					}
				}
				
				return sb.ToString().TrimEnd();
			}
		}
	}
}