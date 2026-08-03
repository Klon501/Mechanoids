using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ApexMechanoids
{
    public class Comp_MechanoidContainer : CompInteractable, IThingGlower
    {
        public new CompProperties_MechanoidContainer Props => (CompProperties_MechanoidContainer)props;

        public bool isEmpty = false;
        public PawnKindDef mechKind;

        public override bool HideInteraction => isEmpty;

        public Comp_MechanoidContainer()
        {
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            ChangeMechKindToSpawn();
            parent.overrideGraphicIndex = 0;
        }

        public override bool DontDrawParent()
        {
            return true;
        }

        public override void PostPrintOnto(SectionLayer layer)
        {
            (isEmpty ? Props.emptyGraphic.Graphic : parent.Graphic).Print(layer, parent, 0f);
        }

        public override void OnInteracted(Pawn caster)
        {
            DeployMech(caster);
        }

        public void DeployMech(Pawn mechanitor)
        {
            IntVec3 loc = parent.OccupiedRect().ExpandedBy(1).EdgeCells.Where(c => c.Standable(parent.Map)).MinBy(c => c.DistanceTo(mechanitor.Position));
            if (loc.IsValid)
            {
                ScatterDebrisUtility.ScatterFilthAroundThing(parent, parent.Map, ThingDefOf.Filth_GestationFluid, CompMechGestatorTank.GestationFluidFilthRange);
                Pawn mech = PawnGenerator.GeneratePawn(mechKind, mechanitor.Faction);
                GenSpawn.Spawn(mech, loc, parent.Map);
                mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                isEmpty = true;
                parent.DirtyMapMesh(parent.Map);
                parent.TryGetComp<CompGlower>()?.UpdateLit(parent.Map);
            }
        }

        public void ChangeMechKindToSpawn(PawnKindDef kindDef = null)
        {
            if (kindDef == null)
            {
                mechKind = Props.mechKindOptions.RandomElementByWeight((PawnKindDefWeight x) => x.weight).kindDef;
            }
            else
            {
                mechKind = kindDef;
            }
        }

        public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
        {
            AcceptanceReport baseReport = base.CanInteract(activateBy, checkOptionalItems);
            if (!baseReport)
            {
                return baseReport;
            }
            if (isEmpty)
            {
                return "CommandPodEjectFailEmpty".Translate();
            }
            if (activateBy != null)
            {
                if (!MechanitorUtility.IsMechanitor(activateBy))
                {
                    return "NotAMechanitor".Translate();
                }
                if (activateBy.mechanitor.TotalBandwidth < activateBy.mechanitor.UsedBandwidth + mechKind.race.GetStatValueAbstract(StatDefOf.BandwidthCost))
                {
                    return "NotEnoughBandwidth".Translate();
                }
            }
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        DeployMech(PawnsFinder.AllMaps_FreeColonists.First(p => MechanitorUtility.IsMechanitor(p)));
                    },
                    defaultLabel = "Dev: Activate",
                    defaultDesc = $"Activate with first mechanitor available.",
                    disabled = !MechanitorUtility.AnyMechanitorInPlayerFaction(),
                    disabledReason = "No mechanitors"
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isEmpty, "isEmpty", defaultValue: false);
            string kindDefName = "";
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                kindDefName = mechKind.defName;
            }
            Scribe_Values.Look(ref kindDefName, "kindDefName", "");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (kindDefName.NullOrEmpty())
                {
                    ChangeMechKindToSpawn();
                }
                else
                {
                    PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed(kindDefName, false);
                    if (kindDef == null)
                    {
                        ChangeMechKindToSpawn();
                    }
                    else
                    {
                        ChangeMechKindToSpawn(kindDef);
                    }
                }
            }
        }

        public bool ShouldBeLitNow()
        {
            return !isEmpty;
        }

        public override string CompInspectStringExtra()
        {
            string iString = "\n";
            if (isEmpty)
            {
                iString = "CommandPodEjectFailEmpty".Translate() + iString;
            }
            else
            {
                iString = "CasketContains".Translate() + $" {mechKind.label}" + iString;
            }
            return iString + base.CompInspectStringExtra();
        }
    }
}
