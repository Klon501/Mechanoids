using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
	/// <summary>
	/// Hediff comp that banks "charges" whenever its host downs or kills a target,
	/// then spends those charges to self-repair whenever the host is damaged.
	/// </summary>
	public class HediffCompProperties_VampiricMechanites : HediffCompProperties
	{
		// Charges gained per downed target.
		public int chargesPerDown = 1;

		// Charges gained per killed target.
		public int chargesPerKill = 2;

		// Maximum banked charges.
		public int maxCharges = 25;

		// Ticks between self-repair attempts.
		public int healIntervalTicks = 30;

		// HP restored per heal tick. One heal tick consumes one charge.
		public float healAmountPerTick = 4f;

		// How many heal ticks a single charge is worth. Each heal tick heals
		// healAmountPerTick HP and consumes one charge, so one charge restores
		// (healTicksPerCharge * healAmountPerTick) HP before being spent.
		public int healTicksPerCharge = 5;

		// Whether a single charge can regrow a missing body part.
		public bool restoreMissingParts = true;

		public HediffCompProperties_VampiricMechanites()
		{
			compClass = typeof(HediffComp_VampiricMechanites);
		}
	}

	public class HediffComp_VampiricMechanites : HediffComp
	{
		public HediffCompProperties_VampiricMechanites Props => (HediffCompProperties_VampiricMechanites)props;

		private float charges;

		private int healTimer;

		// Pawns that currently carry this comp. Used as a cheap negative filter so the
		// global damage patch does not scan every attacker's hediff list on every hit.
		// Weak keys so entries for dead/removed pawns are collected automatically.
		private static readonly ConditionalWeakTable<Pawn, object> pawnsWithVampiric = new ConditionalWeakTable<Pawn, object>();

		// Victims already credited for a down, so a still-downed pawn is not repeatedly
		// banked by every follow-up hit. Weak keys avoid holding victims alive.
		// Internal so the sibling damage-patch class can access it.
		internal static readonly ConditionalWeakTable<Pawn, object> downedCredited = new ConditionalWeakTable<Pawn, object>();

		public float Charges => charges;

		public override void CompPostMake()
		{
			base.CompPostMake();
			if (Pawn != null && !pawnsWithVampiric.TryGetValue(Pawn, out _))
			{
				pawnsWithVampiric.Add(Pawn, null);
			}
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			base.CompPostTick(ref severityAdjustment);
			if (!Pawn.Spawned || charges <= 0)
			{
				return;
			}

			healTimer--;
			if (healTimer > 0)
			{
				return;
			}
			healTimer = Props.healIntervalTicks;

			TryHealOnce();
		}

		// Spends one repair attempt's worth of charges. Returns true if anything was repaired.
		private bool TryHealOnce()
		{
			HediffSet hediffSet = Pawn.health.hediffSet;

			// Prioritise regrowing a missing part.
			if (Props.restoreMissingParts)
			{
				Hediff_MissingPart missing = hediffSet.GetMissingPartsCommonAncestors().FirstOrDefault();
				if (missing != null)
				{
				Pawn.health.RestorePart(missing.Part);
				charges = Mathf.Max(0, charges - 1);
				return true;
				}
			}

			// Otherwise heal injuries up to the per-tick budget.
			float budget = Props.healAmountPerTick;
			List<Hediff> hediffs = hediffSet.hediffs;
			bool healed = false;
			for (int i = 0; i < hediffs.Count; i++)
			{
				if (budget <= 0f)
				{
					break;
				}
				Hediff injury = hediffs[i];
				if (!(injury is Hediff_Injury))
				{
					continue;
				}
				float amount = Mathf.Min(injury.Severity, budget);
				injury.Heal(amount);
				budget -= amount;
				healed = true;
			}

			if (healed)
			{
				charges = Mathf.Max(0, charges - 1);
			}
			return healed;
		}

		public void AddCharges(int amount)
		{
			if (amount <= 0)
			{
				return;
			}
			charges = Mathf.Min(Props.maxCharges, charges + amount);
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_Values.Look(ref charges, "vampiricCharges", 0);
			Scribe_Values.Look(ref healTimer, "vampiricHealTimer", 0);
		}

		public override string CompTipStringExtra => "APM_VampiricCharges".Translate(charges, Props.maxCharges, charges * Props.healTicksPerCharge);

		// Finds the active comp on a given pawn (used by the damage patch).
		public static HediffComp_VampiricMechanites GetOn(Pawn pawn)
		{
			if (pawn == null || pawn.health == null || !pawnsWithVampiric.TryGetValue(pawn, out _))
			{
				return null;
			}
			foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
			{
				if (hediff is HediffWithComps withComps && withComps.comps != null)
				{
					foreach (HediffComp comp in withComps.comps)
					{
						if (comp is HediffComp_VampiricMechanites vampiric)
						{
							return vampiric;
						}
					}
				}
			}
			return null;
		}
	}

	// Credits the attacking pawn's Vampiric Mechanites whenever it downs or kills a target.
	[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
	public static class VampiricMechanites_DamagePatch
	{
		[HarmonyPostfix]
		public static void PostApplyDamage(Pawn_HealthTracker __instance, DamageInfo dinfo, float totalDamageDealt)
		{
			if (dinfo.Instigator == null)
			{
				return;
			}

			Pawn attacker = dinfo.Instigator as Pawn;
			if (attacker == null && dinfo.Instigator is Projectile projectile)
			{
				attacker = projectile.Launcher as Pawn;
			}
			if (attacker == null)
			{
				return;
			}

			Pawn victim = __instance.pawn;
			if (victim == attacker)
			{
				return;
			}

			HediffComp_VampiricMechanites comp = HediffComp_VampiricMechanites.GetOn(attacker);
			if (comp == null)
			{
				return;
			}

			if (victim.Dead)
			{
				HediffComp_VampiricMechanites.downedCredited.Remove(victim);
				comp.AddCharges(comp.Props.chargesPerKill);
			}
			else if (victim.Downed)
			{
				if (!HediffComp_VampiricMechanites.downedCredited.TryGetValue(victim, out _))
				{
					HediffComp_VampiricMechanites.downedCredited.Add(victim, null);
					comp.AddCharges(comp.Props.chargesPerDown);
				}
			}
			else
			{
				// Victim recovered (or was never downed); allow future downs to be credited.
				HediffComp_VampiricMechanites.downedCredited.Remove(victim);
			}
		}
	}
}
