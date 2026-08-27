using RimWorld;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using Verse.Sound;
using System.Linq;
using Verse.AI;



namespace ApexMechanoids
{
    public class  CompRemoteMechCasketAbilities  : ThingComp
    {
        public CompProperties_RemoteMechCasketAbilities Props => (CompProperties_RemoteMechCasketAbilities)props;

        public int IsBusy = (int)MechCasketAction.idle;
        public enum MechCasketAction
        {
            idle,           //0
            connecting,     //1
            disconnecting,  //2   
            repair,         //3 
            shield,          //4
            openStasisContainer //5
        }

        public Pawn User;

        public bool IsActing => IsBusy != (int)MechCasketAction.idle;
        
        public List<MechCasketQuedAction> QuedActions = new List<MechCasketQuedAction>();

        private int TicksPerHeal => Mathf.RoundToInt(1f / User.GetStatValue(StatDefOf.MechRepairSpeed) * 120f * Props.actionspeed);

        private int ticksToNextRepair = 0;

        public LocalTargetInfo curLocalTargetInfo = LocalTargetInfo.Invalid;    //invalid so it does not nullpoint

        public Thing curTarget;

        public Building curTargetBuilding => curTarget as Building;

        public Pawn curTargetPawn => curTarget as Pawn;

        private int ticksToTakeControl = 0;

        private int ticksToDisconnect = 1;

        private int actionTick = 0;

        private SimpleColor Color_Repair = SimpleColor.White;
        private SimpleColor Color_Connect = SimpleColor.Cyan;

        public float TicksForShieldcooldown = 0;

        public IEnumerable<Gizmo> GetGizmos()   //only add gizmos in here, so we can mirror them to the mechanitor!
        {
            if(User != null)
            {
                if (CanUseAbilities)
                {
                    CommandCasketAbilityGizmo abilityGizmo = new CommandCasketAbilityGizmo(parent, this);

                    /*
                    foreach (var Gizmo in GetGizmosSingles())
                    {
                        yield return Gizmo;
                    }
                    */

                    yield return abilityGizmo;
                }
                if(DebugSettings.ShowDevGizmos)
                {
                    Command_Action open_Action = new Command_Action();
                    open_Action.defaultLabel = "DEV: " + "APM.CommandCasket.Gizmo.OpenStasisContainer.Label".Translate();
                    open_Action.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/APM_OpenStasisContainer");
                    open_Action.action = delegate
                    {
                        Find.Targeter.BeginTargeting(StasisContainerTargetingParameters(), StartToHackStasisContainer, Highlight, IsStasisContainerWithContent);
                    };
                    yield return open_Action;

                    Command_Action LogQue_Action = new Command_Action();
                    LogQue_Action.defaultLabel = "DEV: Write Que in log";
                    LogQue_Action.action = delegate
                    {
                        if (!QuedActions.NullOrEmpty())
                        {
                            foreach (MechCasketQuedAction action in QuedActions)
                            {
                                Log.Message(action.targetinfo.ToString() + " -> " + action.action);
                            }
                        }
                    };
                    yield return LogQue_Action;
                }
            }

            yield break;
        }

        public IEnumerable<Gizmo> GetGizmosSingles()
        {
            #region Dis-/connect

            Command_Action remoteControll_Action = new Command_Action();
            remoteControll_Action.defaultLabel = "APM.CommandCasket.Gizmo.Connect.Label".Translate();
            remoteControll_Action.icon = ContentFinder<Texture2D>.Get(Props.textpath_Connect);
            remoteControll_Action.defaultDesc = "APM.CommandCasket.Gizmo.Connect.Desc".Translate().CapitalizeFirst();
            remoteControll_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(RemoteConnectTargetingParameters(), StartToConnect, Highlight, CanRemoteConnect);
            };
            yield return remoteControll_Action;


            Command_Action disconnect_Action = new Command_Action();
            disconnect_Action.defaultLabel = "APM.CommandCasket.Gizmo.Disconnect.Label".Translate();
            disconnect_Action.icon = ContentFinder<Texture2D>.Get(Props.textpath_Disconnect);
            disconnect_Action.defaultDesc = "APM.CommandCasket.Gizmo.Connect.Desc".Translate().CapitalizeFirst();
            disconnect_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(RemoteDisconnectTargetingParameters(), StartToDisconnect, Highlight, CanRemoteDisconnect);
            };
            yield return disconnect_Action;

