using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class Verb_ShootHazeBeam : Verb_ShootBeam
    {
        private static readonly float ToxExplosionRadius = 3.5f;
        private static readonly int ToxGasAmount = 8;

        public override bool TryCastShot()
        {
            bool result = base.TryCastShot();
            if (!result)
                return result;

            ShootLine resultingLine;
            if (!TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine))
                return result;

            IntVec3 hitCell = resultingLine.Dest;
            if (!hitCell.InBounds(caster.Map))
                return result;

            SpawnToxEffects(hitCell);
            return result;
        }

        private void SpawnToxEffects(IntVec3 center)
        {
            List<Pawn> affected = new List<Pawn>();

            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, ToxExplosionRadius, useCenter: true))
            {
                if (!c.InBounds(caster.Map))
                    continue;

                if (Rand.Chance(0.35f))
                    GasUtility.AddGas(c, caster.Map, GasType.ToxGas, ToxGasAmount);

                List<Thing> things = c.GetThingList(caster.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn == caster || pawn.RaceProps.IsMechanoid)
                        continue;
                    if (affected.Contains(pawn))
                        continue;
                    affected.Add(pawn);
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, Rand.Range(0.05f, 0.12f));
                }
            }
        }
    }
}
