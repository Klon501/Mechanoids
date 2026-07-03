using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    public class Building_RepairStation : Building_Enterable, IThingHolderWithDrawnPawn
    {
        private CompPowerTrader cachedPowerComp;
        private CompRepairStation cachedProps;
        private ArmsAnimation armsAnim;
        private PlatformAnimation platformAnim;
        private Graphic topGraphic;
        private bool autoRepairEnabled;
        private int autoRepairIntervalTicks = 2500;
        private int autoRepairTimer;
        private Effecter progressBar;
        private Effecter mechRepairEffecter;
        private float totalHpToHeal;
        private float hpHealedSoFar;
        private static readonly int[] IntervalOptions = new int[] { 1500, 2500, 5000, 10000 };
        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public static readonly CachedTexture InsertPawnIcon = new CachedTexture("UI/Gizmos/InsertPawn");

        public CompRepairStation Config
        {
            get
            {
                if (cachedProps == null) cachedProps = GetComp<CompRepairStation>();
                return cachedProps;
            }
        }

        private CompPowerTrader PowerTrader
        {
            get
            {
                if (cachedPowerComp == null) cachedPowerComp = this.TryGetComp<CompPowerTrader>();
                return cachedPowerComp;
            }
        }

        public Pawn ContainedMech => innerContainer.FirstOrDefault() as Pawn;
        public bool PowerOn => PowerTrader != null && PowerTrader.PowerOn;
        public bool AutoRepairEnabled => autoRepairEnabled;
        public float HeldPawnDrawPos_Y => DrawPos.y + 0.04f;
        public float HeldPawnBodyAngle => this.def.rotatable ? this.Rotation.Opposite.AsAngle : this.Rotation.AsAngle;
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        public override Vector3 PawnDrawOffset => GetMechPositionOffset();

        private Vector3 GetMechPositionOffset()
        {
            switch (Rotation.AsInt)
            {
                case 0:
                    return Config.MechPositionOffsetNorth;
                case 1:
                    return Config.MechPositionOffsetEast;
                case 2:
                    return Config.MechPositionOffsetSouth;
                case 3:
                    return Config.MechPositionOffsetWest;
                default:
                    return Vector3.zero;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (Config.ArmsAnimation != null) armsAnim = new ArmsAnimation(Config.ArmsAnimation);
                if (Config.PlatformAnimation != null) platformAnim = new PlatformAnimation(Config.PlatformAnimation);
            });
            if (autoRepairTimer <= 0) autoRepairTimer = autoRepairIntervalTicks;
        }

        public override void Tick()
        {
            base.Tick();
            if (PowerTrader != null)
            {
                PowerTrader.PowerOutput = (base.Working && ContainedMech != null)
                    ? -Config.ActivePowerConsumption
                    : -PowerTrader.Props.basePowerConsumption;
            }
            if (base.Working && ContainedMech != null)
            {
                if (PowerOn && !this.IsBrokenDown())
                {
                    DoRepairTick(ContainedMech);
                    armsAnim?.Update(true);
                    platformAnim?.Update(true);
                    if (mechRepairEffecter == null) mechRepairEffecter = EffecterDefOf.MechRepairing.Spawn(this, Map, PawnDrawOffset);
                    mechRepairEffecter.EffectTick(this, this);
                    if (progressBar == null) progressBar = EffecterDefOf.ProgressBar.Spawn();
                    progressBar.EffectTick(this, TargetInfo.Invalid);
                    MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBar.children[0]).mote;
                    if (mote != null)
                    {
                        mote.progress = totalHpToHeal > 0f ? hpHealedSoFar / totalHpToHeal : 1f;
                        mote.offsetZ = -0.8f;
                    }
                }
                else
                {
                    if (mechRepairEffecter != null)
                    {
                        mechRepairEffecter.Cleanup();
                        mechRepairEffecter = null;
                    }
                    if (progressBar != null)
                    {
                        progressBar.Cleanup();
                        progressBar = null;
                    }
                }
            }
            else
            {
                armsAnim?.Update(false);
                platformAnim?.Update(false);
                if (mechRepairEffecter != null)
                {
                    mechRepairEffecter.Cleanup();
                    mechRepairEffecter = null;
                }
                if (progressBar != null)
                {
                    progressBar.Cleanup();
                    progressBar = null;
                }

                if (autoRepairEnabled && selectedPawn == null && PowerOn)
                {
                    autoRepairTimer--;
                    if (autoRepairTimer <= 0)
                    {
                        TryFindAutoRepairCandidate();
                        autoRepairTimer = autoRepairIntervalTicks;
                    }
                }
            }
        }

        private void TryFindAutoRepairCandidate()
        {
            Pawn bestCandidate = Map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.IsMechanoid)
                .Where(p => !p.Drafted)
                .Where(p => p.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
                .Where(p => CanAcceptPawn(p).Accepted)
                .Where(p => p.CurJobDef != JobDefOf.EnterBuilding)
                .OrderBy(p => p.health.summaryHealth.SummaryHealthPercent)
                .FirstOrDefault();

            if (bestCandidate != null)
            {
                SelectPawn(bestCandidate);
                Messages.Message("APM_AutoRepairOrdered".Translate(bestCandidate.LabelShort), bestCandidate, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void DoRepairTick(Pawn mech)
        {
            if (mech.health.hediffSet.GetMissingPartsCommonAncestors().Any())
            {
                var part = mech.health.hediffSet.GetMissingPartsCommonAncestors().First();
                mech.health.RestorePart(part.Part);
            }
            float hpBudget = Config.HealHpPerTick;
            List<Hediff> injuries = mech.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury)
                .ToList();

            foreach (Hediff injury in injuries)
            {
                if (hpBudget <= 0f) break;
                float amount = Mathf.Ceil(Mathf.Min(injury.Severity, hpBudget));
                injury.Heal(amount);
                hpHealedSoFar += amount;
                hpBudget -= amount;

            }
            if (hpHealedSoFar >= totalHpToHeal && !mech.health.hediffSet.GetMissingPartsCommonAncestors().Any())
            {
                Messages.Message("APM_MechRepaired".Translate(mech.LabelShort), mech, MessageTypeDefOf.PositiveEvent);
                EjectContents();
            }
        }

        public override AcceptanceReport CanAcceptPawn(Pawn p)
        {
            if (!p.RaceProps.IsMechanoid) return "APM_NotMechanoid".Translate();
            if (p.Faction != Faction.OfPlayer) return "APM_NotPlayerFaction".Translate();

            float size = p.BodySize;
            if (size < Config.MinMechBodySize || size > Config.MaxMechBodySize)
                return "APM_WrongSize".Translate(Config.MinMechBodySize + "-" + Config.MaxMechBodySize);

            if (ContainedMech != null) return "Occupied".Translate();
            if (selectedPawn != null && selectedPawn != p) return "Occupied".Translate();
            if (!PowerOn) return "NoPower".Translate();

            bool damaged = p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury) || p.health.hediffSet.GetMissingPartsCommonAncestors().Any();
            if (!damaged) return "APM_FullHealth".Translate();

            return true;
        }

        public override void TryAcceptPawn(Pawn p)
        {
            if ((bool)CanAcceptPawn(p))
            {
                selectedPawn = p;
                bool deSpawned = p.DeSpawnOrDeselect();

                if (innerContainer.TryAddOrTransfer(p))
                {
                    startTick = Find.TickManager.TicksGame;
                    totalHpToHeal = p.health.hediffSet.hediffs.Where(h => h is Hediff_Injury).Sum(h => h.Severity);
                    hpHealedSoFar = 0f;
                }

                if (deSpawned) Find.Selector.Select(p, false, false);
            }
        }

        public void EjectContents()
        {
            innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
            selectedPawn = null;
            startTick = -1;
            totalHpToHeal = 0f;
            hpHealedSoFar = 0f;
            SoundDefOf.Building_Complete.PlayOneShot(SoundInfo.InMap(this));
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            armsAnim?.Draw(drawLoc, Rotation);
            platformAnim?.Draw(drawLoc, Rotation);
            if (Config.TopGraphic != null)
            {
                if (topGraphic == null)
                {
                    topGraphic = Config.TopGraphic.Graphic;
                }
                Vector3 loc = new Vector3(drawLoc.x, AltitudeLayer.BuildingOnTop.AltitudeFor(), drawLoc.z);
                topGraphic.Draw(loc, Rotation, this);
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            if (ContainedMech != null)
            {
                Vector3 elevated = drawLoc;
                ContainedMech.Drawer.renderer.DynamicDrawPhaseAt(phase, elevated + PawnDrawOffset, null, neverAimWeapon: true);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;

            if (Prefs.DevMode && Config.ArmsAnimation != null && Config.ArmsAnimation.arms.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dev: Adjust offsets",
                    action = () =>
                    {
                        Find.WindowStack.Add(new Dialog_OffsetEditor("Repair Station Offsets", BuildDialogData(), ExportXml));
                    }
                };
            }

            if (base.Working)
            {
                yield return new Command_Action
                {
                    defaultLabel = "APM_CommandCancelRepair".Translate(),
                    defaultDesc = "APM_CommandCancelRepairDesc".Translate(),
                    icon = CancelIcon,
                    action = EjectContents,
                    activateSound = SoundDefOf.Designate_Cancel
                };
            }
            else if (selectedPawn == null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "InsertPerson".Translate() + "...",
                    icon = InsertPawnIcon.Texture,
                    action = () =>
                    {
                        List<FloatMenuOption> opts = new List<FloatMenuOption>();
                        foreach (Pawn p in Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.IsMechanoid && p.Faction == Faction.OfPlayer))
                        {
                            AcceptanceReport report = CanAcceptPawn(p);
                            if (report.Accepted) opts.Add(new FloatMenuOption(p.LabelCap, () => SelectPawn(p), p, Color.white));
                        }
                        if (!opts.Any()) opts.Add(new FloatMenuOption("NoViablePawns".Translate(), null));
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                };
                yield return new Command_Toggle
                {
                    defaultLabel = "APM_Gizmo_AutoRepair".Translate(),
                    defaultDesc = "APM_Gizmo_AutoRepair_Desc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/TryReconnect"),
                    isActive = () => autoRepairEnabled,
                    toggleAction = () => autoRepairEnabled = !autoRepairEnabled
                };

                if (autoRepairEnabled)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "APM_Gizmo_SetInterval".Translate(autoRepairIntervalTicks.ToStringTicksToPeriod()),
                        action = () =>
                        {
                            int idx = System.Array.IndexOf(IntervalOptions, autoRepairIntervalTicks);
                            autoRepairIntervalTicks = IntervalOptions[(idx + 1) % IntervalOptions.Length];
                        }
                    };
                }
            }
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            if (base.Working && ContainedMech != null)
            {
                s += "\n" + "APM_Repairing".Translate(ContainedMech.LabelShort);
                s += "\n" + "Health".Translate() + ": " + ContainedMech.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
            }
            return s;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoRepairEnabled, "autoRepairEnabled", false);
            Scribe_Values.Look(ref autoRepairIntervalTicks, "autoRepairIntervalTicks", 2500);
            Scribe_Values.Look(ref autoRepairTimer, "autoRepairTimer", 0);
            Scribe_Values.Look(ref totalHpToHeal, "totalHpToHeal", 0f);
            Scribe_Values.Look(ref hpHealedSoFar, "hpHealedSoFar", 0f);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mechRepairEffecter != null)
            {
                mechRepairEffecter.Cleanup();
                mechRepairEffecter = null;
            }
            if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
            base.DeSpawn(mode);
        }

        private List<SectionData> BuildDialogData()
        {
            List<SectionData> sections = new List<SectionData>();
            sections.Add(new SectionData("Mech Position Offset",
                () => Config.MechPositionOffsetNorth, v => Config.Props.mechPositionOffsetNorth = v,
                () => Config.MechPositionOffsetEast, v => Config.Props.mechPositionOffsetEast = v,
                () => Config.MechPositionOffsetSouth, v => Config.Props.mechPositionOffsetSouth = v,
                () => Config.MechPositionOffsetWest, v => Config.Props.mechPositionOffsetWest = v));
            
            if (Config.ArmsAnimation != null)
            {
                for (int i = 0; i < Config.ArmsAnimation.arms.Count; i++)
                {
                    ArmConfig arm = Config.ArmsAnimation.arms[i];
                    sections.Add(new SectionData("Arm " + i + " Offsets",
                        () => arm.drawOffsetNorth, v => arm.drawOffsetNorth = v,
                        () => arm.drawOffsetEast, v => arm.drawOffsetEast = v,
                        () => arm.drawOffsetSouth, v => arm.drawOffsetSouth = v,
                        () => arm.drawOffsetWest, v => arm.drawOffsetWest = v));
                    sections.Add(new SectionData("Arm " + i + " Graphic Size",
                        () => arm.graphicData.drawSize, v => { arm.graphicData.drawSize = v; armsAnim?.RegenerateArmGraphic(i); },
                        0.1f, 3f, 0.1f, 3f));
                    sections.Add(new SectionData("Arm " + i + " Random Interval",
                        () => arm.randomInterval, v => arm.randomInterval = v,
                        arm.randomInterval, 1f, 300f));
                    sections.Add(new SectionData("Arm " + i + " Random Reach",
                        () => arm.randomReach, v => arm.randomReach = v,
                        arm.randomReach, -1f, 1f));
                }
            }
            
            return sections;
        }

        private string ExportXml()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("        <mechPositionOffsetNorth>(" + Config.Props.mechPositionOffsetNorth.x.ToString("F3") + "," + Config.Props.mechPositionOffsetNorth.y.ToString("F3") + "," + Config.Props.mechPositionOffsetNorth.z.ToString("F3") + ")</mechPositionOffsetNorth>");
            sb.AppendLine("        <mechPositionOffsetEast>(" + Config.Props.mechPositionOffsetEast.x.ToString("F3") + "," + Config.Props.mechPositionOffsetEast.y.ToString("F3") + "," + Config.Props.mechPositionOffsetEast.z.ToString("F3") + ")</mechPositionOffsetEast>");
            sb.AppendLine("        <mechPositionOffsetSouth>(" + Config.Props.mechPositionOffsetSouth.x.ToString("F3") + "," + Config.Props.mechPositionOffsetSouth.y.ToString("F3") + "," + Config.Props.mechPositionOffsetSouth.z.ToString("F3") + ")</mechPositionOffsetSouth>");
            sb.AppendLine("        <mechPositionOffsetWest>(" + Config.Props.mechPositionOffsetWest.x.ToString("F3") + "," + Config.Props.mechPositionOffsetWest.y.ToString("F3") + "," + Config.Props.mechPositionOffsetWest.z.ToString("F3") + ")</mechPositionOffsetWest>");
            sb.AppendLine("        <armsAnimation>");
            sb.AppendLine("            <extendTicks>" + Config.ArmsAnimation.extendTicks + "</extendTicks>");
            sb.AppendLine("            <retractTicks>" + Config.ArmsAnimation.retractTicks + "</retractTicks>");
            sb.AppendLine("            <arms>");
            for (int i = 0; i < Config.ArmsAnimation.arms.Count; i++)
            {
                ArmConfig arm = Config.ArmsAnimation.arms[i];
                sb.AppendLine("                <li>");
                sb.AppendLine("                    <graphicData>");
                sb.AppendLine("                        <texPath>" + arm.graphicData.texPath + "</texPath>");
                sb.AppendLine("                        <graphicClass>" + arm.graphicData.graphicClass + "</graphicClass>");
                sb.AppendLine("                        <drawSize>(" + arm.graphicData.drawSize.x.ToString("F3") + "," + arm.graphicData.drawSize.y.ToString("F3") + ")</drawSize>");
                sb.AppendLine("                    </graphicData>");
                sb.AppendLine("                    <maxReach>" + arm.maxReach.ToString("F3") + "</maxReach>");
                sb.AppendLine("                    <drawOffsetNorth>(" + arm.drawOffsetNorth.x.ToString("F3") + "," + arm.drawOffsetNorth.y.ToString("F3") + "," + arm.drawOffsetNorth.z.ToString("F3") + ")</drawOffsetNorth>");
                sb.AppendLine("                    <drawOffsetEast>(" + arm.drawOffsetEast.x.ToString("F3") + "," + arm.drawOffsetEast.y.ToString("F3") + "," + arm.drawOffsetEast.z.ToString("F3") + ")</drawOffsetEast>");
                sb.AppendLine("                    <drawOffsetSouth>(" + arm.drawOffsetSouth.x.ToString("F3") + "," + arm.drawOffsetSouth.y.ToString("F3") + "," + arm.drawOffsetSouth.z.ToString("F3") + ")</drawOffsetSouth>");
                sb.AppendLine("                    <drawOffsetWest>(" + arm.drawOffsetWest.x.ToString("F3") + "," + arm.drawOffsetWest.y.ToString("F3") + "," + arm.drawOffsetWest.z.ToString("F3") + ")</drawOffsetWest>");
                if (arm.randomInterval.HasValue)
                {
                    sb.AppendLine("                    <randomInterval>" + arm.randomInterval.Value.min + "~" + arm.randomInterval.Value.max + "</randomInterval>");
                }
                sb.AppendLine("                </li>");
            }
            sb.AppendLine("            </arms>");
            sb.AppendLine("        </armsAnimation>");
            return sb.ToString();
        }
    }
}
