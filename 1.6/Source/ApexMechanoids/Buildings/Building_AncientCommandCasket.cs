using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class Building_AncientCommandCasket : Building_Casket
    {

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // Only on a fresh placement. A casket coming back from a save already carries whatever it
            // was saved with, and an emptied one must stay empty rather than generate a second body.
            if (!respawningAfterLoad)
            {
                FillBuilding();
            }
        }

        /// <summary>
        /// <c>base.PostMake()</c>, not <c>base.PostPostMake()</c>. Those are two different methods:
        /// PostMake is what hands the thing its ID and its starting hit points and initialises its
        /// comps, and PostPostMake only forwards to the comps that are not there yet. Calling the
        /// second in place of the first is why a casket used to come out with -1 hit points; it also
        /// left it with no thing ID at all, which went unnoticed only because nothing placed one
        /// until the domain layouts did.
        /// </summary>
        public override void PostMake()
        {
            base.PostMake();
            FillBuilding();
        }

        public override bool Accepts(Thing thing)
        {
            if (!base.Accepts(thing))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Puts the body the def names inside. Which pawn kind that is, how far gone it should be and
        /// which faction it belonged to are all read off
        /// <see cref="DefModExtension_CasketOccupant"/>, so a second ancient casket only needs a def
        /// and never a second class. A def with no such extension is left empty.
        /// </summary>
        private void FillBuilding()
        {
            if (HasAnyContents)
            {
                return;
            }

            DefModExtension_CasketOccupant occupant = def.GetModExtension<DefModExtension_CasketOccupant>();
            if (occupant?.pawnKind == null)
            {
                return;
            }

            Faction faction = occupant.faction != null
                ? Find.FactionManager.FirstFactionOfDef(occupant.faction)
                : Find.FactionManager.OfAncientsHostile;

            PawnGenerationRequest request = new PawnGenerationRequest(occupant.pawnKind, faction,
                forceGenerateNewPawn: true,
                allowDead: true, allowDowned: false, canGeneratePawnRelations: false, allowPregnant: false, allowFood: false, allowAddictions: true, certainlyBeenInCryptosleep: true, forceNoIdeo: false, forceNoBackstory: false,
                forbidAnyTitle: false, forceDead: true,
                developmentalStages: DevelopmentalStage.Adult
                );
            Pawn pawnToSpawn = PawnGenerator.GeneratePawn(request);

            Corpse corpse = pawnToSpawn.Corpse;
            if (corpse == null)
            {
                return;
            }

            // Centuries sealed in. Done before the corpse goes in so it is already at the right stage
            // the first time the player opens the lid, rather than rotting once they do.
            if (occupant.rotStage != RotStage.Fresh)
            {
                corpse.GetComp<CompRottable>()?.RotImmediately(occupant.rotStage);
            }

            innerContainer.TryAddOrTransfer(corpse);
        }

        public override void Open()
        {
            if (HasAnyContents)
            {
                EjectContents();
                if (!openedSignal.NullOrEmpty())
                {
                    Find.SignalManager.SendSignal(new Signal(openedSignal, this.Named("SUBJECT")));
                }
                DirtyMapMesh(base.Map);
                Utils.ReplaceBuilding(this, ApexDefsOf.APM_MechCommandCasket);
            }
        }


        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                EjectContents();
            }
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

   




    }
}