            #endregion

            #region implant based

            if (HasImplantRepair())
            {
                Command_Action repair_Action = new Command_Action();
                repair_Action.defaultLabel = "APM.CommandCasket.Gizmo.Repair.Label".Translate();
                repair_Action.icon = ContentFinder<Texture2D>.Get(Props.textpath_Repair);
                repair_Action.defaultDesc = "APM.CommandCasket.Gizmo.Repair.Desc".Translate().CapitalizeFirst();
                repair_Action.action = delegate
                {
                    Find.Targeter.BeginTargeting(RemoteRepairTargetingParameters(), StartToRepair, Highlight, CanRemoteRepair);
                };
                yield return repair_Action;
            }

            if (HasImplantShield())
            {
                Command_Action shield_Action = new Command_Action();
                shield_Action.defaultLabel = GetShieldGizmoLabel();
                shield_Action.icon = GetShieldTexture();
                shield_Action.defaultDesc = "APM.CommandCasket.Gizmo.Shield.Desc".Translate().CapitalizeFirst();
                shield_Action.action = delegate
                {
                    if (TicksForShieldcooldown == 0)
                    {
                        Find.Targeter.BeginTargeting(RemoteShieldTargetingParameters(), StartToShield, Highlight, CanRemoteShield);
                    }
                    else
                    {
                        string message = "shielding is on cooldown!";
                        Messages.Message(message, User, MessageTypeDefOf.CautionInput);
                    }
                };
                yield return shield_Action;
            }

