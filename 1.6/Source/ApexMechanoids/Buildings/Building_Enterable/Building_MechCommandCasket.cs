using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using PipeSystem;
using static ApexMechanoids.CompRemoteMechCasketAbilities;
using Unity.Burst.Intrinsics;

namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    public class Building_MechCommandCasket : Building_Enterable, IStoreSettingsParent, IThingHolderWithDrawnPawn, IThingHolder
    {
        private float containedNutritionNotInItems = 0f;

        private StorageSettings allowedNutritionSettings;

        [Unsaved(false)]
        private CompPowerTrader cachedPowerComp;

        [Unsaved(false)]
        private CompRemoteMechCasketAbilities cachedAbilityComp;

        [Unsaved(false)]
        private CompResource cachedResourceComp;

        public static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        public static readonly CachedTexture InsertPawnIcon = new CachedTexture("UI/Gizmos/InsertPawn");

        private const float BiostarvationGainPerDayNoFoodOrPower = 0.5f;

        private const float BiostarvationFallPerDayPoweredAndFed = 0.1f;

        private const float BasePawnConsumedNutritionPerDay = 2.0f; //1.6 is the normal rate for a pawn

        public const float NutritionBuffer = 10f;   //maximum nutrition inside

        private float minimumContainedNutrition = 1f;

        public float NutritionPercent
        { 
            get
            {
                if (containedNutritionNotInItems <= 0f)
                {
                    return 0f;
                }

                return (1f / NutritionBuffer) * NutritionStored;
            }
        }

        #region Needed from parent IStoreSettingsParent

         public bool StorageTabVisible => true;

        public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;

        public float HeldPawnBodyAngle => base.Rotation.AsAngle;

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        public override Vector3 PawnDrawOffset => CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);


        #endregion

        public bool PowerOn => PowerTraderComp.PowerOn;

        private CompPowerTrader PowerTraderComp
        {
            get
            {
                if (cachedPowerComp == null)
                {
                    cachedPowerComp = this.TryGetComp<CompPowerTrader>();
                }
                return cachedPowerComp;
            }
        }

        public float BiostarvationDailyOffset
        {
            get
            {
                if (!base.Working)
                {
                    return 0f;
                }
                if (!PowerOn || containedNutritionNotInItems <= 0f)
                {
                    return BiostarvationGainPerDayNoFoodOrPower;
                }
                return -BiostarvationFallPerDayPoweredAndFed;
            }
        }

        private float BiostarvationSeverityPercent
        {
            get
            {
                if (selectedPawn != null)
                {
                    Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                    if (firstHediffOfDef != null)
                    {
                        return firstHediffOfDef.Severity / HediffDefOf.BioStarvation.maxSeverity;
                    }
                }
                return 0f;
            }
        }

        public float NutritionConsumedPerDay
        {
            get
            {
                float num = BasePawnConsumedNutritionPerDay;
                if (BiostarvationSeverityPercent > 0f)
                {
                    float num2 = 1.1f;
                    num *= num2;
                }
                return num;
            }
        }

        public float NutritionStored
        {
            get
            {
                float num = containedNutritionNotInItems;
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    Thing thing = innerContainer[i];

                    if(thing != selectedPawn)
                    {
                        num += (float)thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
                    }
                }
                return num;
            }
        }

        public float NutritionNeeded
        {
            get
            {
                if (selectedPawn == null)
                {
                    float needed = minimumContainedNutrition - NutritionStored;

                    if(needed > 0)
                    {
                        return needed;
                    }
                    return 0f;
                }
                return NutritionBuffer - NutritionStored;
            }
        }
        

        public override void PostMake()
        {
            base.PostMake();
            allowedNutritionSettings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
            {
                allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                if (selectedPawn != null && innerContainer.Contains(selectedPawn))
                {
                    Notify_PawnRemoved();
                }
            }
            base.DeSpawn(mode);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
        }

        public override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(250))
            {
                PowerTraderComp.PowerOutput = (base.Working ? (0f - base.PowerComp.Props.PowerConsumption) : (0f - base.PowerComp.Props.idlePowerDraw));
                GetNutritionFromNetwork();
            }

            if (base.Working)
            {
                if (selectedPawn != null)
                {
                    float num = BiostarvationDailyOffset / 60000f * HediffDefOf.BioStarvation.maxSeverity;
                    Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                    if (firstHediffOfDef != null)
                    {
                        firstHediffOfDef.Severity += num;
                        if (firstHediffOfDef.ShouldRemove)
                        {
                            selectedPawn.health.RemoveHediff(firstHediffOfDef);
                        }
                    }
                    else if (num > 0f)
                    {
                        Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BioStarvation, selectedPawn);
                        hediff.Severity = num;
                        selectedPawn.health.AddHediff(hediff);
                    }
                }
                if (BiostarvationSeverityPercent >= 0.8f)
                {
                    TryToAutoEject();
                    return;
                }
                if (BiostarvationSeverityPercent >= 1f)
                {
                    Fail();
                    return;
                }
                containedNutritionNotInItems = Mathf.Clamp(containedNutritionNotInItems - NutritionConsumedPerDay / 60000f, 0f, 2.14748365E+09f);   // same as Building_GrowthVat
                if (containedNutritionNotInItems <= 0f)
                {
                    TryAbsorbNutritiousThing(); //destroys an nutriton item and adds that nutrition to containedNutritionNotInItems
                }
            }
        }


        #region texture drawing

        public override Graphic Graphic
        {
            get
            {
                return GetGraphic;
            }
        }

        private Graphic GetGraphic
        {
            get
            {
                if (CachedGraphic == null || UpdateGraphic)
                {
                    if (def.graphicData == null)
                    {
                        return BaseContent.BadGraphic;
                    }
                    Graphic BaseGraphic = def.graphicData.GraphicColoredFor(this);

                    CachedGraphic = GraphicDatabase.Get<Graphic_Multi>(GetTexturePath, BaseGraphic.Shader, BaseGraphic.drawSize, BaseGraphic.color, BaseGraphic.colorTwo, BaseGraphic.data);
                    UpdateGraphic = false;
                }

                return CachedGraphic;

            }
        }

        private Graphic CachedGraphic;
        private bool UpdateGraphic = false;

        private string GetTexturePath
        {
            get
            {
                if (selectedPawn != null && innerContainer.Contains(selectedPawn))
                {
                    return "Things/Building/MechCommandCasket/MechCommandCasket";
                }
                else
                {
                    return "Things/Building/MechCommandCasket/MechCommandCasket_Empty";
                }
            }
        }

        private void UpdateGraphics()
        {
            UpdateGraphic = true;
            foreach (IntVec3 item in this.OccupiedRect())
            {
                this.Map.mapDrawer.MapMeshDirty(item, MapMeshFlagDefOf.Things);
            }
        }

        #endregion


        private void GetNutritionFromNetwork()
        {
           // CompResource comp =  GetComp(__instance);
            if (CompResource == null)
            {
                return;
            }

            PipeNet pipeNet = CompResource.PipeNet;
            
            if (pipeNet != null)
            {
                float stored = pipeNet.Stored;
                while (NutritionNeeded > NutritionFromPaste && stored > 0f)
                {
                    Thing paste = ThingMaker.MakeThing(ThingDefOf.MealNutrientPaste);
                    paste.stackCount = 1;
                    innerContainer.TryAdd(paste);
                    pipeNet.DrawAmongStorage(1f, pipeNet.storages);
                }
            }
        }

        private float NutritionFromPaste => ThingDefOf.MealNutrientPaste.GetStatValueAbstract(StatDefOf.Nutrition);


        public override AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            if (base.Working && selectedPawn != null && innerContainer.Contains(selectedPawn))
            {
                return "Occupied".Translate();
            }
            if (!PowerOn)
            {
                return "NoPower".Translate().CapitalizeFirst();
            }
            if (selectedPawn != null && selectedPawn != pawn)
            {
                return "WaitingForPawn".Translate(selectedPawn.Named("PAWN"));
            }
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.BioStarvation))
            {
               pawn.health.hediffSet.TryGetHediff(HediffDefOf.BioStarvation, out Hediff starvation);

                if(starvation != null && starvation.Severity > 0.25f)
                {
                    return "PawnBiostarving".Translate(pawn.Named("PAWN"));
                }
            }
            if (!MechanitorUtility.IsMechanitor(pawn))
            {
                return "APM.CommandCasket.FailReason.NotMechanitor".Translate(pawn.Named("PAWN"));
            }
            return pawn.IsColonist && !pawn.IsQuestLodger();
        }


        public CompRemoteMechCasketAbilities CompAbilities
        {
            get
            {
                if (cachedAbilityComp == null)
                {
                    cachedAbilityComp = this.TryGetComp<CompRemoteMechCasketAbilities>();
                }
                return cachedAbilityComp;
            }
        }

        private CompResource CompResource
        {
            get
            {
                if (cachedResourceComp == null)
                {
                    cachedResourceComp = this.TryGetComp<CompResource>();
                }
                return cachedResourceComp;
            }
        }

        public override void TryAcceptPawn(Pawn pawn)
        {
            if (selectedPawn == null || !CanAcceptPawn(pawn))
            {
                return;
            }
            selectedPawn = pawn;
            if(CompAbilities != null)
            {
                CompAbilities.TryChangeUser(selectedPawn);
            }
            bool num = pawn.DeSpawnOrDeselect();
            if (innerContainer.TryAddOrTransfer(pawn))
            {
                SoundDefOf.GrowthVat_Close.PlayOneShot(SoundInfo.InMap(this));
                startTick = Find.TickManager.TicksGame;
            }
            if (num)
            {
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            }
            UpdateGraphics();
        }

        private void TryAbsorbNutritiousThing()
        {
            for (int i = 0; i < innerContainer.Count; i++)
            {
                if (innerContainer[i] != selectedPawn && innerContainer[i].def != ThingDefOf.Xenogerm && innerContainer[i].def != ThingDefOf.HumanEmbryo)
                {
                    float statValue = innerContainer[i].GetStatValue(StatDefOf.Nutrition);
                    if (statValue > 0f)
                    {
                        containedNutritionNotInItems += statValue;
                        innerContainer[i].SplitOff(1).Destroy();
                        break;
                    }
                }
            }
        }

        public void Finish()
        {
            if (selectedPawn != null && innerContainer.Contains(selectedPawn))
            {
                Notify_PawnRemoved();
                innerContainer.TryDrop(selectedPawn, InteractionCell, base.Map, ThingPlaceMode.Near, 1, out var _);
                OnStop();
            }
        }

        private bool TryToAutoEject()
        {
            if(!PowerOn)
            {  
                return false; 
            }

            Finish();
            return true;
        }

        private void Fail()
        {
            if (innerContainer.Contains(selectedPawn))
            {
                Notify_PawnRemoved();
                innerContainer.TryDrop(selectedPawn, InteractionCell, base.Map, ThingPlaceMode.Near, 1, out var _);
                Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                selectedPawn.Kill(null, firstHediffOfDef);
            }
            OnStop();
        }

        private void OnStop()
        {
            selectedPawn = null;
            startTick = -1;
            UpdateGraphics();
        }


        private void Notify_PawnRemoved()
        {
            SoundDefOf.GrowthVat_Open.PlayOneShot(SoundInfo.InMap(this));
            if (CompAbilities != null)
            {
                CompAbilities.TryChangeUser(null);
            }
        }

        public bool CanAcceptNutrition(Thing thing)
        {
            return allowedNutritionSettings.AllowedToAccept(thing);
        }

        public StorageSettings GetStoreSettings()
        {
            return allowedNutritionSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
            {
                yield return item;
            }
            if (DebugSettings.ShowDevGizmos)
            {
                if (base.Working && CompAbilities == null)
                {
                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = "APM.CommandCasket.Gizmo.CancelLink.Label".Translate();
                    command_Action.defaultDesc = "APM.CommandCasket.Gizmo.CancelLink.Desc".Translate();
                    command_Action.icon = CancelLoadingIcon;
                    command_Action.activateSound = SoundDefOf.Designate_Cancel;
                    command_Action.action = delegate
                    {
                        Action action = delegate
                        {
                            Finish();
                        };
                        action();
                    };
                    yield return command_Action;

                }

                /*
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill fake nutrition",
                    action = delegate
                    {
                        containedNutritionNotInItems = NutritionBuffer;
                    }
                };
                */
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty ALL",
                    action = delegate
                    {
                        innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
                    }
                };
                

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Write Nutrition Debug Log",
                    action = delegate
                    {
                        Log.Message("containedNutritionNotInItems = " + containedNutritionNotInItems);
                        Log.Message("NutritionStored = " + NutritionStored);
                        Log.Message("NutritionNeeded = " + NutritionNeeded);
                        Log.Message("NutritionConsumedPerDay = " + NutritionConsumedPerDay);
                        Log.Message("NutritionPercent = " + (NutritionPercent * 100f) + "%");
                        Log.Message("NutritionFromPaste = " + NutritionFromPaste);
                    }
                };
            }
        }

        private string CurrentActionString
        {
            get
            {
                if(CompAbilities != null)
                {
                    string temp = "APM.CommandCasket.Inspect.CurrentAction".Translate() + " ";
                    if (CompAbilities.IsBusy == (int)MechCasketAction.idle)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.Idle".Translate() + " ";
                        return temp;
                    }
                    if (CompAbilities.IsBusy == (int)MechCasketAction.connecting)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.Connect".Translate() + " ";
                    }
                    else if (CompAbilities.IsBusy == (int)MechCasketAction.disconnecting)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.Disconnect".Translate() + " ";
                    }
                    else if (CompAbilities.IsBusy == (int)MechCasketAction.shield)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.Shield".Translate() + " ";
                    }
                    else if (CompAbilities.IsBusy == (int)MechCasketAction.repair)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.Repair".Translate() + " ";
                    }
                    else if (CompAbilities.IsBusy == (int)MechCasketAction.openStasisContainer)
                    {
                        temp += "APM.CommandCasket.Inspect.CurrentAction.StasisContainer".Translate() + " ";
                    }
                    if (CompAbilities.HasQuedAction)
                    {
                        temp += "(+" + CompAbilities.QuedActions.Count + ")";
                    }
                    return temp;
                }
                return "";
            }
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (base.Working)
            {
                if (selectedPawn != null && innerContainer.Contains(selectedPawn))
                {
                    stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1}, {2}", "CasketContains".Translate().ToString(), selectedPawn.NameShortColored.Resolve(), selectedPawn.ageTracker.AgeBiologicalYears));
                    if(CompAbilities != null && CompAbilities.HasQuedAction)
                    {
                        stringBuilder.AppendLineIfNotEmpty().Append(CurrentActionString);
                    }
                    stringBuilder.AppendLineIfNotEmpty().Append("APM.CommandCasket.Inspection.Bandwidth".Translate() + " " + selectedPawn.mechanitor.UsedBandwidth.ToString() + " / " + selectedPawn.mechanitor.TotalBandwidth);
                }

                float biostarvationSeverityPercent = BiostarvationSeverityPercent;
                if (biostarvationSeverityPercent > 0f)
                {
                    string text = ((BiostarvationDailyOffset >= 0f) ? "+" : string.Empty);
                    stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1} ({2})", "Biostarvation".Translate(), biostarvationSeverityPercent.ToStringPercent(), "PerDay".Translate(text + BiostarvationDailyOffset.ToStringPercent())));
                }
            }
            else if (selectedPawn != null)
            {
                stringBuilder.AppendLineIfNotEmpty().Append("WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve());
            }
            stringBuilder.AppendLineIfNotEmpty().Append("Nutrition".Translate()).Append(": ")
                .Append(NutritionStored.ToStringByStyle(ToStringStyle.FloatMaxOne));
            stringBuilder.Append("/" + NutritionBuffer);
            if (base.Working)
            {
                stringBuilder.Append(" (-").Append("PerDay".Translate(NutritionConsumedPerDay.ToString("F1"))).Append(")");
            }
            return stringBuilder.ToString();
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }
            AcceptanceReport acceptanceReport = CanAcceptPawn(selPawn);
            if (acceptanceReport.Accepted)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
                {
                    SelectPawn(selPawn);
                }), selPawn, this);
            }
            else if (!acceptanceReport.Reason.NullOrEmpty())
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + acceptanceReport.Reason.CapitalizeFirst(), null);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref containedNutritionNotInItems, "containedNutrition", 0f);
            Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings", this);
            if (allowedNutritionSettings == null)
            {
                allowedNutritionSettings = new StorageSettings(this);
                if (def.building.defaultStorageSettings != null)
                {
                    allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
                }
            }
        }
    }

}
