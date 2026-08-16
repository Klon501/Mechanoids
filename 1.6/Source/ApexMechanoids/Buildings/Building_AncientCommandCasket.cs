using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class Building_AncientCommandCasket : Building_Casket
    {

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            FillBuilding();
        }

        public override void PostMake()
        {
            base.PostPostMake();
            FillBuilding();
            if (HitPoints != def.BaseMaxHitPoints)  // this is needed for resons I don't understand. Spawns with -1 hitpoint otherwise
            {
                HitPoints = def.BaseMaxHitPoints;
            }
        }

        public override bool Accepts(Thing thing)
        {
            if (!base.Accepts(thing))
            {
                return false;
            }
            return true;
        }

        private void FillBuilding()
        {
            if (!HasAnyContents)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.Mechanitor_Basic, Find.FactionManager.OfAncientsHostile,
                    forceGenerateNewPawn: true,
                    allowDead: true, allowDowned: false, canGeneratePawnRelations: false, allowPregnant: false, allowFood: false, allowAddictions: true, certainlyBeenInCryptosleep: true, forceNoIdeo: false, forceNoBackstory: false,
                    forbidAnyTitle: false, forceDead: true,
                    developmentalStages: DevelopmentalStage.Adult
                    );
                Pawn pawnToSpawn = PawnGenerator.GeneratePawn(request);

                innerContainer.TryAddOrTransfer(pawnToSpawn.Corpse);
            }
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