            #endregion
        }


        public void TryChangeUser(Pawn pawn)
        {
            if(pawn == null)
            {
                EndAction();
                User = pawn;
                return;
            }

            if(User != pawn)
            {  
                User = pawn; 
            }
        }

        #region QuedActions

        public void AddQuedAction(LocalTargetInfo curLocalTargetInfo, int action)
        {
            if(ActionIsValid(curLocalTargetInfo, action))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, action);
                QuedActions.Add(quedAction);
            }
        }

        public void AddQuedActionShield(LocalTargetInfo curLocalTargetInfo)
        {
            if (ActionIsValid(curLocalTargetInfo, (int)MechCasketAction.shield))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, (int)MechCasketAction.shield);
                QuedActions.Add(quedAction);
            }
        }

        public void AddQuedActionRepair(LocalTargetInfo curLocalTargetInfo)
        {
            if (ActionIsValid(curLocalTargetInfo, (int)MechCasketAction.repair))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, (int)MechCasketAction.repair);
                QuedActions.Add(quedAction);
            }
        }

        public void AddQuedActionConnect(LocalTargetInfo curLocalTargetInfo)
        {
            if (ActionIsValid(curLocalTargetInfo, (int)MechCasketAction.connecting))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, (int)MechCasketAction.connecting);
                QuedActions.Add(quedAction);
            }
        }

        public void AddQuedActionDisconnect(LocalTargetInfo curLocalTargetInfo)
        {
            if (ActionIsValid(curLocalTargetInfo, (int)MechCasketAction.disconnecting))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, (int)MechCasketAction.disconnecting);
                QuedActions.Add(quedAction);
            }
        }

        public void AddQuedActionOpenStasisContainer(LocalTargetInfo curLocalTargetInfo)
        {
            if (ActionIsValid(curLocalTargetInfo, (int)MechCasketAction.openStasisContainer))
            {
                MechCasketQuedAction quedAction = new MechCasketQuedAction(curLocalTargetInfo, (int)MechCasketAction.openStasisContainer);
                QuedActions.Add(quedAction);
            }
        }


        private bool ActionIsValid(LocalTargetInfo curLocalTargetInfo, int action)
        {
            if(curLocalTargetInfo.Pawn != null) // && action >= (int)MechCasketAction.connecting && action <= (int)MechCasketAction.shield)
            {
                if(!curLocalTargetInfo.Pawn.Dead)
                {
                    if(action == (int)MechCasketAction.connecting && curLocalTargetInfo.Pawn.GetOverseer() == User)
                    {
                        return false;
                    }
                    if (action == (int)MechCasketAction.disconnecting && curLocalTargetInfo.Pawn.GetOverseer() != User)
                    {
                        return false;
                    }
                    return true;
                }
            }
            if(action == (int)MechCasketAction.openStasisContainer)
            {
                if(curLocalTargetInfo.Thing.TryGetComp<Comp_MechanoidContainer>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ActionIsValid(MechCasketQuedAction quedAction)
        {
            if (quedAction != null && ActionIsValid(quedAction.targetinfo, quedAction.action))
            {
                return true;
            }
            return false;
        }

        public void StartQuedAction()
        {
            if (!QuedActions.NullOrEmpty())
            {
                MechCasketQuedAction quedAction = QuedActions.FirstOrDefault();

                if (ActionIsValid(quedAction))
                {
                    ForceSetTargetPawn(quedAction.targetinfo.Pawn, out LocalTargetInfo target);
                    if (quedAction.action == (int)MechCasketAction.connecting)
                    {
                        StartToConnect(target);
                    }
                    else if (quedAction.action == (int)MechCasketAction.disconnecting)
                    {
                        StartToDisconnect(target);
                    }
                    else if (quedAction.action == (int)MechCasketAction.repair)
                    {
                        StartToRepair(target);
                    }
                    else if (quedAction.action == (int)MechCasketAction.shield)
                    {
                        StartToShield(target);
                    }
                }

                QuedActions.Remove(quedAction);
            }
        }

        public bool HasQuedAction
        {
            get
            {
                if(QuedActions == null)
                {
                    QuedActions = new List<MechCasketQuedAction>();
                }

                if (!QuedActions.NullOrEmpty())
                {
                    return QuedActions.Count > 0;
                }
                return false;
            }
        }

        #endregion

        public bool IsBoosted
        {
            get
            {
                if(User.health.hediffSet.HasHediff(ApexDefsOf.APM_MechCommandCasketBoost))
                {
                    return true;
                }
                return false;
            }
        }

        public bool ShouldBeBoosted
        {
            get 
            {
                if (parent.GetStatValue(ApexDefsOf.APM_CasketBandwidth) >= 1)
                {
                    return true;
                }
                return false;
            }
        }



        public override void CompTickInterval(int delta)
        {
            if (TicksForShieldcooldown > 0)
            {
                if (IsBusy != (int)MechCasketAction.shield)
                {
                    TicksForShieldcooldown -= (1 * delta); // resets the cooldown when not shielding
                }
            }

            if (User == null)
            {
                ResetAllEffecter();
                return;
            }
            if (parent.IsHashIntervalTick(Props.TicksToCheckForHediff))
            {
                if (IsBoosted || ShouldBeBoosted)
                {
                    Hediff hediff = User.health.hediffSet.GetFirstHediffOfDef(ApexDefsOf.APM_MechCommandCasketBoost);

                    if(!ShouldBeBoosted)
                    {
                        User.health.RemoveHediff(hediff);
                    }
                    else
                    {
                        if (hediff == null)
                        {
                            hediff = User.health.AddHediff(ApexDefsOf.APM_MechCommandCasketBoost, User.health.hediffSet.GetBrain());
                        }

                        if (hediff is Hediff_CommandCasketBoost)
                        {
                            Hediff_CommandCasketBoost bandwidthHediff = (Hediff_CommandCasketBoost)hediff;

                            bandwidthHediff.BandwidthOffset = (int)parent.GetStatValue(ApexDefsOf.APM_CasketBandwidth);

                            bandwidthHediff.UpdateStats();
                        }
                    }
                }

                if (Props.HediffToGive != null)
                {
                    Hediff hediff = User.health.GetOrAddHediff(Props.HediffToGive);

                    if (hediff == null)
                    {
                        hediff = User.health.AddHediff(Props.HediffToGive, User.health.hediffSet.GetBrain());
                        hediff.Severity = 1f;
                        HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                        if (hediffComp_Link != null)
                        {
                            hediffComp_Link.drawConnection = false;
                            hediffComp_Link.other = parent;
                        }
                    }

                    HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                    if (hediffComp_Disappears != null)
                    {
                        hediffComp_Disappears.ticksToDisappear = Props.TicksToCheckForHediff + 10;
                    }
                }

            }

            // gizmo actions

            if (actionTick != 0)
            {
                actionTick += (1 * delta);

                if (IsBusy != (int)MechCasketAction.idle)
                {
                    if (IsBusy == (int)MechCasketAction.connecting)
                    {
                        if (!curLocalTargetInfo.Pawn.Dead && curLocalTargetInfo.Pawn.Map == parent.Map)
                        {
                            PawnUtility.ForceWait(curLocalTargetInfo.Pawn, 5, null, maintainPosture: true, maintainSleep: true);
                            // in tick so that mech can move again if it gets canceled
                        }

                        if (connectEffecter == null)
                        {
                            EffecterDef progressBar = EffecterDefOf.ProgressBar;
                            connectEffecter = progressBar.Spawn();
                        }
                        if (connectEffecter != null)
                        {
                            TargetInfo infoMech = new TargetInfo(thing: curLocalTargetInfo.Pawn);

                            connectEffecter.EffectTick(infoMech, TargetInfo.Invalid);
                        }

                        if(connectMote == null)
                        {
                            connectMote = ((SubEffecter_ProgressBar)connectEffecter.children[0]).mote;
                        }
                        
                        if (connectMote != null)
                        {
                            connectMote.progress = Mathf.Clamp01(1f / ticksToTakeControl * actionTick) ;
                            connectMote.offsetZ = -0.5f;
                            connectMote.alwaysShow = true;
                        }

                        if (actionTick >= ticksToTakeControl)
                        {
                            Connect(curLocalTargetInfo, User);
                            EndAction();
                        }
                    }
                    else if (IsBusy == (int)MechCasketAction.disconnecting)
                    {
                        if (actionTick >= 5)
                        {
                            Disconnect(curLocalTargetInfo, User);
                            EndAction();
                        }
                    }
                    else if (IsBusy == (int)MechCasketAction.repair)
                    {
                        if (actionTick != 0)
                        {
                            if (CanRemoteRepair(curLocalTargetInfo) && !curLocalTargetInfo.Pawn.Dead)
                            {
                                Repair(curLocalTargetInfo, User, delta);

                                if (repairEffecter == null)
                                {
                                    EffecterDef progressBar = EffecterDefOf.ProgressBar;
                                    repairEffecter = progressBar.Spawn();
                                }
                                if (repairEffecter != null)
                                {
                                    TargetInfo infoMech = new TargetInfo(thing: curLocalTargetInfo.Pawn);

                                    repairEffecter.EffectTick(infoMech, TargetInfo.Invalid);
                                }

                                if (repairMote == null)
                                {
                                    repairMote = ((SubEffecter_ProgressBar)repairEffecter.children[0]).mote;
                                }

                                if (repairMote != null)
                                {
                                    repairMote.progress = Mathf.Clamp01(1f - ((float)ticksToNextRepair / (float)TicksPerHeal));

                                    repairMote.offsetZ = -0.5f;
                                    repairMote.alwaysShow = true;
                                }

                            }
                            else
                            {
                                EndAction();
                            }
                        }
                    }
                    else if (IsBusy == (int)MechCasketAction.shield)
                    {
                        ShieldTick();
                    }
                    else if(IsBusy == (int)MechCasketAction.openStasisContainer)
                    {
                        if (hackContainerEffecter == null)
                        {
                            EffecterDef progressBar = EffecterDefOf.ProgressBar;
                            hackContainerEffecter = progressBar.Spawn();
                        }
                        if (hackContainerEffecter != null)
                        {
                            TargetInfo infoContainer = new TargetInfo(thing: curLocalTargetInfo.Thing);

                            hackContainerEffecter.EffectTick(infoContainer, TargetInfo.Invalid);
                        }
                        if (hackContainerMote == null)
                        {
                            hackContainerMote = ((SubEffecter_ProgressBar)hackContainerEffecter.children[0]).mote;
                        }
                        if (hackContainerMote != null)
                        {
                            hackContainerMote.progress = Mathf.Clamp01(1f / ticksToTakeControl * actionTick);
                            hackContainerMote.offsetZ = -0.5f;
                            hackContainerMote.alwaysShow = true;
                        }

                        if (actionTick >= ticksToTakeControl)
                        {
                            HackStasisContainer(curLocalTargetInfo, User);
                            EndAction();
                        }
                    }
                }
                else //idle
                {
                    ResetAllEffecter();
                }
            }
            else //idle but ticking without actiontick
            {
                if (HasQuedAction)
                {
                    StartQuedAction();
                }
            }
        }


        public void ResetAllEffecter()
        {
            ResetConnectEffecter();
            ResetRepairEffecter();
            ResetHackContainertEffecter();
        }

        public void ResetConnectEffecter()
        {
            if (connectEffecter != null)
            {
                connectEffecter.Cleanup();
                connectEffecter = null;
                connectMote = null;
            }
        }

        public void ResetHackContainertEffecter()
        {
            if (hackContainerEffecter != null)
            {
                hackContainerEffecter.Cleanup();
                hackContainerEffecter = null;
                hackContainerMote = null;
            }
        }

        public void ResetRepairEffecter()
        {
            if (repairEffecter != null)
            {
                repairEffecter.Cleanup();
                repairEffecter = null;
                repairMote = null;
            }
        }

        public Effecter hackContainerEffecter = null;

        public MoteProgressBar hackContainerMote = null; 

        public Effecter connectEffecter = null;

        public MoteProgressBar connectMote = null;

        public Effecter repairEffecter = null;

        public MoteProgressBar repairMote = null;
        

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            DestroyMechShield();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            DestroyMechShield();
        }


        public override void PostExposeData()
        {
            Scribe_References.Look(ref User, "User");
            Scribe_References.Look(ref curTarget, "curTarget");
            Scribe_References.Look(ref mechShield, "mechShield");

            Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair");
            Scribe_Values.Look(ref IsBusy, "IsBusy");
            Scribe_Values.Look(ref ticksToTakeControl, "ticksToTakeControl");
            Scribe_Values.Look(ref ticksToDisconnect, "ticksToDisconnect");
            Scribe_Values.Look(ref actionTick, "actionTick");
            Scribe_Values.Look(ref TicksForShieldcooldown, "TicksForShieldcooldown");
            Scribe_Collections.Look(ref QuedActions, "QuedActions", LookMode.Value);
        }

        
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (User == null && !HasATarget() && !TargetOnSameMap())
            {
                return;
            }
            if (IsBusy == (int)MechCasketAction.connecting || IsBusy == (int)MechCasketAction.disconnecting)
            {
                DrawLine(Color_Connect);
            }
            else if (IsBusy == (int)MechCasketAction.repair)
            {
                DrawLine(Color_Repair);
            }
            else if (IsBusy == (int)MechCasketAction.shield)
            {
                DrawLine(Color_Repair);
            }
            base.PostDraw();
        }



        public void Highlight(LocalTargetInfo target)      //simplified CompPlantable targeter
        {
            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlight(target);        //this is the circle over the targeted pawn!
            }
        }

        public void UpdateTargetThing(LocalTargetInfo target)
        {
            curLocalTargetInfo = target;
            curTarget = curLocalTargetInfo.Thing;
        }

        public void UpdateTargetPawn(LocalTargetInfo target)
        {
            curLocalTargetInfo = target;
            curTarget = curLocalTargetInfo.Pawn;
        }

        public void ForceSetTargetPawn(LocalTargetInfo target)
        {
            curLocalTargetInfo = target;
            curTarget = curLocalTargetInfo.Pawn;
        }

        public void ForceSetTargetThing(Thing thing, out LocalTargetInfo target)
        {
            curLocalTargetInfo = new LocalTargetInfo(thing);
            curTarget = curLocalTargetInfo.Thing;
            target = curLocalTargetInfo;
        }

        public void ForceSetTargetPawn(Pawn mech, out LocalTargetInfo target)
        {
            curLocalTargetInfo = new LocalTargetInfo(mech);
            curTarget = curLocalTargetInfo.Pawn;
            target = curLocalTargetInfo;
        }



        private bool HasATarget()
        {
            if (curTargetPawn != null)
            {
                return true;
            }
            return false;
        }

        private bool TargetOnSameMap()
        {
            if (HasATarget())
            {
                if (curTargetPawn.Map == User.Map)
                {
                    return true;
                }
            }
            return false;
        }

        public void DrawLine(SimpleColor color) //seems to no longer get called when we choose Building_MechCommandCasket
        {
            if (IsActing && TargetOnSameMap())
            {
                GenDraw.DrawLineBetween(curTargetPawn.TrueCenter(), parent.TrueCenter(), color);
                //GenDraw.DrawLineBetween(vec_target, vec_building, AltitudeLayer.BuildingBelowTop.AltitudeFor(), LineMatCyan, 3f); // if we need more control over the colors
            }
        }


        #region Actions

        private void StartAction()
        {
            actionTick = 1;
            DestroyMechShield();
        }

        public void EndAction()
        {
            actionTick = 0;
            DestroyMechShield();
            IsBusy = (int)MechCasketAction.idle;
            ResetTarget();
            ResetAllEffecter();
        }

        public void EndActionWithSound()
        {
            EndAction();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        private void ResetTarget()
        {
            curLocalTargetInfo = LocalTargetInfo.Invalid;
            curTarget = null;
        }

        #endregion


        #region TakeControlRemote 

        public TargetingParameters RemoteConnectTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetHumans = false,
                canTargetMechs = true,
                canTargetAnimals = false,
                canTargetLocations = false,
                validator = (TargetInfo x) => CanRemoteConnect((LocalTargetInfo)x)
            };
        }

        public bool CanRemoteConnect(LocalTargetInfo target)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                return false;
            }

            if (pawn.GetOverseer() != User && pawn.IsColonyMech && !pawn.Dead)
            {
                // has the bandwith to take control
                if (pawn.GetStatValue(StatDefOf.BandwidthCost) <= User.mechanitor.TotalBandwidth - User.mechanitor.UsedBandwidth)
                {
                    return true;
                }
            }
            return false;
        }

        public void StartToConnect(LocalTargetInfo target)
        {
            UpdateTargetPawn(target);

            ticksToTakeControl = Mathf.RoundToInt(target.Pawn.GetStatValue(StatDefOf.ControlTakingTime) * 60f);
            PawnUtility.ForceWait(target.Pawn, ticksToTakeControl, null, maintainPosture: true, maintainSleep: true);
            StartAction();
            IsBusy = (int)MechCasketAction.connecting;
        }

        private void Connect(LocalTargetInfo target, Pawn overseer)
        {
            SoundDef sound = SoundDefOf.ControlMech_Complete;
            sound.PlayOneShot(new TargetInfo(curTargetPawn.Position, curTargetPawn.Map));
            ResetTarget();
            Pawn mech = target.Pawn;
            if (mech.Faction != User.Faction) //failsafe
            {
                mech.SetFaction(User.Faction);
            }
            mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
            overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        }

        #endregion


        #region DisconnectRemote 

        public TargetingParameters RemoteDisconnectTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetHumans = false,
                canTargetMechs = true,
                canTargetAnimals = false,
                canTargetLocations = false,
                validator = (TargetInfo x) => CanRemoteDisconnect((LocalTargetInfo)x)
            };
        }

        public bool CanRemoteDisconnect(LocalTargetInfo target)
        {
            Pawn mech = target.Pawn;
            if (mech == null)
            {
                return false;
            }
            if (mech.GetOverseer() == User && mech.IsColonyMech && !mech.Dead) //mech.Downed && mech.IsAttacking()) ??
            {
                return true;
            }
            return false;
        }

        public void StartToDisconnect(LocalTargetInfo target)
        {
            UpdateTargetPawn(target);
            ticksToDisconnect = (int)(target.Pawn.GetStatValue(StatDefOf.ControlTakingTime) * 60);

            PawnUtility.ForceWait(target.Pawn, ticksToDisconnect, User, maintainPosture: true, maintainSleep: true);
            StartAction();
            IsBusy = (int)MechCasketAction.disconnecting;
        }

        private void Disconnect(LocalTargetInfo target, Pawn overseer)
        {
            ResetTarget();
            Pawn mech = target.Pawn;
            if (target.Pawn.Faction != User.Faction)     //failsafe
            {
                mech.SetFaction(User.Faction);
            }
            if (target.Pawn.drafter.Drafted)
            {
                target.Pawn.drafter.Drafted = !target.Pawn.drafter.Drafted;
            }
            if (mech.GetOverseer() == User)
            {
                overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
            }
        }

        #endregion


        #region RepairRemote 

        public TargetingParameters RemoteRepairTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetHumans = false,
                canTargetMechs = true,
                canTargetAnimals = false,
                canTargetLocations = false,
                validator = (TargetInfo x) => CanRemoteRepair((LocalTargetInfo)x)
            };
        }

        public bool CanRemoteRepair(LocalTargetInfo target)
        {
            Pawn mech = target.Pawn;
            if (mech == null)
            {
                return false;
            }
            if (mech.IsColonyMech && !mech.Dead && MechRepairUtility.CanRepair(mech))
            {
                return true;
            }
            return false;
        }

        public void StartToRepair(LocalTargetInfo target)
        {
            UpdateTargetPawn(target);
            StartAction();
            IsBusy = (int)MechCasketAction.repair;
        }

        private void ForceWaitRemoteRepair(LocalTargetInfo target)
        {
            //deactivated, so mechs can still do combat
            //PawnUtility.ForceWait(target.Pawn, TicksPerHeal - 1, Cryohediff.pawn, maintainPosture: true, maintainSleep: true);  // -1 only in here! for ticks so rounding does not fuck up things
        }

        private void Repair(LocalTargetInfo target, Pawn overseer, int delta)
        {
            Pawn mech = target.Pawn;

            ticksToNextRepair--;
            if (ticksToNextRepair <= 0)
            {
                if(Props.repairCostsEnergy)
                {
                    mech.needs.energy.CurLevel -= mech.GetStatValue(StatDefOf.MechEnergyLossPerHP) * (float)delta;
                }
                MechRepairUtility.RepairTick(mech, delta);
                ticksToNextRepair = TicksPerHeal;

                if (CanRemoteRepair(curLocalTargetInfo))
                {
                    ForceWaitRemoteRepair(curLocalTargetInfo);
                }
            }
            if (overseer.skills != null && HasCraftSkill())
            {
                overseer.skills.Learn(SkillDefOf.Crafting, Props.repairExp * (float)delta);
            }


        }

        #endregion


        #region ShieldRemote 


        private MechShield mechShield;

        private CompProjectileInterceptor projectileInterceptor;

        private CompProjectileInterceptor ProjectileInterceptor
        {
            get
            {
                if (projectileInterceptor == null && mechShield != null)
                {
                    projectileInterceptor = mechShield.GetComp<CompProjectileInterceptor>();
                }
                return projectileInterceptor;
            }
        }

        public TargetingParameters RemoteShieldTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetHumans = false,
                canTargetMechs = true,
                canTargetAnimals = false,
                canTargetLocations = false,
                validator = (TargetInfo x) => CanRemoteShield((LocalTargetInfo)x)
            };
        }

        public bool CanRemoteShield(LocalTargetInfo target)     //ai choosing should not be in here
        {
            Pawn mech = target.Pawn;
            if (mech == null)
            {
                return false;
            }
            if (mech.IsColonyMech && !mech.Dead)
            {
                return true;
            }
            return false;
        }

        public void StartToShield(LocalTargetInfo target)
        {
            UpdateTargetPawn(target);
            StartAction();
            IsBusy = (int)MechCasketAction.shield;
        }

        private void ShieldTick()      //this is NOT a tick action, instead a one time thing
        {
            if (CanRemoteShield(curLocalTargetInfo) && !curLocalTargetInfo.Pawn.Dead)
            {
                if (projectileInterceptor != null)
                {
                    if (projectileInterceptor.currentHitPoints <= 0.9)      //ends if mechshield is down, needs to be more than 0 !!!
                    {
                        EndAction();
                    }
                }
                else if (mechShield == null)
                {
                    Shield(curLocalTargetInfo, User);
                }
                
            }
        }

        private void Shield(LocalTargetInfo target, Pawn overseer)      //this is NOT a tick action, instead a one time thing
        {
            Pawn mech = target.Pawn;

            mechShield = (MechShield)GenSpawn.Spawn(ThingDefOf.MechShield, mech.Position, mech.Map);
            mechShield.SetTarget(mech);
            int num = (int)overseer.GetStatValue(StatDefOf.MechRemoteShieldEnergy);
            ProjectileInterceptor.maxHitPointsOverride = num;
            ProjectileInterceptor.currentHitPoints = num;

            SoundDef sound = ApexDefsOf.ShieldMech_Start;
            sound.PlayOneShot(new TargetInfo(curTargetPawn.Position, curTargetPawn.Map));

            TicksForShieldcooldown = Props.Shieldcooldown;
        }

        private void DestroyMechShield()
        {
            if (mechShield != null)
            {
                if (!mechShield.Destroyed)
                {
                    mechShield.Destroy();
                }
                mechShield = null;
                projectileInterceptor = null;   //removes nullpointer??
                if (HasATarget())
                {
                    SoundDef sound = ApexDefsOf.ShieldMech_Complete;
                    sound.PlayOneShot(new TargetInfo(curTargetPawn.Position, curTargetPawn.Map));
                }
            }
            if (IsBusy == (int)MechCasketAction.shield)
            {
                TicksForShieldcooldown = Props.Shieldcooldown;
            }
        }


        #endregion


        #region OpenStasisContainer 

        public TargetingParameters StasisContainerTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = false,
                canTargetBuildings = true,
                canTargetHumans = false,
                canTargetMechs = true,
                canTargetAnimals = false,
                canTargetLocations = false,
                validator = (TargetInfo x) => IsStasisContainerWithContent((LocalTargetInfo)x)
            };
        }

        public bool IsStasisContainerWithContent(LocalTargetInfo target)
        {
            Comp_MechanoidContainer comp = target.thingInt?.TryGetComp<Comp_MechanoidContainer>();

            if (comp != null && !comp.isEmpty)
            {
                return true;
            }
            return false;
        }

        public void StartToHackStasisContainer(LocalTargetInfo target)
        {
            UpdateTargetThing(target);
            
            Comp_MechanoidContainer comp = target.thingInt?.TryGetComp<Comp_MechanoidContainer>();
            if(comp != null)
            {
                try
                {
                    ticksToTakeControl = comp.TicksToActivate / 10; //Mathf.RoundToInt(comp.mechKind.race.GetStatValueAbstract(StatDefOf.ControlTakingTime) * 60f);
                    StartAction();
                    IsBusy = (int)MechCasketAction.openStasisContainer;
                }
                catch 
                {
                    Utils.LogError("Could not find mech inside mechanoid stasis container, skipping...");
                }
            }
        }

        private void HackStasisContainer(LocalTargetInfo target, Pawn overseer)
        {
            Comp_MechanoidContainer comp =  target.thingInt?.TryGetComp<Comp_MechanoidContainer>();

            if(comp != null)
            {
                comp.OnInteracted(overseer);
            }
        }

        #endregion


        public bool HasImplantRepair()
        {
            Hediff repairhediff = User?.health?.hediffSet?.GetFirstHediffOfDef(ApexDefsOf.RemoteRepairerImplant);

            if (repairhediff != null)
            {
                return true;
            }
            return false;
        }

        public bool HasImplantShield()
        {
            Hediff shieldhediff = User?.health?.hediffSet?.GetFirstHediffOfDef(ApexDefsOf.RemoteShielderImplant);
            if (shieldhediff != null)
            {
                return true;
            }

            return false;
        }

        private bool HasCraftSkill()
        {
            if (User.WorkTagIsDisabled(WorkTags.Crafting))
            {
                return false;
            }
            return true;
        }

        #region Gizmostuff 

        public Texture2D GetShieldTexture()
        {
            if (TicksForShieldcooldown == 0)
            {
                return ContentFinder<Texture2D>.Get(Props.textpath_Shield);
            }
            else
            {
                return ContentFinder<Texture2D>.Get(Props.textpath_ShieldCooldown);
            }
        }


        public string GetShieldGizmoLabel()
        {
            return "APM.CommandCasket.Gizmo.Shield.Label".Translate().CapitalizeFirst();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            List<Gizmo> list = GetGizmos().ToList();      //only add gizmos in GetGizmos, so we can mirror them to the mechanitor!

            foreach (Gizmo g in list)
            {
                yield return g;
            }
        }

        #endregion


        public bool CanUseAbilities
        { 
            get 
            {
                return Utils.IsUplinkActiveFor(User);
            } 
        }


    }

}
