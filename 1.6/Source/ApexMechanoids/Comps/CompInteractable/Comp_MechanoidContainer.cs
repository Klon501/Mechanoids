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
        public bool ScalesWithPlayerStrength =>
            Props.maxCombatPowerByThreatPoints != null || Props.minCombatPowerByThreatPoints != null;

        /// <summary>
        /// True for a container the colony put down itself, which is the one case that must not come
        /// with an occupant. Reinstalling a found one lands here too, but by then it already has its
        /// mech and the stocking pass has nothing left to do.
        /// </summary>
        private bool PlayerBuilt => parent.Faction != null && parent.Faction.IsPlayer;

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
            if (respawningAfterLoad || !ScalesWithPlayerStrength || mechKind != null || isEmpty)
            {
                return;
            }
            if (Props.stockOnlyWhenNotPlayerBuilt && PlayerBuilt)
            {
                IsEmpty = true;
                return;
            }
            ChangeMechKindToSpawn();
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
                mechKind = null;
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
        /// The option list narrowed to the band of combat power this colony has earned. The cap keeps a
        /// young colony from walking away with a centipede; the floor keeps a rich one from opening a
        /// rare container onto a militor, which the cap alone cannot do because the cheap kinds carry
        /// the heaviest weights and never leave the pool.
        ///
        /// The band is a preference, not a promise. If nothing sits inside it the container still holds
        /// something: whatever is closest to the band rather than nothing at all.
        /// </summary>
        protected List<PawnKindDefWeight> AllowedMechKindOptions()
        {
            List<PawnKindDefWeight> options = CandidateMechKinds();

            if (!ScalesWithPlayerStrength || options.Count == 0)
            {
                return options;
            }

            float points = PlayerStrengthPoints();
            float cap = Props.maxCombatPowerByThreatPoints?.Evaluate(points) ?? float.MaxValue;
            float floor = Props.minCombatPowerByThreatPoints?.Evaluate(points) ?? 0f;

            List<float> combatPowers = options.Select((PawnKindDefWeight x) => x.kindDef.combatPower).ToList();
            return MechContainerStockRules.IndicesWithinBand(combatPowers, floor, cap)
                .Select((int i) => options[i])
                .ToList();
        }

        /// <summary>
        /// Everything this container could hold before the colony's strength is taken into account: the
        /// curated list, plus whatever <c>autoIncludeControllableMechs</c> sweeps up.
        /// </summary>
        private List<PawnKindDefWeight> CandidateMechKinds()
        {
            List<PawnKindDefWeight> options = Props.mechKindOptions
                .Where((PawnKindDefWeight x) => x?.kindDef != null && !Excluded(x.kindDef))
                .ToList();

            if (!Props.autoIncludeControllableMechs)
            {
                return options;
            }

            HashSet<PawnKindDef> curated = new HashSet<PawnKindDef>(options.Select((PawnKindDefWeight x) => x.kindDef));
            foreach (PawnKindDef kindDef in ControllableMechKinds())
            {
                if (curated.Contains(kindDef) || Excluded(kindDef))
                {
                    continue;
                }
                options.Add(new PawnKindDefWeight
                {
                    kindDef = kindDef,
                    weight = Props.autoIncludeWeight
                });
            }
            return options;
        }

        private bool Excluded(PawnKindDef kindDef)
        {
            return Props.excludedMechKinds.Contains(kindDef) || BossKinds.Contains(kindDef);
        }

        /// <summary>
        /// Every mech a colony could have built for itself, from any mod. A gestator recipe is the test
        /// rather than the overseer comp: every mechanoid inherits that comp from the vanilla base, so
        /// it would sweep up escorts and set pieces that were never meant to leave their group, while a
        /// recipe is somebody deciding on purpose that a player may own one. Our own Satellite is the
        /// case in point, and it drops out here without needing to be named.
        ///
        /// Built once. Def lists do not change after startup, and this runs inside cluster generation.
        /// </summary>
        private static IEnumerable<PawnKindDef> ControllableMechKinds()
        {
            if (cachedControllableMechKinds != null)
            {
                return cachedControllableMechKinds;
            }

            HashSet<ThingDef> gestatable = new HashSet<ThingDef>(MechanitorUtility.MechRecipes
                .Select((RecipeDef recipe) => recipe.ProducedThingDef)
                .Where((ThingDef def) => def != null));

            return cachedControllableMechKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where((PawnKindDef kindDef) => kindDef.race != null
                    && kindDef.RaceProps.IsMechanoid
                    && kindDef.combatPower > 0f
                    && gestatable.Contains(kindDef.race)
                    && kindDef.race.HasComp(typeof(CompOverseerSubject)))
                .ToList();
        }

        /// <summary>The bossgroup bosses of every mod, which are nobody's prize for opening a crate.</summary>
        private static HashSet<PawnKindDef> BossKinds =>
            cachedBossKinds ?? (cachedBossKinds = new HashSet<PawnKindDef>(DefDatabase<BossDef>.AllDefsListForReading
                .Select((BossDef boss) => boss.kindDef)
                .Where((PawnKindDef kindDef) => kindDef != null)));

        private static List<PawnKindDef> cachedControllableMechKinds;

        private static HashSet<PawnKindDef> cachedBossKinds;

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
                bool wasEmpty = isEmpty;
                mechKind = kindDefName.NullOrEmpty()
                    ? null
                    : DefDatabase<PawnKindDef>.GetNamed(kindDefName, false);
                if (MechContainerStockRules.Resolve(wasEmpty, mechKind != null) == LoadedOccupancy.Reroll)
                {
                    ChangeMechKindToSpawn();
                }
                isEmpty = wasEmpty;
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

        /// <summary>
        /// What a sealed container gives away about its occupant. A mech the colony walked in there
        /// itself is named; one that was already inside when the container was found is not, because
        /// reading the label off a crate nobody has opened is the whole tension of opening it.
        /// </summary>
        protected string ContentsLine => "APM.MechanoidContainer.ContainsUnknown".Translate();

        public override string CompInspectStringExtra()
        {
            string iString = "\n";
            if (IsEmpty)
            {
                iString = "CommandPodEjectFailEmpty".Translate() + iString;
            }
            else
            {
                iString = ContentsLine + iString;
            }
            return iString + BaseCompInspectStringExtra();
        }
    }
}
