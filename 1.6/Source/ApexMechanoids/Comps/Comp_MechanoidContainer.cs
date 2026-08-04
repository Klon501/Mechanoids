using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ApexMechanoids
{
    public class Comp_MechanoidContainer : CompInteractable, IThingGlower
    {
        public new CompProperties_MechanoidContainer Props => (CompProperties_MechanoidContainer)props;

        public virtual bool IsEmpty
        {
            get
            {
                return isEmpty;
            }
            set
            {
                if (value != isEmpty)
                {
                    isEmpty = value;
                    if (parent.Map != null)
                    {
                        parent.DirtyMapMesh(parent.Map);
                        parent.TryGetComp<CompGlower>()?.UpdateLit(parent.Map);
                    }
                }
            }
        }

        public bool isEmpty = false;
        public PawnKindDef mechKind;

        public override bool HideInteraction => IsEmpty;

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
            (IsEmpty ? Props.emptyGraphic.Graphic : parent.Graphic).Print(layer, parent, 0f);
        }

        public override void OnInteracted(Pawn caster)
        {
            DeployMech(caster);
        }

        public virtual void DeployMech(Pawn mechanitor)
        {
            IntVec3 loc = parent.OccupiedRect().ExpandedBy(1).EdgeCells.Where(c => c.Standable(parent.Map)).MinBy(c => c.DistanceTo(mechanitor.Position));
            if (loc.IsValid)
            {
                ScatterDebrisUtility.ScatterFilthAroundThing(parent, parent.Map, ThingDefOf.Filth_GestationFluid, CompMechGestatorTank.GestationFluidFilthRange);
                Pawn mech = PawnGenerator.GeneratePawn(mechKind, mechanitor.Faction);
                GenSpawn.Spawn(mech, loc, parent.Map);
                mechanitor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                IsEmpty = true;
            }
        }

        public virtual void ChangeMechKindToSpawn(PawnKindDef kindDef = null)
        {
            if (kindDef == null)
            {
                if (Props.mechKindOptions.NullOrEmpty())
                {
                    IsEmpty = true;
                }
                else
                {
                    mechKind = Props.mechKindOptions.RandomElementByWeight((PawnKindDefWeight x) => x.weight).kindDef;
                    IsEmpty = false;
                }
            }
            else
            {
                mechKind = kindDef;
                IsEmpty = false;
            }
        }

        public AcceptanceReport BaseCanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
        {
            return base.CanInteract(activateBy, checkOptionalItems);
        }

        public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
        {
            AcceptanceReport baseReport = BaseCanInteract(activateBy, checkOptionalItems);
            if (!baseReport)
            {
                return baseReport;
            }
            if (IsEmpty)
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
            if (Scribe.mode == LoadSaveMode.Saving && mechKind != null)
            {
                kindDefName = mechKind.defName;
            }
            Scribe_Values.Look(ref kindDefName, "kindDefName", "");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
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
            return !IsEmpty;
        }

        public string BaseCompInspectStringExtra()
        {
            return base.CompInspectStringExtra();
        }

        public override string CompInspectStringExtra()
        {
            string iString = "\n";
            if (IsEmpty)
            {
                iString = "CommandPodEjectFailEmpty".Translate() + iString;
            }
            else
            {
                iString = "CasketContains".Translate() + $" {mechKind.label}" + iString;
            }
            return iString + BaseCompInspectStringExtra();
        }
    }
}
