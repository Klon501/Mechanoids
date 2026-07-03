using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class DefModExtension_GazerEmplacement : DefModExtension
    {
        public float firingArcHalfAngle = 18f;
        public float firingPowerConsumption = 20000f;
        public int cooldownTicks = 2400;
    }

    public class Building_GazerEmplacement : Building, IVerbOwner
    {
        private const string SunRayAbilityDefName = "APM_SunRay";
        private const string SunRayFallbackIconPath = "Things/Item/Equipment/WeaponRanged/SunRay";
        private const string CancelIconPath = "UI/Designators/Cancel";
        private const int AutoTargetScanIntervalTicks = 30;

        private static readonly Color WarmupProgressColor = new Color(1f, 0.58f, 0.1f);
        private static readonly Color FiringProgressColor = new Color(1f, 0.2f, 0.2f);
        private static readonly Color CooldownProgressColor = new Color(0.2f, 0.55f, 1f);

        private LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;
        private LocalTargetInfo activeTarget = LocalTargetInfo.Invalid;
        private bool forcedTargetIsPlayerDesignated;
        private int nextAutoTargetScanTick;
        private int warmupTicksLeft;
        private int cooldownTicksLeft;
        private bool firing;
        private int beamTicksToNextPathStep;
        private int beamBurstShotsLeft = -1;
        private Vector3 initialTargetPosition;
        private Vector3 lastTargetPosition;

        private List<Vector3> beamPath = new List<Vector3>();
        private HashSet<IntVec3> beamPathCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> beamHitCells = new HashSet<IntVec3>();
        private List<Vector3> tmpPath = new List<Vector3>();
        private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

        private CompPowerTrader powerComp;
        private CompMannable mannableComp;
        private CompFlickable flickableComp;
        private CompBreakdownable breakdownableComp;
        private CompGazerBeamOriginOffsets beamOriginComp;
        private MoteDualAttached warmupLineMote;
        private Mote chargeMote;
        private Mote targetMote;
        private Sustainer aimingSustainer;
        private MoteDualAttached beamMote;
        private Effecter beamEndEffecter;
        private Sustainer beamSustainer;
        private AbilityDef cachedSunRayAbility;
        private VerbTracker verbTracker;
        private List<VerbProperties> cachedVerbProperties;
        private bool compsCached;

        private DefModExtension_GazerEmplacement Props => def.GetModExtension<DefModExtension_GazerEmplacement>();

        private AbilityDef SunRayAbilityDef
        {
            get
            {
                if (cachedSunRayAbility == null)
                {
                    cachedSunRayAbility = DefDatabase<AbilityDef>.GetNamedSilentFail(SunRayAbilityDefName);
                }

                return cachedSunRayAbility;
            }
        }

        private Texture2D SunRayCommandIcon
        {
            get
            {
                if (beamOriginComp != null)
                {
                    Texture2D compIcon = beamOriginComp.GetFireCommandIcon();
                    if (compIcon != null)
                    {
                        return compIcon;
                    }
                }

                string iconPath = SunRayAbilityDef != null ? SunRayAbilityDef.iconPath : null;
                if (!iconPath.NullOrEmpty())
                {
                    Texture2D icon = ContentFinder<Texture2D>.Get(iconPath, false);
                    if (icon != null)
                    {
                        return icon;
                    }
                }

                return ContentFinder<Texture2D>.Get(SunRayFallbackIconPath);
            }
        }

        private Texture2D CancelCommandIcon
        {
            get
            {
                if (beamOriginComp != null)
                {
                    Texture2D compIcon = beamOriginComp.GetCancelCommandIcon();
                    if (compIcon != null)
                    {
                        return compIcon;
                    }
                }

                return ContentFinder<Texture2D>.Get(CancelIconPath);
            }
        }

        private VerbProperties BeamProps => SunRayAbilityDef != null ? SunRayAbilityDef.verbProperties : null;

        private VerbTracker VerbTrackerInternal
        {
            get
            {
                EnsureVerbTrackerInitialized();
                return verbTracker;
            }
        }

        private Verb_GazerEmplacementBeam BeamCommandVerb => VerbTrackerInternal.PrimaryVerb as Verb_GazerEmplacementBeam;

        private float FiringArcHalfAngle => Props != null ? Props.firingArcHalfAngle : 18f;
        private float FiringPowerConsumption => Props != null ? Props.firingPowerConsumption : 20000f;
        private int CooldownTicks => Props != null ? Props.cooldownTicks : 2400;
        private int WarmupTicks => Mathf.Max(1, (BeamProps != null ? BeamProps.warmupTime : 9.5f).SecondsToTicks());
        private int BeamStepTicks => Mathf.Max(1, BeamProps != null ? BeamProps.ticksBetweenBurstShots : 14);
        private float Range => BeamProps != null ? BeamProps.range : 34.9f;
        private float MinRange => BeamProps != null ? BeamProps.minRange : 3.9f;
        private int MinRangeCells => Mathf.RoundToInt(MinRange);

        public Vector3 BeamOriginWorld
        {
            get
            {
                Vector3 offset = beamOriginComp != null ? beamOriginComp.GetWorldOffset(Rotation) : Vector3.zero;
                return DrawPos + offset;
            }
        }

        public IntVec3 BeamOriginCell => BeamOriginWorld.ToIntVec3();

        public IntVec3 BeamTraceSourceCell
        {
            get
            {
                IntVec3 cell = BeamOriginCell;
                CellRect occupied = GenAdj.OccupiedRect(Position, Rotation, def.Size);
                if (!occupied.Contains(cell))
                {
                    return cell;
                }

                IntVec3 step = Rotation.FacingCell;
                IntVec3 next = cell;
                for (int i = 0; i < 8; i++)
                {
                    next += step;
                    if (!next.InBounds(Map) || !occupied.Contains(next))
                    {
                        return next;
                    }
                }

                return cell + step;
            }
        }

        VerbTracker IVerbOwner.VerbTracker => VerbTrackerInternal;
        List<VerbProperties> IVerbOwner.VerbProperties => GetCachedVerbProperties();
        List<Tool> IVerbOwner.Tools => null;
        ImplementOwnerTypeDef IVerbOwner.ImplementOwnerTypeDef => DefDatabase<ImplementOwnerTypeDef>.GetNamed("NativeVerb");
        Thing IVerbOwner.ConstantCaster => this;

        string IVerbOwner.UniqueVerbOwnerID()
        {
            try
            {
                return GetUniqueLoadID();
            }
            catch
            {
                return ((def != null) ? def.defName : GetType().Name) + "_Preview_" + GetHashCode();
            }
        }

        bool IVerbOwner.VerbsStillUsableBy(Pawn p)
        {
            return Spawned;
        }

        public Building_GazerEmplacement()
        {
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compsCached = false;
            EnsureCachedComps();
            EnsureVerbTrackerInitialized();
            UpdatePowerDraw();
        }

        public override void Tick()
        {
            base.Tick();
            EnsureCachedComps();

            if (mannableComp != null && !mannableComp.MannedNow && (forcedTarget.IsValid || activeTarget.IsValid || warmupTicksLeft > 0 || firing))
            {
                ClearTargetsBecauseUnmanned();
            }

            if (forcedTarget.HasThing && IsTargetDestroyed(forcedTarget))
            {
                ClearForcedTargetFlag();
            }

            if (activeTarget.HasThing && IsTargetDestroyed(activeTarget))
            {
                activeTarget = LocalTargetInfo.Invalid;
            }

            if (!forcedTargetIsPlayerDesignated && !firing && warmupTicksLeft <= 0)
            {
                UpdateAutoTarget();
            }

            if (firing)
            {
                if (!CanOperate())
                {
                    CancelFiring();
                }
                else
                {
                    TickFiring();
                }
            }
            else if (warmupTicksLeft > 0)
            {
                if (!CanContinueWarmup())
                {
                    CancelWarmup();
                }
                else
                {
                    UpdateWarmupEffects();
                    warmupTicksLeft--;
                    if (warmupTicksLeft <= 0)
                    {
                        BeginFiring();
                    }
                }
            }
            else if (cooldownTicksLeft > 0)
            {
                cooldownTicksLeft--;
            }

            if (!firing && warmupTicksLeft <= 0 && cooldownTicksLeft <= 0 && ShouldStartWarmup())
            {
                StartWarmup();
            }

            UpdatePowerDraw();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            CleanupWarmupEffects();
            CleanupBeamEffects();
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref forcedTarget, "forcedTarget");
            Scribe_TargetInfo.Look(ref activeTarget, "activeTarget");
            Scribe_Values.Look(ref forcedTargetIsPlayerDesignated, "forcedTargetIsPlayerDesignated");
            Scribe_Values.Look(ref warmupTicksLeft, "warmupTicksLeft");
            Scribe_Values.Look(ref cooldownTicksLeft, "cooldownTicksLeft");
            Scribe_Values.Look(ref firing, "firing");
            Scribe_Values.Look(ref beamTicksToNextPathStep, "beamTicksToNextPathStep");
            Scribe_Values.Look(ref beamBurstShotsLeft, "beamBurstShotsLeft", -1);
            Scribe_Values.Look(ref initialTargetPosition, "initialTargetPosition");
            Scribe_Values.Look(ref lastTargetPosition, "lastTargetPosition");
            Scribe_Collections.Look(ref beamPath, "beamPath", LookMode.Value);
            Scribe_Collections.Look(ref beamPathCells, "beamPathCells", LookMode.Value);
            Scribe_Collections.Look(ref beamHitCells, "beamHitCells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (beamPath == null) beamPath = new List<Vector3>();
                if (beamPathCells == null) beamPathCells = new HashSet<IntVec3>();
                if (beamHitCells == null) beamHitCells = new HashSet<IntVec3>();
                if (tmpPath == null) tmpPath = new List<Vector3>();
                if (tmpPathCells == null) tmpPathCells = new HashSet<IntVec3>();
                if (tmpHighlightCells == null) tmpHighlightCells = new HashSet<IntVec3>();
                if (tmpSecondaryHighlightCells == null) tmpSecondaryHighlightCells = new HashSet<IntVec3>();
                compsCached = false;
                cachedVerbProperties = null;
                verbTracker = null;
                EnsureCachedComps();
                EnsureVerbTrackerInitialized();
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Faction != Faction.OfPlayer)
            {
                yield break;
            }

            Verb_GazerEmplacementBeam beamVerb = BeamCommandVerb;
            Command_GazerEmplacementVerbTarget command = new Command_GazerEmplacementVerbTarget
            {
                defaultLabel = beamVerb != null && !beamVerb.ReportLabel.NullOrEmpty() ? beamVerb.ReportLabel.CapitalizeFirst() : "Sun Ray",
                defaultDesc = "Order the emplacement to project a Sun Ray inside its fixed frontal sector.",
                icon = SunRayCommandIcon,
                verb = beamVerb,
                drawRadius = false,
                requiresAvailableVerb = false,
                emplacement = this
            };

            string reason;
            if (beamVerb == null)
            {
                command.Disable("Sun Ray definition is missing.");
            }
            else if (!CanUseVerb(out reason))
            {
                command.Disable(reason);
            }

            yield return command;

            if (forcedTarget.IsValid && forcedTargetIsPlayerDesignated)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Stop forced target",
                    defaultDesc = "Clear the current forced target.",
                    icon = CancelCommandIcon,
                    action = delegate
                    {
                        ClearForcedTarget();
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    }
                };
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (!Spawned || BeamProps == null)
            {
                return;
            }

            DrawFiringArc();

            if (forcedTarget.IsValid)
            {
                GenDraw.DrawLineBetween(BeamOriginWorld, forcedTarget.CenterVector3, SimpleColor.Red, 0.18f);
                GenDraw.DrawTargetHighlight(forcedTarget);
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string baseString = base.GetInspectString();
            if (!baseString.NullOrEmpty())
            {
                sb.AppendLine(baseString);
            }

            if (firing)
            {
                sb.AppendLine("Status: Firing Sun Ray");
            }
            else if (warmupTicksLeft > 0)
            {
                sb.AppendLine("Status: Charging");
            }
            else if (cooldownTicksLeft > 0)
            {
                sb.AppendLine("Status: Cooling down");
            }
            else
            {
                sb.AppendLine("Status: Ready");
            }

            if (forcedTarget.IsValid)
            {
                sb.Append(forcedTargetIsPlayerDesignated ? "Forced target: " : "Auto target: ");
                sb.Append(TargetLabel(forcedTarget));
            }

            return sb.ToString().TrimEndNewlines();
        }


        public bool TryGetCommandProgress(out float fillPercent, out Color fillColor)
        {
            if (warmupTicksLeft > 0)
            {
                fillPercent = 1f - (float)warmupTicksLeft / Mathf.Max(1f, WarmupTicks);
                fillColor = WarmupProgressColor;
                return true;
            }

            if (firing)
            {
                float totalSteps = Mathf.Max(1f, beamPath.Count);
                float remainingSteps = Mathf.Clamp(beamBurstShotsLeft + Mathf.Clamp01(GetBeamShotProgress()), 0f, totalSteps);
                fillPercent = Mathf.Clamp01(1f - remainingSteps / totalSteps);
                fillColor = FiringProgressColor;
                return true;
            }

            if (cooldownTicksLeft > 0)
            {
                fillPercent = 1f - (float)cooldownTicksLeft / Mathf.Max(1f, CooldownTicks);
                fillColor = CooldownProgressColor;
                return true;
            }

            fillPercent = 0f;
            fillColor = Color.clear;
            return false;
        }

        public bool CanUseVerb(out string failReason)
        {
            if (!Spawned)
            {
                failReason = "Not spawned.";
                return false;
            }

            if (BeamProps == null)
            {
                failReason = "Sun Ray definition is missing.";
                return false;
            }

            if (breakdownableComp != null && breakdownableComp.BrokenDown)
            {
                failReason = "Broken down.";
                return false;
            }

            if (flickableComp != null && !flickableComp.SwitchIsOn)
            {
                failReason = "Turned off.";
                return false;
            }

            if (powerComp != null && !powerComp.PowerOn)
            {
                failReason = "No power.";
                return false;
            }

            if (mannableComp != null && !mannableComp.MannedNow)
            {
                failReason = "Needs an operator.";
                return false;
            }

            failReason = null;
            return true;
        }

        public bool CanAttackTargetForVerb(LocalTargetInfo target, out string failReason)
        {
            if (BeamProps == null)
            {
                failReason = "Sun Ray definition is missing.";
                return false;
            }

            if (!target.IsValid)
            {
                failReason = "Choose a target cell.";
                return false;
            }

            if (target.HasThing && IsTargetDestroyed(target))
            {
                failReason = "Target is gone.";
                return false;
            }

            IntVec3 cell = target.Cell;
            if (!cell.InBounds(Map))
            {
                failReason = "Target is out of bounds.";
                return false;
            }

            Vector3 targetCenter = target.CenterVector3;
            float distance = (targetCenter - BeamOriginWorld).MagnitudeHorizontal();
            if (distance < MinRange)
            {
                failReason = "Target is too close.";
                return false;
            }

            if (distance > Range)
            {
                failReason = "Target is out of range.";
                return false;
            }

            if (!IsInsideFiringArc(targetCenter))
            {
                failReason = "Target is outside the firing sector.";
                return false;
            }

            failReason = null;
            return true;
        }

        public void DrawVerbTargetingPreview(LocalTargetInfo target)
        {
            DrawFiringArc();
            if (!target.IsValid)
            {
                return;
            }

            string failReason;
            bool valid = CanAttackTargetForVerb(target, out failReason);
            GenDraw.DrawTargetHighlight(target);
            GenDraw.DrawLineBetween(BeamOriginWorld, target.CenterVector3, valid ? SimpleColor.Green : SimpleColor.Red, 0.18f);

            if (!valid)
            {
                return;
            }

            DrawBeamHighlightField(target);
        }

        public bool TryOrderShot(LocalTargetInfo target, out string failReason)
        {
            if (!CanAttackTargetForVerb(target, out failReason))
            {
                return false;
            }

            forcedTarget = target;
            forcedTargetIsPlayerDesignated = true;
            if (!firing && warmupTicksLeft <= 0 && cooldownTicksLeft <= 0 && CanOperate())
            {
                StartWarmup();
            }

            failReason = null;
            return true;
        }

        public void ClearForcedTarget()
        {
            ClearForcedTargetFlag();
            if (!firing)
            {
                activeTarget = LocalTargetInfo.Invalid;
                CancelWarmup();
            }
        }

        private void ClearTargetsBecauseUnmanned()
        {
            ClearForcedTargetFlag();
            activeTarget = LocalTargetInfo.Invalid;
            if (firing)
            {
                CancelFiring();
            }
            else if (warmupTicksLeft > 0)
            {
                CancelWarmup();
            }
        }
        public void DrawFiringArc()
        {
            if (!Spawned || BeamProps == null)
            {
                return;
            }

            Vector3 origin = BeamOriginWorld;
            float radius = Range;
            float startAngle = Rotation.AsAngle - FiringArcHalfAngle;
            float endAngle = Rotation.AsAngle + FiringArcHalfAngle;
            const int segments = 18;

            Vector3 previous = origin + DirectionForAngle(startAngle) * radius;
            GenDraw.DrawLineBetween(origin, previous, SimpleColor.White, 0.1f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
                Vector3 next = origin + DirectionForAngle(angle) * radius;
                GenDraw.DrawLineBetween(previous, next, SimpleColor.White, 0.1f);
                previous = next;
            }
            GenDraw.DrawLineBetween(origin, previous, SimpleColor.White, 0.1f);
        }

        private List<VerbProperties> GetCachedVerbProperties()
        {
            if (cachedVerbProperties != null)
            {
                return cachedVerbProperties;
            }

            VerbProperties props = new VerbProperties
            {
                verbClass = typeof(Verb_GazerEmplacementBeam),
                hasStandardCommand = true,
                label = "Sun Ray",
                range = Range,
                minRange = MinRange,
                targetParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetBuildings = true,
                    canTargetItems = true,
                    canTargetPawns = true,
                    canTargetSelf = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                }
            };

            cachedVerbProperties = new List<VerbProperties> { props };
            return cachedVerbProperties;
        }

        private void EnsureVerbTrackerInitialized()
        {
            if (verbTracker != null)
            {
                return;
            }

            verbTracker = new VerbTracker(this);
            _ = verbTracker.AllVerbs;
        }

        private void EnsureCachedComps()
        {
            if (compsCached)
            {
                return;
            }

            powerComp = GetComp<CompPowerTrader>();
            mannableComp = GetComp<CompMannable>();
            flickableComp = GetComp<CompFlickable>();
            breakdownableComp = GetComp<CompBreakdownable>();
            beamOriginComp = GetComp<CompGazerBeamOriginOffsets>();
            compsCached = true;
        }

        private void ClearForcedTargetFlag()
        {
            forcedTarget = LocalTargetInfo.Invalid;
            forcedTargetIsPlayerDesignated = false;
        }

        private bool IsTargetDestroyed(LocalTargetInfo target)
        {
            return target.HasThing && (target.Thing == null || target.Thing.Destroyed || target.Thing.Map != Map);
        }

        private bool CanOperate()
        {
            if (!Spawned || BeamProps == null)
            {
                return false;
            }

            if (breakdownableComp != null && breakdownableComp.BrokenDown)
            {
                return false;
            }

            if (flickableComp != null && !flickableComp.SwitchIsOn)
            {
                return false;
            }

            if (powerComp != null && !powerComp.PowerOn)
            {
                return false;
            }

            if (mannableComp != null && !mannableComp.MannedNow)
            {
                return false;
            }

            return true;
        }

        private bool CanContinueWarmup()
        {
            if (!CanOperate() || !activeTarget.IsValid)
            {
                return false;
            }

            string failReason;
            return CanAttackTargetForVerb(activeTarget, out failReason);
        }

        private bool ShouldStartWarmup()
        {
            if (!forcedTarget.IsValid || !CanOperate())
            {
                return false;
            }

            string failReason;
            if (!CanAttackTargetForVerb(forcedTarget, out failReason))
            {
                if (forcedTarget.HasThing && IsTargetDestroyed(forcedTarget))
                {
                    forcedTarget = LocalTargetInfo.Invalid;
                    forcedTargetIsPlayerDesignated = false;
                }
                return false;
            }

            return true;
        }

        private void StartWarmup()
        {
            string failReason;
            if (!CanAttackTargetForVerb(forcedTarget, out failReason))
            {
                if (forcedTarget.HasThing && IsTargetDestroyed(forcedTarget))
                {
                    ClearForcedTargetFlag();
                }
                return;
            }

            activeTarget = forcedTarget;
            warmupTicksLeft = WarmupTicks;
            CleanupWarmupEffects();
            UpdateWarmupEffects();
        }

        private void CancelWarmup()
        {
            warmupTicksLeft = 0;
            if (!firing)
            {
                activeTarget = LocalTargetInfo.Invalid;
            }
            CleanupWarmupEffects();
        }

        private void BeginFiring()
        {
            CleanupWarmupEffects();
            warmupTicksLeft = 0;

            if (!activeTarget.IsValid || BeamProps == null)
            {
                FinishFiring(false);
                return;
            }

            initialTargetPosition = activeTarget.CenterVector3;
            lastTargetPosition = initialTargetPosition;
            CalculatePath(initialTargetPosition, beamPath, beamPathCells);
            if (beamPath.Count == 0)
            {
                FinishFiring(false);
                return;
            }

            firing = true;
            beamBurstShotsLeft = beamPath.Count - 1;
            beamTicksToNextPathStep = BeamStepTicks;
            beamHitCells.Clear();

            if (BeamProps.beamMoteDef != null)
            {
                beamMote = MoteMaker.MakeInteractionOverlay(BeamProps.beamMoteDef, this, new TargetInfo(beamPath[0].ToIntVec3(), Map));
            }

            if (BeamProps.soundCastTail != null)
            {
                BeamProps.soundCastTail.PlayOneShot(new TargetInfo(BeamTraceSourceCell, Map));
            }

            FleckMaker.Static(BeamOriginWorld, Map, FleckDefOf.ShotFlash, BeamProps.muzzleFlashScale);
            if (BeamProps.soundCastBeam != null)
            {
                beamSustainer = BeamProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            }

            UpdateBeamEffects();
            FireCurrentBeamStep();
            if (beamBurstShotsLeft < 0)
            {
                FinishFiring(true);
            }
        }

        private void TickFiring()
        {
            if (beamBurstShotsLeft < 0 || beamPath == null || beamPath.Count == 0)
            {
                FinishFiring(true);
                return;
            }

            UpdateBeamEffects();
            beamTicksToNextPathStep--;
            if (beamTicksToNextPathStep > 0)
            {
                return;
            }

            FireCurrentBeamStep();
            if (beamBurstShotsLeft < 0)
            {
                FinishFiring(true);
                return;
            }

            beamTicksToNextPathStep = BeamStepTicks;
        }

        private void FinishFiring(bool startCooldown)
        {
            firing = false;
            beamBurstShotsLeft = -1;
            beamTicksToNextPathStep = 0;
            activeTarget = LocalTargetInfo.Invalid;
            CleanupBeamEffects();
            if (startCooldown)
            {
                cooldownTicksLeft = CooldownTicks;
            }
        }

        private void CancelFiring()
        {
            firing = false;
            beamBurstShotsLeft = -1;
            beamTicksToNextPathStep = 0;
            activeTarget = LocalTargetInfo.Invalid;
            CleanupBeamEffects();
        }
        private void UpdatePowerDraw()
        {
            if (powerComp == null)
            {
                return;
            }

            float powerUse = powerComp.Props.basePowerConsumption;
            if (firing || warmupTicksLeft > 0)
            {
                powerUse = FiringPowerConsumption;
            }

            powerComp.PowerOutput = 0f - powerUse;
        }

        private void UpdateWarmupEffects()
        {
            if (BeamProps == null || !activeTarget.IsValid || Map == null)
            {
                return;
            }

            Vector3 delta = activeTarget.CenterVector3 - BeamOriginWorld;
            Vector3 flatDelta = delta.Yto0();
            if (flatDelta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 normalized = flatDelta.normalized;
            float lineLength = BeamProps.aimingLineMoteFixedLength.HasValue && BeamProps.aimingLineMoteFixedLength.Value > 0f ? BeamProps.aimingLineMoteFixedLength.Value : flatDelta.MagnitudeHorizontal();
            Vector3 lineEnd = BeamOriginWorld + normalized * lineLength;
            IntVec3 lineEndCell = lineEnd.ToIntVec3();
            Vector3 startOffset = BeamOriginWorld - Position.ToVector3Shifted();
            Vector3 endOffset = lineEnd - lineEndCell.ToVector3Shifted();

            if ((warmupLineMote == null || warmupLineMote.Destroyed) && BeamProps.aimingLineMote != null)
            {
                warmupLineMote = MoteMaker.MakeInteractionOverlay(BeamProps.aimingLineMote, this, new TargetInfo(lineEndCell, Map), startOffset, endOffset);
            }

            if (warmupLineMote != null)
            {
                warmupLineMote.UpdateTargets(new TargetInfo(Position, Map), new TargetInfo(lineEndCell, Map), startOffset, endOffset);
                warmupLineMote.Maintain();
            }

            if (BeamProps.aimingChargeMote != null)
            {
                Vector3 chargePos = BeamOriginWorld + normalized * BeamProps.aimingChargeMoteOffset;
                UpdateOrSpawnStaticMote(ref chargeMote, BeamProps.aimingChargeMote, chargePos);
            }

            if (BeamProps.aimingTargetMote != null)
            {
                UpdateOrSpawnStaticMote(ref targetMote, BeamProps.aimingTargetMote, activeTarget.CenterVector3);
            }

            if (aimingSustainer == null && BeamProps.soundAiming != null)
            {
                aimingSustainer = BeamProps.soundAiming.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            }

            aimingSustainer?.Maintain();
        }

        private void CleanupWarmupEffects()
        {
            SafeDestroyMote(ref warmupLineMote);
            SafeDestroyMote(ref chargeMote);
            SafeDestroyMote(ref targetMote);
            SafeEndSustainer(ref aimingSustainer);
        }

        private void UpdateOrSpawnStaticMote(ref Mote mote, ThingDef moteDef, Vector3 worldPos)
        {
            if (moteDef == null || !worldPos.ToIntVec3().InBounds(Map))
            {
                return;
            }

            if (mote == null || mote.Destroyed)
            {
                mote = MoteMaker.MakeStaticMote(worldPos, Map, moteDef, 1f);
            }

            if (mote != null)
            {
                mote.exactPosition = worldPos;
                mote.Maintain();
            }
        }

        private float GetBeamShotProgress()
        {
            if (BeamStepTicks <= 0)
            {
                return 0f;
            }

            return (float)beamTicksToNextPathStep / BeamStepTicks;
        }

        private Vector3 GetInterpolatedBeamPosition()
        {
            if (beamPath == null || beamPath.Count == 0)
            {
                return lastTargetPosition;
            }

            Vector3 delta;
            if (activeTarget.HasThing && activeTarget.Thing != null && activeTarget.Thing.Map == Map)
            {
                lastTargetPosition = activeTarget.CenterVector3;
                delta = lastTargetPosition - initialTargetPosition;
            }
            else
            {
                delta = lastTargetPosition - initialTargetPosition;
            }

            int fromIndex = Mathf.Clamp(Mathf.Max(beamBurstShotsLeft - MinRangeCells, 0), 0, beamPath.Count - 1);
            int toIndex = Mathf.Clamp(Mathf.Min(Mathf.Max(beamBurstShotsLeft + 1 - MinRangeCells, 1), beamPath.Count - 1 - MinRangeCells), 0, beamPath.Count - 1);
            return Vector3.Lerp(beamPath[fromIndex], beamPath[toIndex], GetBeamShotProgress()) + delta;
        }

        private void UpdateBeamEffects()
        {
            if (!firing || BeamProps == null || beamPath == null || beamPath.Count == 0)
            {
                return;
            }

            Vector3 endWorld = GetInterpolatedBeamPosition();
            IntVec3 endCell = endWorld.ToIntVec3();
            Vector3 beamVector = endWorld - BeamOriginWorld;
            float beamLength = beamVector.MagnitudeHorizontal();
            Vector3 flatBeamVector = beamVector.Yto0();
            if (flatBeamVector.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 normalized = flatBeamVector.normalized;
            IntVec3 sourceCellForTrace = BeamTraceSourceCell;
            IntVec3 lastVisibleCell = GenSight.LastPointOnLineOfSight(sourceCellForTrace, endCell, (IntVec3 c) => c.CanBeSeenOverFast(Map), true);
            if (lastVisibleCell.IsValid)
            {
                beamLength -= (endCell - lastVisibleCell).LengthHorizontal;
                endWorld = BeamOriginWorld + normalized * beamLength;
                endCell = endWorld.ToIntVec3();
            }

            Vector3 startOffset = BeamOriginWorld - Position.ToVector3Shifted() + normalized * BeamProps.beamStartOffset;
            Vector3 endOffset = endWorld - endCell.ToVector3Shifted();

            if (beamMote != null)
            {
                beamMote.UpdateTargets(new TargetInfo(Position, Map), new TargetInfo(endCell, Map), startOffset, endOffset);
                beamMote.Maintain();
            }

            if (BeamProps.beamGroundFleckDef != null && Rand.Chance(BeamProps.beamFleckChancePerTick))
            {
                FleckMaker.Static(endWorld, Map, BeamProps.beamGroundFleckDef);
            }

            if (beamEndEffecter == null && BeamProps.beamEndEffecterDef != null)
            {
                beamEndEffecter = BeamProps.beamEndEffecterDef.Spawn(endCell, Map, endOffset);
            }

            if (beamEndEffecter != null)
            {
                beamEndEffecter.offset = endOffset;
                beamEndEffecter.EffectTick(new TargetInfo(endCell, Map), TargetInfo.Invalid);
                beamEndEffecter.ticksLeft--;
            }

            if (BeamProps.beamLineFleckDef != null && BeamProps.beamLineFleckChanceCurve != null)
            {
                float sampleCount = beamLength;
                for (int i = 0; i < sampleCount; i++)
                {
                    if (Rand.Chance(BeamProps.beamLineFleckChanceCurve.Evaluate(i / sampleCount)))
                    {
                        Vector3 fleckOffset = i * normalized - normalized * Rand.Value + normalized / 2f;
                        FleckMaker.Static(BeamOriginWorld + fleckOffset, Map, BeamProps.beamLineFleckDef);
                    }
                }
            }

            beamSustainer?.Maintain();
        }

        private void CleanupBeamEffects()
        {
            SafeDestroyMote(ref beamMote);
            SafeCleanupEffecter(ref beamEndEffecter);
            SafeEndSustainer(ref beamSustainer);
            beamPath.Clear();
            beamPathCells.Clear();
            beamHitCells.Clear();
        }

        private void FireCurrentBeamStep()
        {
            if (BeamProps == null)
            {
                beamBurstShotsLeft = -1;
                return;
            }

            IntVec3 sourceCell = BeamTraceSourceCell;
            IntVec3 targetCell = GetInterpolatedBeamPosition().Yto0().ToIntVec3();
            if (TryGetHitCell(sourceCell, targetCell, out IntVec3 hitCell))
            {
                HitCell(hitCell, sourceCell);
                if (BeamProps.beamHitsNeighborCells)
                {
                    beamHitCells.Add(hitCell);
                    foreach (IntVec3 neighbourCell in GetBeamHitNeighbourCells(sourceCell, hitCell))
                    {
                        if (!beamHitCells.Contains(neighbourCell))
                        {
                            float damageFactor = beamPathCells.Contains(neighbourCell) ? 1f : 0.5f;
                            HitCell(neighbourCell, sourceCell, damageFactor);
                            beamHitCells.Add(neighbourCell);
                        }
                    }
                }
            }

            beamBurstShotsLeft--;
        }
        private void CalculatePath(Vector3 target, List<Vector3> pathList, HashSet<IntVec3> pathCellsList)
        {
            pathList.Clear();
            IntVec3 origin = BeamTraceSourceCell;
            IntVec3 farTarget = target.ToIntVec3();
            float distance = (farTarget - origin).LengthHorizontal;
            if (distance < 0.01f)
            {
                return;
            }

            float dx = (farTarget.x - origin.x) / distance;
            float dz = (farTarget.z - origin.z) / distance;
            farTarget.x = Mathf.RoundToInt(origin.x + dx * BeamProps.range);
            farTarget.z = Mathf.RoundToInt(origin.z + dz * BeamProps.range);
            List<IntVec3> cells = GenSight.BresenhamCellsBetween(origin, farTarget);
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if (cell.InBounds(Map))
                {
                    pathList.Add(cell.ToVector3Shifted());
                }
            }

            pathCellsList.Clear();
            for (int i = 0; i < pathList.Count; i++)
            {
                pathCellsList.Add(pathList[i].ToIntVec3());
            }
            pathList.Reverse();
        }

        private bool CanHitThing(Thing thing)
        {
            if (thing == null || !thing.Spawned)
            {
                return false;
            }

            return !CoverUtility.ThingCovered(thing, Map);
        }

        private bool TryGetHitCell(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell)
        {
            IntVec3 lastPoint = GenSight.LastPointOnLineOfSight(source, targetCell, (IntVec3 c) => c.InBounds(Map) && c.CanBeSeenOverFast(Map), true);
            if (BeamProps.beamCantHitWithinMinRange && lastPoint.DistanceTo(source) < BeamProps.minRange)
            {
                hitCell = default;
                return false;
            }

            hitCell = lastPoint.IsValid ? lastPoint : targetCell;
            return lastPoint.IsValid;
        }

        private IEnumerable<IntVec3> GetBeamHitNeighbourCells(IntVec3 source, IntVec3 pos)
        {
            if (!BeamProps.beamHitsNeighborCells)
            {
                yield break;
            }

            for (int i = 0; i < 4; i++)
            {
                IntVec3 cell = pos + GenAdj.CardinalDirections[i];
                if (cell.InBounds(Map) && (!BeamProps.beamHitsNeighborCellsRequiresLOS || GenSight.LineOfSight(source, cell, Map)))
                {
                    yield return cell;
                }
            }
        }

        private void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
        {
            if (!cell.InBounds(Map))
            {
                return;
            }

            foreach (IntVec3 radialCell in GenRadial.RadialCellsAround(cell, 2f, true).InRandomOrder())
            {
                if (radialCell.InBounds(Map))
                {
                    ApplyDamage(VerbUtility.ThingsToHit(radialCell, Map, CanHitThing).RandomElementWithFallback(), sourceCell, damageFactor);
                }
            }

            if (BeamProps.beamSetsGroundOnFire && Rand.Chance(BeamProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(cell, Map, 1f, this);
            }
        }

        private void ApplyDamage(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
        {
            IntVec3 impactCell = GetInterpolatedBeamPosition().Yto0().ToIntVec3();
            IntVec3 clippedCell = GenSight.LastPointOnLineOfSight(sourceCell, impactCell, (IntVec3 c) => c.InBounds(Map) && c.CanBeSeenOverFast(Map), true);
            if (clippedCell.IsValid)
            {
                impactCell = clippedCell;
            }

            if (thing == null || BeamProps.beamDamageDef == null)
            {
                return;
            }

            float angle = (activeTarget.Cell - BeamTraceSourceCell).AngleFlat;
            BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(this, thing, activeTarget.Thing, null, null, null);
            DamageInfo damageInfo;
            if (BeamProps.beamTotalDamage > 0f && beamPathCells.Count > 0)
            {
                float amount = BeamProps.beamTotalDamage / beamPathCells.Count * damageFactor;
                damageInfo = new DamageInfo(BeamProps.beamDamageDef, amount, BeamProps.beamDamageDef.defaultArmorPenetration, angle, this, null, null, DamageInfo.SourceCategory.ThingOrUnknown, activeTarget.Thing);
            }
            else
            {
                float amount = BeamProps.beamDamageDef.defaultDamage * damageFactor;
                damageInfo = new DamageInfo(BeamProps.beamDamageDef, amount, BeamProps.beamDamageDef.defaultArmorPenetration, angle, this, null, null, DamageInfo.SourceCategory.ThingOrUnknown, activeTarget.Thing);
            }

            thing.TakeDamage(damageInfo).AssociateWithLog(log);
            if (thing.CanEverAttachFire())
            {
                float chance = BeamProps.flammabilityAttachFireChanceCurve == null ? BeamProps.beamChanceToAttachFire : BeamProps.flammabilityAttachFireChanceCurve.Evaluate(thing.GetStatValue(StatDefOf.Flammability));
                if (Rand.Chance(chance))
                {
                    thing.TryAttachFire(BeamProps.beamFireSizeRange.RandomInRange, this);
                }
            }
            else if (Rand.Chance(BeamProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(impactCell, Map, BeamProps.beamFireSizeRange.RandomInRange, this, BeamProps.flammabilityAttachFireChanceCurve);
            }
        }

        private void DrawBeamHighlightField(LocalTargetInfo target)
        {
            tmpPath.Clear();
            tmpPathCells.Clear();
            tmpHighlightCells.Clear();
            tmpSecondaryHighlightCells.Clear();
            CalculatePath(target.CenterVector3, tmpPath, tmpPathCells);
            foreach (IntVec3 pathCell in tmpPathCells)
            {
                IntVec3 sourceCell = BeamTraceSourceCell;
                if (!TryGetHitCell(sourceCell, pathCell, out IntVec3 hitCell))
                {
                    continue;
                }

                foreach (IntVec3 radialCell in GenRadial.RadialCellsAround(hitCell, 2f, true).InRandomOrder())
                {
                    tmpHighlightCells.Add(radialCell);
                }

                if (!BeamProps.beamHitsNeighborCells)
                {
                    continue;
                }

                foreach (IntVec3 neighbourCell in GetBeamHitNeighbourCells(sourceCell, hitCell))
                {
                    if (tmpHighlightCells.Contains(neighbourCell))
                    {
                        continue;
                    }

                    foreach (IntVec3 radialCell in GenRadial.RadialCellsAround(neighbourCell, 2f, true).InRandomOrder())
                    {
                        tmpSecondaryHighlightCells.Add(radialCell);
                    }
                }
            }

            tmpSecondaryHighlightCells.RemoveWhere((IntVec3 cell) => tmpHighlightCells.Contains(cell));
            if (tmpHighlightCells.Any())
            {
                GenDraw.DrawFieldEdges(tmpHighlightCells.ToList(), BeamProps.highlightColor ?? Color.white);
            }

            if (tmpSecondaryHighlightCells.Any())
            {
                GenDraw.DrawFieldEdges(tmpSecondaryHighlightCells.ToList(), BeamProps.secondaryHighlightColor ?? Color.white);
            }
        }

        private bool IsInsideFiringArc(Vector3 targetCenter)
        {
            Vector3 flatDelta = (targetCenter - BeamOriginWorld).Yto0();
            if (flatDelta.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Angle(GetFacingVector(Rotation), flatDelta.normalized) <= FiringArcHalfAngle;
        }

        private void UpdateAutoTarget()
        {
            if (forcedTargetIsPlayerDesignated || !CanOperate() || Map == null)
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextAutoTargetScanTick && forcedTarget.IsValid && (!forcedTarget.HasThing || !IsTargetDestroyed(forcedTarget)))
            {
                return;
            }

            nextAutoTargetScanTick = ticksGame + AutoTargetScanIntervalTicks;

            Thing bestTarget = FindBestAutoTarget();
            if (bestTarget != null)
            {
                forcedTarget = bestTarget;
                return;
            }

            forcedTarget = LocalTargetInfo.Invalid;
        }

        private Thing FindBestAutoTarget()
        {
            if (Map == null)
            {
                return null;
            }

            Thing bestThing = null;
            float bestScore = float.MinValue;
            List<Thing> potentialTargets = Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
            if (potentialTargets == null)
            {
                return null;
            }

            for (int i = 0; i < potentialTargets.Count; i++)
            {
                Thing thing = potentialTargets[i];
                if (thing == null || thing == this || !thing.Spawned || thing.Destroyed || !thing.HostileTo(this))
                {
                    continue;
                }

                string failReason;
                if (!CanAttackTargetForVerb(thing, out failReason))
                {
                    continue;
                }

                float score = 1000f - (thing.DrawPos - BeamOriginWorld).MagnitudeHorizontal();
                Pawn pawn = thing as Pawn;
                if (pawn != null && pawn.Spawned && !pawn.Dead)
                {
                    score += pawn.Downed ? 50f : 200f;
                }
                else if (thing is Building)
                {
                    score += 25f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestThing = thing;
                }
            }

            return bestThing;
        }

        private Vector3 GetFacingVector(Rot4 rot)
        {
            if (rot == Rot4.North)
            {
                return new Vector3(0f, 0f, 1f);
            }

            if (rot == Rot4.East)
            {
                return new Vector3(1f, 0f, 0f);
            }

            if (rot == Rot4.South)
            {
                return new Vector3(0f, 0f, -1f);
            }

            return new Vector3(-1f, 0f, 0f);
        }

        private Vector3 DirectionForAngle(float angle)
        {
            float radians = angle * 0.017453292f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private string TargetLabel(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return "none";
            }

            if (target.HasThing && target.Thing != null)
            {
                return target.Thing.LabelCap;
            }

            return target.Cell.ToString();
        }

        private void SafeDestroyMote<T>(ref T mote) where T : Mote
        {
            if (mote != null && !mote.Destroyed)
            {
                mote.Destroy(DestroyMode.Vanish);
            }

            mote = null;
        }

        private void SafeCleanupEffecter(ref Effecter effecter)
        {
            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
        }

        private void SafeEndSustainer(ref Sustainer sustainer)
        {
            if (sustainer != null)
            {
                sustainer.End();
                sustainer = null;
            }
        }
    }
}
