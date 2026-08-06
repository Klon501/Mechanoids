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

        // HediffDef applied to APM allies (speed boost + mental break reduction).
        public HediffDef buffHediff;

        // Boss variant HediffDef (red halo) applied when the caster is a boss.
        public HediffDef buffHediffBoss;

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

            List<Pawn> affected = new List<Pawn>();
            float radiusSq = Props.radius * Props.radius;
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p.Dead || !p.Spawned) continue;
                if (p.Faction == null || p.Faction != caster.Faction) continue;
                if (p.Position.DistanceToSquared(caster.Position) > radiusSq) continue;
                affected.Add(p);
            }

            bool casterIsBoss = caster.kindDef != null && caster.kindDef.defName.EndsWith("_Boss");
            HediffDef activeHediff = (casterIsBoss && Props.buffHediffBoss != null) ? Props.buffHediffBoss : Props.buffHediff;

            for (int i = 0; i < affected.Count; i++)
            {
                Pawn p = affected[i];

                // Hediff (speed + discipline) only for APM mechanoids.
                if (activeHediff != null && IsApexMechanoid(p))
                {
                    if (Props.buffHediff != null)
                    {
                        Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(Props.buffHediff);
                        if (existing != null) p.health.RemoveHediff(existing);
                    }
                    if (Props.buffHediffBoss != null)
                    {
                        Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(Props.buffHediffBoss);
                        if (existing != null) p.health.RemoveHediff(existing);
                    }
                    Hediff newHediff = HediffMaker.MakeHediff(activeHediff, p);
                    float duration = GetAbilityDuration();
                    if (duration > 0f)
                    {
                        HediffComp_Disappears disappears = newHediff.TryGetComp<HediffComp_Disappears>();
                        if (disappears != null)
                            disappears.ticksToDisappear = duration.SecondsToTicks();
                    }
                    p.health.AddHediff(newHediff);
                }

                // Thought only for organic pawns with a mood need.
                if (Props.inspiredThought != null && p.RaceProps.IsFlesh && p.needs?.mood != null)
                    p.needs.mood.thoughts.memories.TryGainMemory(Props.inspiredThought);
            }
        }

        private static bool IsApexMechanoid(Pawn p)
        {
            return p.kindDef != null && p.kindDef.defName.StartsWith("APM_Mech_");
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
