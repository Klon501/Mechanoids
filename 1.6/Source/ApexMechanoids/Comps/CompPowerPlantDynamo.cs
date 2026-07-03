using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    public class CompPowerPlantDynamo : CompPowerPlant, IThingHolder
    {
        public new CompProperties_PowerPlantDynamo Props => (CompProperties_PowerPlantDynamo)props;

        private ThingOwner<Thing> innerContainer;

        GraphicData overrideGraphicData = new GraphicData();

        public Pawn Dynamo => innerContainer.InnerListForReading.FirstOrDefault() as Pawn;

        private bool wasDrafted;
        private bool isInitialGraphic = true;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public CompPowerPlantDynamo()
        {
            innerContainer = new ThingOwner<Thing>(this, LookMode.Deep, removeContentsIfDestroyed: false);
        }

        public void UpdateGraphic()
        {
            if (Dynamo?.ageTracker?.CurLifeStageIndex > 0)
            {
                overrideGraphicData.CopyFrom(parent.def.graphicData);
                string overrideTexPath = Props.overrideAncientTexPath;
                if (!overrideTexPath.NullOrEmpty())
                {
                    overrideGraphicData.texPath = overrideTexPath;
                }
                AccessTools.Field(typeof(Thing), "graphicInt").SetValue(parent, overrideGraphicData.GraphicColoredFor(parent));
                parent.DirtyMapMesh(parent.Map);
            }
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (dinfo.Instigator?.Faction?.HostileTo(parent.Faction) ?? false)
            {
                parent.Destroy();
            }
        }

        public override void PostDraw()
        {
            if (isInitialGraphic)
            {
                UpdateGraphic();
                isInitialGraphic = false;
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            SwithcToMobileMode(previousMap);
            base.PostDestroy(mode, previousMap);
        }

        public void SwithcToPowerPlantMode(Pawn dynamo)
        {
            if (dynamo.drafter != null)
            {
                wasDrafted = dynamo.drafter.Drafted;
            }
            CompRefuelable compRefuelableDynamo = dynamo.GetComp<CompRefuelable>();
            CompRefuelable compRefuelablePlant = parent.GetComp<CompRefuelable>();
            if (compRefuelableDynamo != null && compRefuelablePlant != null)
            {
                compRefuelablePlant.fuel = compRefuelableDynamo.fuel;
            }
            dynamo.DeSpawn();
            innerContainer.TryAdd(dynamo);
        }

        public Pawn SwithcToMobileMode(Map map)
        {
            if (Dynamo == null)
            {
                return null;
            }
            if (!innerContainer.TryDrop(Dynamo, parent.PositionHeld, map, ThingPlaceMode.Near, out var lastResultingThing))
            {
                if (!RCellFinder.TryFindRandomCellNearWith(parent.PositionHeld, (IntVec3 c) => c.Standable(map), map, out var result, 1))
                {
                    Debug.LogError("Could not drop Dynamo!");
                    return null;
                }
                lastResultingThing = GenSpawn.Spawn(innerContainer.Take(Dynamo), result, map);
            }
            if (lastResultingThing is Corpse corpse)
            {
                return corpse.InnerPawn;
            }
            Pawn dynamo = (Pawn)lastResultingThing;
            dynamo.stances.stunner.StunFor(60, Dynamo, addBattleLog: false, showMote: false);
            if (dynamo.drafter != null)
            {
                dynamo.drafter.Drafted = wasDrafted;
            }
            CompRefuelable compRefuelableDynamo = dynamo.GetComp<CompRefuelable>();
            CompRefuelable compRefuelablePlant = parent.GetComp<CompRefuelable>();
            if (compRefuelableDynamo != null && compRefuelablePlant != null)
            {
                compRefuelableDynamo.fuel = compRefuelablePlant.fuel;
            }
            compRefuelablePlant.fuel = 0;
            return dynamo;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                action = delegate
                {
                    parent.Destroy();
                },
                defaultLabel = "APM.Dynamo.Command.SwithcToMobileMode.Label".Translate(),
                defaultDesc = "APM.Dynamo.Command.SwithcToMobileMode.Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DropCarriedPawn"),
                Order = 30
            };
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref wasDrafted, "wasDrafted", defaultValue: false);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.removeContentsIfDestroyed)
            {
                innerContainer.removeContentsIfDestroyed = false;
            }
        }
    }
}
