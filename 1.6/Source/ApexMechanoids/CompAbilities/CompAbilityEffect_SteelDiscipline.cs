using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_AbilitySteelDiscipline : CompProperties_AbilityEffect
    {
        // Radius in which allies are buffed.
        public float radius = 12f;

        // HediffDef applied to APM allies (speed boost + mental break reduction).
        public HediffDef buffHediff;

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

            for (int i = 0; i < affected.Count; i++)
            {
                Pawn p = affected[i];

                // Hediff (speed + discipline) only for APM mechanoids.
                if (Props.buffHediff != null && IsApexMechanoid(p))
                {
                    Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(Props.buffHediff);
                    if (existing != null)
                        p.health.RemoveHediff(existing);
                    p.health.AddHediff(Props.buffHediff);
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

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(parent.pawn.Position, Props.radius);
        }
    }
}
