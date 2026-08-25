using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class Comp_MechanoidContainerControlled : Comp_MechanoidContainer, IThingHolder
    {
        public new CompProperties_MechanoidContainerControlled Props => (CompProperties_MechanoidContainerControlled)props;

        protected ThingOwner innerContainer;

        public override bool IsEmpty
        {
            get
            {
                return isEmpty && !isContaining;
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

        public bool isContaining => innerContainer.Any;

        public CompPowerTrader PowerTraderComp => cachedPowerComp ?? (cachedPowerComp = parent.TryGetComp<CompPowerTrader>());
        private CompPowerTrader cachedPowerComp;

        public bool PowerOn => PowerTraderComp.PowerOn;

        public Comp_MechanoidContainerControlled() : base()
        {
            innerContainer = new ThingOwner<Thing>(this, LookMode.Deep, removeContentsIfDestroyed: false);
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public AcceptanceReport CanAcceptMech(Pawn mech = null)
        {
            if (!PowerOn)
            {
                return "NoPower".Translate().CapitalizeFirst();
            }
            if (!IsEmpty)
            {
                return "Occupied".Translate();
            }
            if (OnCooldown)
            {
                return Props.onCooldownString + " (" + "DurationLeft".Translate(cooldownTicks.ToStringTicksToPeriod()) + ")";
            }
            if (mech != null && !CanBeSentInside(mech))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Whether this mech may be walked into the container. Whether it currently has an overseer,
        /// and whether anyone has the bandwidth for it, are deliberately not part of the answer:
        /// a mech nobody is holding is the one the player most wants put away.
        /// </summary>
        public static bool CanBeSentInside(Pawn mech)
        {
            return MechContainerAccessRules.CanBeSentInside(
                playerFaction: mech.Faction == Faction.OfPlayer && mech.RaceProps.IsMechanoid,
                everControllable: MechanitorUtility.EverControllable(mech),
                downed: mech.Downed,
                dead: mech.Dead);
        }

        public void TryAcceptPawn(Pawn mech)
        {
            if ((bool)CanAcceptMech(mech))
            {
                MechanitorUtility.ForceDisconnectMechFromOverseer(mech);
                bool num = mech.DeSpawnOrDeselect();
                if (innerContainer.TryAddOrTransfer(mech))
                {
                    IsEmpty = false;
                }
                if (num)
                {
                    Find.Selector.Select(mech, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == CompPowerTrader.PowerTurnedOffSignal && isContaining)
            {
                Pawn mech = innerContainer.First() as Pawn;
                if (!innerContainer.TryDrop(mech, parent.PositionHeld, parent.MapHeld, ThingPlaceMode.Near, out _))
                {
                    if (!RCellFinder.TryFindRandomCellNearWith(parent.PositionHeld, (IntVec3 c) => c.Standable(parent.MapHeld), parent.MapHeld, out var result, 1))
                    {
                        Debug.LogError($"Could not drop {mech.ThingID}!");
                    }
                    GenSpawn.Spawn(innerContainer.Take(mech), result, parent.MapHeld);
                }
                IsEmpty = true;
            }
        }

        public override void DeployMech(Pawn mechanitor)
        {
            IntVec3 loc = parent.OccupiedRect().ExpandedBy(1).EdgeCells.Where(c => c.Standable(parent.Map)).MinBy(c => c.DistanceTo(mechanitor.Position));
            if (loc.IsValid)
            {
                ScatterDebrisUtility.ScatterFilthAroundThing(parent, parent.Map, ThingDefOf.Filth_GestationFluid, CompMechGestatorTank.GestationFluidFilthRange);
                Pawn mech = null;
                if (mechKind != null)
                {
                    mech = PawnGenerator.GeneratePawn(MechAgeRules.RequestFor(mechKind, mechanitor.Faction));
                    GenSpawn.Spawn(mech, loc, parent.Map);
                    // The kind was the container's one sealed occupant, not a recipe it keeps. Leaving
                    // it set would hand out a fresh mech every time the container was refilled and
                    // opened again.
                    mechKind = null;
                }
                else if (isContaining)
                {
                    mech = innerContainer.First() as Pawn;
                    if (!innerContainer.TryDrop(mech, loc, parent.MapHeld, ThingPlaceMode.Direct, out _))
                    {
                        if (!RCellFinder.TryFindRandomCellNearWith(parent.PositionHeld, (IntVec3 c) => c.Standable(parent.MapHeld), parent.MapHeld, out var result, 1))
                        {
                            Debug.LogError($"Could not drop {mech.ThingID}!");
                        }
                        GenSpawn.Spawn(innerContainer.Take(mech), result, parent.MapHeld);
                    }
                }
                TakeControlIfPossible(mechanitor, mech);
                IsEmpty = true;
            }
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
            // Bandwidth is deliberately not tested here. It decides how the container opens, not
            // whether it opens; see Comp_MechanoidContainer.TakeControlIfPossible.
            if (activateBy != null && !MechanitorUtility.IsMechanitor(activateBy))
            {
                return "NotAMechanitor".Translate();
            }
            return true;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (innerContainer.Count > 0 && (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize))
            {
                if (mode != DestroyMode.Deconstruct)
                {
                    List<Pawn> list = new List<Pawn>();
                    foreach (Thing item2 in (IEnumerable<Thing>)innerContainer)
                    {
                        if (item2 is Pawn item)
                        {
                            list.Add(item);
                        }
                    }
                    foreach (Pawn item3 in list)
                    {
                        HealthUtility.DamageUntilDowned(item3);
                    }
                }
                innerContainer.TryDropAll(parent.PositionHeld, previousMap, ThingPlaceMode.Near);
            }
            innerContainer.ClearAndDestroyContents();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (HideInteraction)
            {
                AcceptanceReport acceptanceReport = CanAcceptMech();
                yield return new Command_Action
                {
                    action = delegate
                    {
                        TargetingParameters targetingParameters = TargetingParameters.ForPawns();
                        targetingParameters.mapBoundsContractedBy = 1;
                        targetingParameters.validator = (TargetInfo t) => t.Thing is Pawn pawn && CanBeSentInside(pawn) && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving) && pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly);
                        Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
                        {
                            Pawn chosen = target.Thing as Pawn;
                            // Forcibly, which is the point of it: a mech that has stopped taking
                            // orders is exactly the one worth putting away, and it will not walk
                            // anywhere while the state is still running.
                            chosen.MentalState?.RecoverFromState();
                            chosen.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Props.enterJobDef, parent), JobTag.Misc);
                        }, delegate
                        {
                            Widgets.MouseAttachedLabel("APM.MechanoidContainer.Gizmo.ChooseMech.MouseLabel".Translate());
                        });
                    },
                    defaultLabel = Props.ChooseMechLabel,
                    defaultDesc = Props.ChooseMechDesc,
                    icon = CompTransporter.LoadCommandTex,
                    disabled = !acceptanceReport.Accepted,
                    disabledReason = acceptanceReport.Reason.CapitalizeFirst()
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.removeContentsIfDestroyed)
            {
                innerContainer.removeContentsIfDestroyed = false;
            }
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
                if (mechKind != null)
                {
                    iString = ContentsLine + iString;
                }
                else if (isContaining)
                {
                    iString = "CasketContains".Translate() + $" {innerContainer.First().Label}" + iString;
                }
            }
            return iString + BaseCompInspectStringExtra();
        }
    }
}
