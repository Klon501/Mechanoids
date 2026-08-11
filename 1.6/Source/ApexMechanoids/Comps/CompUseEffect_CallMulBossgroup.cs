using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ApexMechanoids
{
    public class CompUseEffect_CallMulBossgroup : CompUseEffect
    {
        private Effecter prepareEffecter;

        public new CompProperties_Useable_CallMulBossgroup Props => (CompProperties_Useable_CallMulBossgroup)props;
        public DefModExtension_Incident_CallMulBossgroup Ext => cachedExt ?? (cachedExt = Props.incidentDef.GetModExtension<DefModExtension_Incident_CallMulBossgroup>());
        private DefModExtension_Incident_CallMulBossgroup cachedExt;

        private List<string> tmpEntries = new List<string>();

        public bool ShouldSendSpawnLetter
        {
            get
            {
                if (Props.spawnLetterLabelKey.NullOrEmpty() || Props.spawnLetterTextKey.NullOrEmpty())
                {
                    return false;
                }
                if (!MechanitorUtility.AnyMechanitorInPlayerFaction())
                {
                    return false;
                }
                if (Find.BossgroupManager.lastBossgroupCalled > 0)
                {
                    return false;
                }
                return true;
            }
        }

        public string LeaderName
        {
            get
            {
                if (Props.leaderName.NullOrEmpty())
                {
                    return Ext.bosses.First().label;
                }
                return Props.leaderName;
            }
        }

        public int BossMultPerThreat(float points)
        {
            IncidentWorker_GroupBossAssault groupBossAssault = Props.incidentDef.Worker as IncidentWorker_GroupBossAssault;
            return groupBossAssault.BossMultPerThreat(points);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            if (Props.effecterDef != null)
            {
                Effecter obj = new Effecter(Props.effecterDef);
                obj.Trigger(new TargetInfo(parent.Position, parent.Map), TargetInfo.Invalid);
                obj.Cleanup();
            }
            prepareEffecter?.Cleanup();
            prepareEffecter = null;
            CallBossgroup();
        }

        private void CallBossgroup()
        {
            Find.BossgroupManager.lastBossgroupCalled = Find.TickManager.TicksGame;
            IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Props.incidentDef.category, parent.Map);
            incidentParms.forced = true;
            incidentParms.faction = Faction.OfMechanoids;
            Find.Storyteller.incidentQueue.Add(Props.incidentDef, Find.TickManager.TicksGame + Rand.Range(60000, 180000), incidentParms);
        }

        public override TaggedString ConfirmMessage(Pawn p)
        {
            tmpEntries.Clear();
            foreach (PawnKindDef bossKindDef in Ext.bosses)
            {
                tmpEntries.Add(GenLabel.BestKindLabel(bossKindDef, Gender.None).CapitalizeFirst() + " x" + BossMultPerThreat(StorytellerUtility.DefaultThreatPointsNow(parent.Map)));
            }
            TaggedString bossList = tmpEntries.ToLineList("  - ");
            tmpEntries.Clear();
            foreach (PawnGenOption escort in Ext.escorts)
            {
                tmpEntries.Add(GenLabel.BestKindLabel(escort.kind, Gender.None).CapitalizeFirst());
            }
            TaggedString escortList = tmpEntries.ToLineList("  - ");
            return "APM.CallMulBossgroupBossgroupWarningDialog".Translate(LeaderName.Named("LEADER"), bossList.Named("BOSS"), escortList.Named("ESCORT"));
        }

        public override void PrepareTick()
        {
            if (Props.prepareEffecterDef != null && prepareEffecter == null)
            {
                prepareEffecter = Props.prepareEffecterDef.Spawn(parent.Position, parent.MapHeld);
            }
            prepareEffecter?.EffectTick(parent, TargetInfo.Invalid);
        }

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (Faction.OfMechanoids == null || Faction.OfMechanoids.deactivated)
            {
                return "MechsDisabled".Translate();
            }
            if (!MechanitorUtility.IsMechanitor(p))
            {
                return "RequiresMechanitor".Translate();
            }
            return CanResolve(p);
        }

        public virtual AcceptanceReport CanResolve(Pawn caller)
        {
            int lastBossgroupCalled = Find.BossgroupManager.lastBossgroupCalled;
            int num = Find.TickManager.TicksGame - lastBossgroupCalled;
            if (num < 120000)
            {
                return "BossgroupAvailableIn".Translate((120000 - num).ToStringTicksToPeriod());
            }
            PawnKindDef pendingBossgroup = CallBossgroupUtility.GetPendingBossgroup();
            if (pendingBossgroup != null)
            {
                return "BossgroupIncoming".Translate(pendingBossgroup.label);
            }
            return true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!ModLister.CheckBiotech("Call bossgroup"))
            {
                parent.Destroy();
            }
            else if (!respawningAfterLoad && ShouldSendSpawnLetter)
            {
                SendBossgroupDetailsLetter(Props.spawnLetterLabelKey, Props.spawnLetterTextKey, parent.def);
            }
        }

        public void SendBossgroupDetailsLetter(string labelKey, string textKey, ThingDef parent)
        {
            List<ThingDef> list = new List<ThingDef> { parent };
            foreach (PawnKindDef bossKindDef in Ext.bosses)
            {
                list.AddRange(bossKindDef.race.killedLeavingsPlayerHostile.Select((ThingDefCountClass t) => t.thingDef));
            }
            Find.LetterStack.ReceiveLetter(FormatLetterLabel(labelKey), FormatLetterText(textKey, parent), LetterDefOf.NeutralEvent, null, null, null, list);
        }
        public string FormatLetterLabel(string label)
        {
            return label.Translate(LeaderName.Named("LEADER"));
        }

        public string FormatLetterText(string text, ThingDef parent)
        {
            string arg = Ext.bosses.SelectMany(pkd => pkd.race.killedLeavingsPlayerHostile.Select((ThingDefCountClass r) => r.Label + " x" + r.count)).ToLineList("- ");
            return text.Translate(NamedArgumentUtility.Named(parent.label, "PARENT"), LeaderName.Named("LEADER"), arg.Named("REWARDSLIST"));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "DEV: Call with Points";
            command_Action.action = delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                List<int> values = new List<int> { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000 };
                foreach (int value in values)
                {
                    list.Add(new FloatMenuOption(value.ToString(), delegate
                    {
                        IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Props.incidentDef.category, parent.Map);
                        incidentParms.forced = true;
                        incidentParms.faction = Faction.OfMechanoids;
                        incidentParms.points = value;
                        Find.Storyteller.incidentQueue.Add(Props.incidentDef, Find.TickManager.TicksGame + Rand.Range(2500, 7500), incidentParms);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            };
            yield return command_Action;
        }
    }
}
