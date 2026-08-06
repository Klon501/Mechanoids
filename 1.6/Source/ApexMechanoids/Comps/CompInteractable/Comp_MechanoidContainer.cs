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

        /// <summary>
        /// A scaled container cannot pick its occupant at make time: the roll needs the colony it is
        /// about to land next to, and mech cluster buildings are made well before they are spawned.
        /// </summary>
        public bool ScalesWithPlayerStrength => Props.maxCombatPowerByThreatPoints != null;

        public override void PostPostMake()
        {
            base.PostPostMake();
            if (!ScalesWithPlayerStrength)
            {
                ChangeMechKindToSpawn();
            }
            parent.overrideGraphicIndex = 0;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && ScalesWithPlayerStrength && mechKind == null && !isEmpty)
            {
                ChangeMechKindToSpawn();
            }
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
            if (kindDef != null)
            {
                mechKind = kindDef;
                IsEmpty = false;
                return;
            }

            List<PawnKindDefWeight> options = AllowedMechKindOptions();
            if (options.NullOrEmpty())
            {
                IsEmpty = true;
                return;
            }

            mechKind = options.RandomElementByWeight((PawnKindDefWeight x) => x.weight).kindDef;
            IsEmpty = false;
        }

        /// <summary>
        /// The option list with anything the colony is not yet strong enough to be handed filtered out.
        /// If the colony is below even the weakest option the container still holds something: it drops
        /// to the cheapest kinds rather than opening empty.
        /// </summary>
        protected List<PawnKindDefWeight> AllowedMechKindOptions()
        {
            List<PawnKindDefWeight> options = Props.mechKindOptions
                .Where((PawnKindDefWeight x) => x?.kindDef != null)
                .ToList();

            if (!ScalesWithPlayerStrength || options.Count == 0)
            {
                return options;
            }

            float cap = Props.maxCombatPowerByThreatPoints.Evaluate(PlayerStrengthPoints());
            List<PawnKindDefWeight> withinCap = options
                .Where((PawnKindDefWeight x) => x.kindDef.combatPower <= cap)
                .ToList();
            if (withinCap.Count > 0)
            {
                return withinCap;
            }

            float weakest = options.Min((PawnKindDefWeight x) => x.kindDef.combatPower);
            return options.Where((PawnKindDefWeight x) => x.kindDef.combatPower <= weakest).ToList();
        }

        /// <summary>
        /// Threat points are the game's own read on how strong the colony is, so they are what the
        /// occupant scales against. Measured on a player home map: a container generated for a pocket
        /// map or a quest site would otherwise read as a colony with nothing in it.
        /// </summary>
        private float PlayerStrengthPoints()
        {
            // Loading a save re-rolls containers whose kind no longer resolves, and that runs before
            // the game has maps or a storyteller. Read as "no colony yet" rather than throwing.
            if (Current.Game == null || Find.Storyteller == null)
            {
                return 0f;
            }

            Map map = parent.MapHeld;
            if (map == null || !map.IsPlayerHome)
            {
                map = Find.AnyPlayerHomeMap ?? map;
            }
            return map == null ? 0f : StorytellerUtility.DefaultThreatPointsNow(map);
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
