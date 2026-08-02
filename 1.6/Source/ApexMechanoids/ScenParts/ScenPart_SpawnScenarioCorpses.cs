using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// One pawn kind that should be dead by the time the player sees the starting map.
    /// </summary>
    public class ScenarioCorpseTarget : IExposable
    {
        public PawnKindDef pawnKind;
        public RotStage rotStage = RotStage.Dessicated;
        public bool spawnFilthAround = true;
        public int filthCount = 5;
        public ThingDef filthDef;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref pawnKind, "pawnKind");
            Scribe_Values.Look(ref rotStage, "rotStage", RotStage.Dessicated);
            Scribe_Values.Look(ref spawnFilthAround, "spawnFilthAround", defaultValue: true);
            Scribe_Values.Look(ref filthCount, "filthCount", 5);
            Scribe_Defs.Look(ref filthDef, "filthDef");
        }
    }

    /// <summary>
    /// Turns pawns that the starting structure placed into corpses, in place.
    ///
    /// KCSG's own <c>spawnDead</c> on <c>SymbolDef</c> kills the pawn before it is ever put on
    /// the map, so <see cref="Pawn.Kill"/> takes its unspawned branch and the resulting corpse
    /// never goes through normal placement. This part lets KCSG spawn the pawns alive at the
    /// exact cells the layout asks for, then kills them once they are spawned, which is the
    /// path the game actually supports.
    ///
    /// Apparel stays worn on the corpse, so armour remains strippable.
    /// </summary>
    public class ScenPart_SpawnScenarioCorpses : ScenPart
    {
        public List<ScenarioCorpseTarget> targets = new List<ScenarioCorpseTarget>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref targets, "targets", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && targets == null)
            {
                targets = new List<ScenarioCorpseTarget>();
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (targets.NullOrEmpty())
            {
                yield return "no targets configured";
                yield break;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i]?.pawnKind == null)
                {
                    yield return "target " + i + " has no pawnKind";
                }
            }
        }

        public override void PostMapGenerate(Map map)
        {
            // Only the map the player starts on. Later maps generate their own structures and
            // must not have their pawns killed off.
            if (Find.GameInitData == null || targets.NullOrEmpty())
            {
                return;
            }

            List<Pawn> candidates = new List<Pawn>();

            for (int i = 0; i < targets.Count; i++)
            {
                ScenarioCorpseTarget target = targets[i];
                if (target?.pawnKind == null)
                {
                    continue;
                }

                candidates.Clear();
                IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                for (int j = 0; j < spawned.Count; j++)
                {
                    Pawn pawn = spawned[j];
                    if (pawn.kindDef == target.pawnKind && !pawn.Dead && pawn.Faction != Faction.OfPlayer)
                    {
                        candidates.Add(pawn);
                    }
                }

                for (int j = 0; j < candidates.Count; j++)
                {
                    MakeCorpse(candidates[j], map, target);
                }
            }
        }

        private static void MakeCorpse(Pawn pawn, Map map, ScenarioCorpseTarget target)
        {
            IntVec3 cell = pawn.Position;
            DegradeGear(pawn);

            // Null DamageInfo keeps this off the faction goodwill and death-notification paths.
            pawn.Kill(null);

            Corpse corpse = pawn.Corpse;
            if (corpse == null || !corpse.Spawned)
            {
                Log.Warning("[ApexMechanoids] No spawned corpse for " + pawn.KindLabel + " at " + cell + ".");
                return;
            }

            if (target.rotStage != RotStage.Fresh)
            {
                corpse.GetComp<CompRottable>()?.RotImmediately(target.rotStage);
            }

            if (!target.spawnFilthAround)
            {
                return;
            }

            ThingDef filthDef = target.filthDef ?? ThingDefOf.Filth_CorpseBile;
            for (int i = 0; i < target.filthCount; i++)
            {
                if (RCellFinder.TryFindRandomCellNearWith(cell, (IntVec3 c) => c.Walkable(map), map, out IntVec3 filthCell, 1, 3))
                {
                    FilthMaker.TryMakeFilth(filthCell, map, filthDef);
                }
            }
        }

        /// <summary>
        /// Beats up whatever the pawn is carrying so the loot reads as centuries-old salvage
        /// rather than a fresh kit. Mirrors what KCSG did before killing the pawn.
        /// </summary>
        private static void DegradeGear(Pawn pawn)
        {
            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    DegradeThing(worn[i]);
                }
            }

            if (pawn.equipment?.Primary != null)
            {
                DegradeThing(pawn.equipment.Primary);
            }

            if (pawn.inventory == null)
            {
                return;
            }

            // Rottables in a centuries-old inventory would just spam rot messages on arrival.
            ThingOwner held = pawn.inventory.GetDirectlyHeldThings();
            for (int i = held.Count - 1; i >= 0; i--)
            {
                Thing carried = held[i];
                if (carried.TryGetComp<CompRottable>() != null)
                {
                    held.Remove(carried);
                }
            }
        }

        private static void DegradeThing(Thing thing)
        {
            if (thing.def.useHitPoints && thing.MaxHitPoints > 1)
            {
                thing.HitPoints = Rand.RangeInclusive(1, Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * 0.75f)));
            }
        }

        public override string Summary(Scenario scen)
        {
            return def.description;
        }
    }
}
