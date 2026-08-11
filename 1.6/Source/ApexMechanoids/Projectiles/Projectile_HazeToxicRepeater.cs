using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class Projectile_HazeToxicRepeater : Beam
    {
        private const float DirectToxicSeverity = 0.015f;

        private static ThingDef toxicPuddleDef;

        private static ThingDef ToxicPuddleDef
        {
            get
            {
                if (toxicPuddleDef == null)
                {
                    toxicPuddleDef = DefDatabase<ThingDef>.GetNamedSilentFail("APM_HazeToxicPuddle");
                }

                return toxicPuddleDef;
            }
        }

        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = hitThing?.Position ?? Position;

            base.Impact(hitThing, blockedByShield);

            if (blockedByShield || map == null || !cell.InBounds(map))
            {
                return;
            }

            ApplyDirectToxicBuildup(hitThing);
            TryPlaceToxicPuddle(cell, map);
        }

        private static void ApplyDirectToxicBuildup(Thing hitThing)
        {
            Pawn pawn = hitThing as Pawn;
            if (pawn == null || pawn.Dead || !(pawn.RaceProps?.IsFlesh ?? false))
            {
                return;
            }

            float resistanceFactor = Mathf.Clamp01(1f - pawn.GetStatValue(StatDefOf.ToxicResistance));
            if (resistanceFactor <= 0f)
            {
                return;
            }

            float bodySizeFactor = 1f / Mathf.Max(pawn.BodySize, 0.25f);
            HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, DirectToxicSeverity * resistanceFactor * bodySizeFactor);
        }

        private static void TryPlaceToxicPuddle(IntVec3 cell, Map map)
        {
            ThingDef puddleDef = ToxicPuddleDef;
            if (puddleDef == null)
            {
                return;
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing is HazeToxicPuddle existingPuddle)
                {
                    existingPuddle.Refresh();
                    return;
                }
            }

            GenSpawn.Spawn(puddleDef, cell, map);
        }
    }

    public class HazeToxicPuddle : Filth
    {
        private const int LifetimeTicks = 900;
        private const int ToxicTickInterval = 120;
        private const float ToxicSeverity = 0.025f;

        private static readonly List<Pawn> tmpPawnsToAffect = new List<Pawn>();

        private int ticksLeft = LifetimeTicks;

        public void Refresh()
        {
            ticksLeft = LifetimeTicks;
        }

        public override void Tick()
        {
            base.Tick();

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            if (this.IsHashIntervalTick(ToxicTickInterval))
            {
                ApplyToxicBuildup();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, nameof(ticksLeft), LifetimeTicks);
        }

        private void ApplyToxicBuildup()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            tmpPawnsToAffect.Clear();
            List<Thing> things = Position.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn pawn && ShouldAffectPawn(pawn, map))
                {
                    tmpPawnsToAffect.Add(pawn);
                }
            }

            for (int i = 0; i < tmpPawnsToAffect.Count; i++)
            {
                Pawn pawn = tmpPawnsToAffect[i];
                if (ShouldAffectPawn(pawn, map))
                {
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, ToxicSeverity);
                }
            }

            tmpPawnsToAffect.Clear();
        }

        private bool ShouldAffectPawn(Pawn pawn, Map map)
        {
            return pawn != null
                && !pawn.Dead
                && pawn.Spawned
                && pawn.MapHeld == map
                && pawn.Position == Position
                && !(pawn.RaceProps?.IsMechanoid ?? false);
        }
    }
}
