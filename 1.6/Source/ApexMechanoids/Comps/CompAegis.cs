using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using static RimWorld.MechClusterSketch;

namespace ApexMechanoids
{
    public class CompAegis : ThingComp
    {
        private bool shieldsDamaged = false;
        private int ticksSinceDamage = 0;
        private int ticksSinceRegen = 0;
        public int steelDeliveredForRepair = 0;
        private const int CompTickRareInterval = 250;

        public CompProperties_Aegis Props => (CompProperties_Aegis)props;

        private int RegenerationIntervalTicks => (int)(Props.regenerationIntervalSeconds * 60f);
        private int RegenerationDelayTicks => (int)(Props.regenerationDelaySeconds * 60f);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shieldsDamaged, "shieldsDamaged", false);
            Scribe_Values.Look(ref ticksSinceDamage, "ticksSinceDamage", 0);
            Scribe_Values.Look(ref ticksSinceRegen, "ticksSinceRegen", 0);
            Scribe_Values.Look(ref steelDeliveredForRepair, "steelDeliveredForRepair", 0);
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            if (totalDamageDealt <= 0f)
                return;

            CheckShieldDamage(parent as Pawn);
        }

        private void CheckShieldDamage(Pawn pawn)
        {
            if (ShieldsDamaged(pawn) || ShieldsMissing(pawn))
            {
                shieldsDamaged = true;
                ticksSinceDamage = 0;
                ticksSinceRegen = 0;
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            Pawn pawn = parent as Pawn;

            UpdateShieldStatus(pawn);

            if (shieldsDamaged)
            {
                ticksSinceRegen += CompTickRareInterval;
                ticksSinceDamage += CompTickRareInterval;
                if (ticksSinceDamage >= RegenerationDelayTicks && ticksSinceRegen >= RegenerationIntervalTicks)
                {
                    if (RegenerateShields(pawn))
                    {
                        shieldsDamaged = false;
                        ticksSinceDamage = 0;
                        ticksSinceRegen = 0;
                    }
                    else
                    {
                        ticksSinceRegen = 0;
                    }
                }
            }
        }

        private void UpdateShieldStatus(Pawn pawn)
        {
            if (!ShieldsDamaged(pawn) && !ShieldsMissing(pawn))
            {
                shieldsDamaged = false;
                ticksSinceDamage = 0;
                ticksSinceRegen = 0;
            }
        }

        private bool RegenerateShields(Pawn pawn)
        {
            var shieldParts = pawn.RaceProps.body.AllParts.Where(part => part.def == ApexDefsOf.APM_AegisShield);

            foreach (var shieldPart in shieldParts)
            {
                if (ShieldMissing(pawn, shieldPart))
                {
                    pawn.health.RemoveHediff(pawn.health.hediffSet.GetMissingPartFor(shieldPart));

                    float maxHealth = shieldPart.def.hitPoints;
                    float damageAmount = maxHealth * 0.95f;

                    HediffDef hediffDefFromDamage = HealthUtility.GetHediffDefFromDamage(DamageDefOf.Crush, pawn, shieldPart);
                    Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(hediffDefFromDamage, pawn, shieldPart);
                    injury.Severity = damageAmount;
                    pawn.health.AddHediff(injury, shieldPart);

                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);

                    break;
                }

                if (ShieldDamaged(pawn, shieldPart))
                {
                    var injuries = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(injury => injury.Part == shieldPart)
                    .ToList();

                    var selectedInjury = injuries.First();
                    float healAmount = Props.regenerationAmount;
                    selectedInjury.Heal(healAmount);

                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);
                    break;
                }
            }

            return !ShieldsDamaged(pawn) && !ShieldsMissing(pawn);
        }

        private List<Thing> FindAvailableSteel(Pawn pawn, int neededAmount)
        {
            List<Thing> foundSteel = new List<Thing>();
            int totalFound = 0;

            List<Thing> allSteel = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Steel)
                .Where(t => !t.IsForbidden(pawn) && pawn.CanReserve(t))
                .OrderBy(t => t.Position.DistanceTo(pawn.Position))
                .ToList();

            foreach (Thing steel in allSteel)
            {
                if (totalFound >= neededAmount)
                    break;

                foundSteel.Add(steel);
                totalFound += steel.stackCount;
            }

            return foundSteel;
        }

        private bool ShieldsMissing(Pawn pawn)
        {
            var shieldParts = pawn.RaceProps.body.AllParts.Where(part => part.def == ApexDefsOf.APM_AegisShield);
            return shieldParts.Any(shieldPart => pawn.health.hediffSet.PartIsMissing(shieldPart));
        }

        private bool ShieldsDamaged(Pawn pawn)
        {
            var shieldParts = Utils.GetNonMissingBodyParts(pawn, ApexDefsOf.APM_AegisShield);
            var injuredParts = pawn.health.hediffSet.GetInjuredParts();

            return shieldParts.Any(shieldPart => injuredParts.Contains(shieldPart));
        }

        private bool ShieldDamaged(Pawn pawn, BodyPartRecord shield)
        {
            return pawn.health.hediffSet.GetInjuredParts().Contains(shield);
        }

        private bool ShieldMissing(Pawn pawn, BodyPartRecord shield)
        {
            return pawn.health.hediffSet.PartIsMissing(shield);
        }
        
        /*public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
       Pawn pawn = parent as Pawn;

       if (pawn != null && pawn.Faction == Faction.OfPlayer && shieldsDamaged)
       {
           Command_Action repairCommand = new Command_Action
           {
               defaultLabel = "APM.RepairAegisShields.Label".Translate(),
               defaultDesc = "APM.RepairAegisShields.Desc".Translate(Props.steelRequiredForRepair),
               icon = Assets.shieldRepairIcon,
               action = delegate
               {
                   List<FloatMenuOption> options = new List<FloatMenuOption>();

                   foreach (Pawn colonist in pawn.Map.mapPawns.FreeColonistsSpawned)
                   {
                       if (colonist.workSettings != null && colonist.workSettings.WorkIsActive(WorkTypeDefOf.Crafting))
                       {
                           Pawn localColonist = colonist;
                           Pawn localMech = pawn;

                           if (!colonist.CanReach(pawn, PathEndMode.Touch, Danger.Deadly))
                           {
                               options.Add(new FloatMenuOption(colonist.LabelShort + " " + "APM.CannotReach".Translate(), null));
                               continue;
                           }

                           List<Thing> availableSteel = FindAvailableSteel(localColonist, Props.steelRequiredForRepair);
                           int totalAvailable = availableSteel.Sum(t => t.stackCount);

                           if (totalAvailable < Props.steelRequiredForRepair)
                           {
                               options.Add(new FloatMenuOption(
                                   colonist.LabelShort + " " + "APM.NeedSteel".Translate(Props.steelRequiredForRepair, totalAvailable),
                                   null
                               ));
                               continue;
                           }

                           options.Add(new FloatMenuOption(colonist.LabelShort, delegate
                           {
                               List<Thing> steelToHaul = FindAvailableSteel(localColonist, Props.steelRequiredForRepair);

                               if (steelToHaul.Count == 0)
                               {
                                   Messages.Message("APM.NoSteelAvailable".Translate(), MessageTypeDefOf.RejectInput);
                                   return;
                               }

                               Job repairJob = JobMaker.MakeJob(ApexDefsOf.APM_RepairAegisShields, localMech);
                               repairJob.count = Props.steelRequiredForRepair;

                               repairJob.targetQueueA = new List<LocalTargetInfo>();
                               foreach (Thing steel in steelToHaul)
                               {
                                   repairJob.targetQueueA.Add(steel);
                                   localColonist.Reserve(steel, repairJob);
                               }

                               localColonist.jobs.TryTakeOrderedJob(repairJob, JobTag.Misc);

                               Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat);
                               waitJob.expiryInterval = 99999;
                               localMech.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);
                           }));
                       }
                   }

                   if (options.Count == 0)
                   {
                       options.Add(new FloatMenuOption("APM.NoAvailableColonists".Translate(), null));
                   }

                   Find.WindowStack.Add(new FloatMenu(options));
               }
           };

           yield return repairCommand;
       }
   }*/
    }
}