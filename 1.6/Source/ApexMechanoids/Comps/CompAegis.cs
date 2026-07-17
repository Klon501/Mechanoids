using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompAegis : ThingComp
    {
        private int ticksSinceDamage;
        private int ticksSinceRegen;
        private const int CompTickRareInterval = 250;

        public ModExtension_Aegis Ext => parent.def.GetModExtension<ModExtension_Aegis>();
        public Pawn Pawn => parent as Pawn;

        private int RegenerationDelayTicks => (int)(Ext.regenerationDelaySeconds * 60f);
        private int RegenerationIntervalTicks => (int)(Ext.regenerationIntervalSeconds * 60f);

        // ---- Shield HP accounting (used by the gizmo bar and the repair energy patch) ----

        public IEnumerable<BodyPartRecord> ShieldParts()
        {
            if (Pawn == null || Ext?.shieldPart == null)
            {
                yield break;
            }

            foreach (BodyPartRecord part in Pawn.RaceProps.body.AllParts)
            {
                if (part.def == Ext.shieldPart)
                {
                    yield return part;
                }
            }
        }

        public float MaxShieldHP
        {
            get
            {
                float sum = 0f;
                foreach (BodyPartRecord part in ShieldParts())
                {
                    sum += part.def.GetMaxHealth(Pawn);
                }
                return sum;
            }
        }

        public float CurShieldHP
        {
            get
            {
                float sum = 0f;
                foreach (BodyPartRecord part in ShieldParts())
                {
                    sum += Pawn.health.hediffSet.GetPartHealth(part);
                }
                return sum;
            }
        }

        public float ShieldHPPercent
        {
            get
            {
                float max = MaxShieldHP;
                return max > 0f ? Mathf.Clamp01(CurShieldHP / max) : 0f;
            }
        }

        // ---- Save/load ----

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksSinceDamage, "ticksSinceDamage", 0);
            Scribe_Values.Look(ref ticksSinceRegen, "ticksSinceRegen", 0);
        }

        // ---- Damage tracking ----

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            if (totalDamageDealt <= 0f || Pawn == null || Ext == null)
            {
                return;
            }

            if (AnyShieldsMissingOrDamaged())
            {
                // Restart the peace timer; do not reset the regen throttle so occasional chip
                // damage cannot stall regeneration forever.
                ticksSinceDamage = 0;
            }
        }

        // ---- Slow regeneration ----

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (Pawn == null || Ext == null)
            {
                return;
            }

            if (!AnyShieldsMissingOrDamaged())
            {
                ticksSinceDamage = 0;
                ticksSinceRegen = 0;
                return;
            }

            ticksSinceDamage += CompTickRareInterval;
            ticksSinceRegen += CompTickRareInterval;

            if (ticksSinceDamage >= RegenerationDelayTicks && ticksSinceRegen >= RegenerationIntervalTicks)
            {
                RegenerateOneShieldStep();
                ticksSinceRegen = 0;
            }
        }

        // Heals a small amount of HP on every damaged shield, and slowly rebuilds destroyed ones.
        private void RegenerateOneShieldStep()
        {
            bool changed = false;

            foreach (BodyPartRecord part in ShieldParts())
            {
                if (Pawn.health.hediffSet.PartIsMissing(part))
                {
                    RebuildMissingShield(part);
                    changed = true;
                }
                else if (Pawn.health.hediffSet.GetInjuredParts().Contains(part))
                {
                    HealShieldInjury(part);
                    changed = true;
                }
            }

            if (changed && Pawn.Spawned)
            {
                FleckMaker.ThrowMetaIcon(Pawn.Position, Pawn.Map, FleckDefOf.HealingCross);
            }
        }

        private void RebuildMissingShield(BodyPartRecord part)
        {
            // Restore the part, then re-add it as an almost-destroyed injury so that the HP-based
            // regen heals it back up gradually rather than instantly.
            Hediff missing = Pawn.health.hediffSet.GetMissingPartFor(part);
            if (missing != null)
            {
                Pawn.health.RemoveHediff(missing);
            }

            float maxHP = part.def.GetMaxHealth(Pawn);
            if (HediffMaker.MakeHediff(HediffDefOf.Cut, Pawn, part) is Hediff_Injury injury)
            {
                injury.Severity = Mathf.Max(1f, maxHP - Ext.regenerationHPPerStep);
                Pawn.health.AddHediff(injury, part);
            }
        }

        private void HealShieldInjury(BodyPartRecord part)
        {
            Hediff_Injury injury = Pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .FirstOrDefault(h => h.Part == part);

            injury?.Heal(Ext.regenerationHPPerStep);
        }

        private bool AnyShieldsMissingOrDamaged()
        {
            foreach (BodyPartRecord part in ShieldParts())
            {
                if (Pawn.health.hediffSet.PartIsMissing(part))
                {
                    return true;
                }

                if (Pawn.health.hediffSet.GetInjuredParts().Contains(part))
                {
                    return true;
                }
            }

            return false;
        }

        // ---- Gizmo bar ----

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (Pawn == null || Ext == null || Pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (MaxShieldHP <= 0f)
            {
                yield break;
            }

            yield return new Gizmo_ShieldHP { comp = this };
        }
    }
}
