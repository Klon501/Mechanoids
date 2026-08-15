using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    // The Aegis passive. Two jobs:
    //
    //   1. Decide which shield, if any, catches an incoming attack. The damage patch asks; this
    //      comp answers, so the direction rules and the tuning values stay next to each other.
    //   2. Rebuild the shields on its own, slowly, once the mech has been left alone long enough.
    //
    // Shield body parts never change over a pawn's life, so they are looked up once and kept.
    public class CompAegis : ThingComp
    {
        private const int TickRareInterval = 250;

        private List<BodyPartRecord> cachedShieldParts;

        private int ticksSinceDamage;
        private int ticksSinceRegen;

        public CompProperties_Aegis Props => (CompProperties_Aegis)props;

        private Pawn Pawn => parent as Pawn;

        private int RegenerationDelayTicks => (int)(Props.regenerationDelaySeconds * 60f);
        private int RegenerationIntervalTicks => (int)(Props.regenerationIntervalSeconds * 60f);

        public List<BodyPartRecord> ShieldParts
        {
            get
            {
                if (cachedShieldParts == null)
                {
                    cachedShieldParts = new List<BodyPartRecord>();

                    Pawn pawn = Pawn;
                    if (pawn != null && Props.shieldPart != null)
                    {
                        List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
                        for (int i = 0; i < allParts.Count; i++)
                        {
                            if (allParts[i].def == Props.shieldPart)
                            {
                                cachedShieldParts.Add(allParts[i]);
                            }
                        }
                    }
                }

                return cachedShieldParts;
            }
        }

        public float MaxShieldHP
        {
            get
            {
                List<BodyPartRecord> parts = ShieldParts;
                float sum = 0f;
                for (int i = 0; i < parts.Count; i++)
                {
                    sum += parts[i].def.GetMaxHealth(Pawn);
                }
                return sum;
            }
        }

        public float CurShieldHP
        {
            get
            {
                List<BodyPartRecord> parts = ShieldParts;
                float sum = 0f;
                for (int i = 0; i < parts.Count; i++)
                {
                    sum += Pawn.health.hediffSet.GetPartHealth(parts[i]);
                }
                return sum;
            }
        }

        // Whether a body part group is one of this mech's shield sides. Asked by the melee damage
        // patch, which sees a tool's linked group and needs to know if it is looking at a shield.
        public bool IsShieldGroup(BodyPartGroupDef group)
        {
            return group != null && (group == Props.leftShieldGroup || group == Props.rightShieldGroup);
        }

        // ---- Hit interception ----

        // The shield that catches an attack coming from attacker, or null if the attack gets
        // through. Attacks from behind are never caught.
        public BodyPartRecord ShieldInterceptingAttackFrom(Pawn attacker)
        {
            if (ShieldParts.Count == 0)
            {
                return null;
            }

            Rot4 attackRot = Pawn_RotationTracker.RotFromAngleBiased((attacker.DrawPos - Pawn.DrawPos).AngleFlat());
            switch ((attackRot.AsInt - Pawn.Rotation.AsInt + 4) % 4)
            {
                case 0: // Head on. Either shield can take it, whichever is still standing.
                    if (!Rand.Chance(Props.frontDamageChance))
                    {
                        return null;
                    }
                    bool rightFirst = Rand.Bool;
                    return IntactShieldIn(rightFirst ? Props.rightShieldGroup : Props.leftShieldGroup)
                        ?? IntactShieldIn(rightFirst ? Props.leftShieldGroup : Props.rightShieldGroup);
                case 1: // The mech's right.
                    return Rand.Chance(Props.sideDamageChance) ? IntactShieldIn(Props.rightShieldGroup) : null;
                case 3: // The mech's left.
                    return Rand.Chance(Props.sideDamageChance) ? IntactShieldIn(Props.leftShieldGroup) : null;
                default: // From behind.
                    return null;
            }
        }

        private BodyPartRecord IntactShieldIn(BodyPartGroupDef group)
        {
            if (group == null)
            {
                return null;
            }

            List<BodyPartRecord> parts = ShieldParts;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                if (part.groups != null
                    && part.groups.Contains(group)
                    && !Pawn.health.hediffSet.PartIsMissing(part))
                {
                    return part;
                }
            }

            return null;
        }

        // ---- Regeneration ----

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            // Restart the peace timer, but leave the regen throttle alone so a steady trickle of
            // chip damage cannot stall regeneration forever.
            if (totalDamageDealt > 0f)
            {
                ticksSinceDamage = 0;
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (Pawn == null || ShieldParts.Count == 0)
            {
                return;
            }

            if (CurShieldHP >= MaxShieldHP)
            {
                ticksSinceDamage = 0;
                ticksSinceRegen = 0;
                return;
            }

            ticksSinceDamage += TickRareInterval;
            ticksSinceRegen += TickRareInterval;

            if (ticksSinceDamage < RegenerationDelayTicks || ticksSinceRegen < RegenerationIntervalTicks)
            {
                return;
            }

            ticksSinceRegen = 0;
            RegenerateStep();
        }

        // Heals a little off every damaged shield and rebuilds destroyed ones a step at a time.
        private void RegenerateStep()
        {
            bool changed = false;
            List<BodyPartRecord> parts = ShieldParts;

            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                bool stepped = Pawn.health.hediffSet.PartIsMissing(part)
                    ? TryRebuildShield(part)
                    : TryHealShield(part);

                changed |= stepped;
            }

            if (changed && Pawn.Spawned)
            {
                FleckMaker.ThrowMetaIcon(Pawn.Position, Pawn.Map, FleckDefOf.HealingCross);
            }
        }

        // Mirrors how Pawn_HealthTracker regenerates a missing part: only rebuild one whose parent
        // is still attached, and bring it back nearly destroyed so the HP regen below finishes the
        // job rather than handing back a whole shield in one step. Skipping the parent check would
        // reattach a shield to an arm that is no longer there, and HediffSet.AddDirect rejects
        // hediffs on missing parts.
        private bool TryRebuildShield(BodyPartRecord part)
        {
            HediffSet hediffSet = Pawn.health.hediffSet;
            if (part.parent == null || hediffSet.PartIsMissing(part.parent))
            {
                return false;
            }

            Hediff missing = hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(part);
            if (missing == null)
            {
                return false;
            }

            Pawn.health.RemoveHediff(missing);

            float maxHealth = hediffSet.GetPartHealth(part);
            float restored = Mathf.Min(Props.regenerationHPPerStep, maxHealth);
            if (restored < maxHealth)
            {
                Pawn.health.AddHediff(HediffDefOf.Misc, part).Severity = maxHealth - restored;
            }

            return true;
        }

        // Heals the worst injury on the shield, so a shield covered in scratches closes the big
        // wound first instead of whichever one happens to sit earliest in the hediff list.
        private bool TryHealShield(BodyPartRecord part)
        {
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            Hediff_Injury worst = null;

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury
                    && injury.Part == part
                    && (worst == null || injury.Severity > worst.Severity))
                {
                    worst = injury;
                }
            }

            if (worst == null)
            {
                return false;
            }

            worst.Heal(Props.regenerationHPPerStep);
            return true;
        }

        // ---- Save/load ----

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksSinceDamage, "ticksSinceDamage", 0);
            Scribe_Values.Look(ref ticksSinceRegen, "ticksSinceRegen", 0);
        }

        // ---- Gizmo bar ----

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = Pawn;
            if (pawn != null && pawn.Faction == Faction.OfPlayer && MaxShieldHP > 0f)
            {
                yield return new Gizmo_ShieldHP { comp = this };
            }
        }
    }
}
