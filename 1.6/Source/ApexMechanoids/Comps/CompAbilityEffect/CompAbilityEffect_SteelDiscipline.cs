using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilitySteelDiscipline : CompProperties_AbilityEffect
    {
        // Radius in which allies are buffed.
        public float radius = 12f;

        public HediffDef buffHediff;

        public HediffDef buffHediffBoss;

        public bool apexMechsOnly = true;

        // Thought given to organic same-faction pawns that have a mood need.
        public ThoughtDef inspiredThought = null;

        public CompProperties_AbilitySteelDiscipline()
        {
            compClass = typeof(CompAbilityEffect_SteelDiscipline);
        }
    }

    public class CompAbilityEffect_SteelDiscipline : CompAbilityEffect
    {
        public new CompProperties_AbilitySteelDiscipline Props => (CompProperties_AbilitySteelDiscipline)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster.Map;
            if (map == null || Props.buffHediff == null)
            {
                return;
            }

            bool casterIsBoss = caster.kindDef != null && caster.kindDef.defName.EndsWith("_Boss");
            HediffDef activeHediff = (casterIsBoss && Props.buffHediffBoss != null) ? Props.buffHediffBoss : Props.buffHediff;

            float radiusSq = Props.radius * Props.radius;
            int durationTicks = 0;
            float duration = GetAbilityDuration();
            if (duration > 0f)
            {
                durationTicks = duration.SecondsToTicks();
            }

            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p.Dead || !p.Spawned) continue;
                if (p.Faction == null || p.Faction != caster.Faction) continue;
                if (p.Position.DistanceToSquared(caster.Position) > radiusSq) continue;

                if (IsBuffTarget(p))
                {
                    ApplyBuff(p, activeHediff, durationTicks);
                }

                if (Props.inspiredThought != null && p.RaceProps.IsFlesh && p.needs?.mood != null)
                    p.needs.mood.thoughts.memories.TryGainMemory(Props.inspiredThought);
            }
        }

        private void ApplyBuff(Pawn p, HediffDef activeHediff, int durationTicks)
        {
            HediffDef other = (activeHediff == Props.buffHediff) ? Props.buffHediffBoss : Props.buffHediff;
            if (other != null && other != activeHediff)
            {
                Hediff stale = p.health.hediffSet.GetFirstHediffOfDef(other);
                if (stale != null) p.health.RemoveHediff(stale);
            }

            Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(activeHediff);
            if (existing != null)
            {
                if (durationTicks > 0)
                {
                    HediffComp_Disappears running = existing.TryGetComp<HediffComp_Disappears>();
                    if (running != null)
                        running.ticksToDisappear = durationTicks;
                }
                return;
            }

            Hediff newHediff = HediffMaker.MakeHediff(activeHediff, p);
            if (durationTicks > 0)
            {
                HediffComp_Disappears disappears = newHediff.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                    disappears.ticksToDisappear = durationTicks;
            }
            p.health.AddHediff(newHediff);
        }

        private bool IsBuffTarget(Pawn p)
        {
            if (Props.apexMechsOnly)
            {
                return p.kindDef != null && p.kindDef.defName.StartsWith("APM_Mech_");
            }

            return p.RaceProps != null && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid);
        }

        private float GetAbilityDuration()
        {
            List<StatModifier> statBases = parent.def.statBases;
            if (statBases == null) return 0f;
            for (int i = 0; i < statBases.Count; i++)
            {
                if (statBases[i].stat == StatDefOf.Ability_Duration)
                    return statBases[i].value;
            }
            return 0f;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(parent.pawn.Position, Props.radius, ApexMechColors.GetAbilityColor(parent.pawn));
        }
    }
}
