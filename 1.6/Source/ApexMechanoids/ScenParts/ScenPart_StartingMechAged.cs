using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// A starting mech that is not brand new.
    ///
    /// Vanilla's <c>ScenPart_StartingMech</c> asks the generator for a newborn, so every mech the
    /// scenario hands over reads as zero years old. That is fine for a colony that just gestated
    /// them and wrong for one that walked into a complex full of machines older than the corpses on
    /// the floor. This is the same part with the age taken out of <see cref="MechAgeRules"/> instead.
    ///
    /// Written out rather than subclassed because vanilla keeps its kind and its overseer chance
    /// private, so there is nothing to inherit and change.
    /// </summary>
    public class ScenPart_StartingMechAged : ScenPart
    {
        public PawnKindDef mechKind;

        public float overseenByPlayerPawnChance = 1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref mechKind, "mechKind");
            Scribe_Values.Look(ref overseenByPlayerPawnChance, "overseenByPlayerPawnChance", 1f);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (mechKind == null)
            {
                yield return "no mechKind";
            }
            else if (mechKind.race == null || !mechKind.RaceProps.IsMechanoid)
            {
                yield return mechKind.defName + " is not a mechanoid";
            }
        }

        public override IEnumerable<Thing> PlayerStartingThings()
        {
            if (mechKind == null)
            {
                yield break;
            }

            Pawn mech = PawnGenerator.GeneratePawn(MechAgeRules.RequestFor(mechKind, Faction.OfPlayer));
            if (Rand.Chance(overseenByPlayerPawnChance) && mech.OverseerSubject != null)
            {
                Pawn overseer = (from p in Find.GameInitData.startingAndOptionalPawns.Take(Find.GameInitData.startingPawnCount)
                                 where MechanitorUtility.IsMechanitor(p) && p.mechanitor.CanOverseeSubject(mech)
                                 orderby p.mechanitor.OverseenPawns.Count
                                 select p).RandomElementWithFallback();
                overseer?.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            }
            yield return mech;
        }

        /// <summary>Listed on the scenario page the same way vanilla lists its own starting mechs.</summary>
        public override string Summary(Scenario scen)
        {
            return ScenSummaryList.SummaryWithList(scen, "PlayerStartsWith", ScenPart_StartingThing_Defined.PlayerStartWithIntro);
        }

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag == "PlayerStartsWith" && mechKind != null)
            {
                yield return "Mechanoid".Translate().CapitalizeFirst() + ": " + mechKind.LabelCap;
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() ^ (mechKind?.GetHashCode() ?? 0);
        }
    }
}
