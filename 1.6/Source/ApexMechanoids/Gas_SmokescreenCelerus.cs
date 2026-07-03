using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class Gas_SmokescreenCelerus : Gas
    {


        private DefModExtension_CelerusGas modExtension => def.GetModExtension<DefModExtension_CelerusGas>();

        private int effectDelay;
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad: true);
            destroyTick = Find.TickManager.TicksGame + def.gas.expireSeconds.RandomInRange.SecondsToTicks();
            effectDelay = modExtension.effectDelay.RandomInRange;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref destroyTick, "destroyTick", 0);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Destroyed)
            {
                if (this.IsHashIntervalTick(120,delta))
                {
                    Map map = base.Map;
                    IntVec3 position = base.Position;
                    List<Thing> thingList = position.GetThingList(map);
                    if (thingList.Count <= 0)
                    {
                        return;
                    }
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        if (thingList[i] is Pawn)
                        {
                            Pawn pawn = thingList[i] as Pawn;
                            if (pawn != null && !pawn.Dead && pawn.Position == position)
                            {
                                if (!modExtension.immuneThingDefs.NullOrEmpty())
                                {
                                    if (modExtension.immuneThingDefs.Contains(pawn.def))
                                    {
                                        if (modExtension.hediffToImmunePawn != null)
                                        {
                                            GiveHediff(pawn,modExtension.hediffToImmunePawn);
                                        }
                                        continue;
                                    }
                                }
                                if (modExtension.hediffToAffectedPawn != null)
                                {
                                    GiveHediff(pawn,modExtension.hediffToAffectedPawn);
                                }
                                DoDamage(pawn);
                            }
                        }
                    }
                }

            }
        }
        public override void Tick()
        {
            if (destroyTick <= Find.TickManager.TicksGame)
            {
                Destroy();
            }
            graphicRotation += graphicRotationSpeed;
            if (!Destroyed)
            {
                if (this.IsHashIntervalTick(effectDelay))
                {
                    DoEffect();
                }
            }
        }
        private void GiveHediff(Pawn pawn, HediffDef hediffDef)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                hediff.Severity += modExtension.severityPerTrigger;
                return;
            }
            else
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                hediff.Severity = modExtension.initialSeverity;
                pawn.health.AddHediff(hediff);
            }
        }

        private void DoEffectOn(IntVec3 pos)
        {
            if (!modExtension.fleckDefs.NullOrEmpty())
            {
                FleckMaker.Static(pos, MapHeld, modExtension.fleckDefs.RandomElement());
            }
            if (modExtension.effecterDef != null)
            {
                Effecter effecter = modExtension.effecterDef.Spawn(pos, MapHeld);
                effecter.Cleanup();
            }
            if (modExtension.soundDef != null)
            {
                modExtension.soundDef.PlayOneShot(SoundInfo.InMap(this));
            }
        }
        private void DoEffect()
        {
            if (Rand.Chance(0.5f))
            {
                if (!modExtension.fleckDefs.NullOrEmpty())
                {
                    FleckMaker.Static(PositionHeld, MapHeld, modExtension.fleckDefs.RandomElement());
                }
                if (modExtension.effecterDef != null)
                {
                    Effecter effecter = modExtension.effecterDef.Spawn(PositionHeld, MapHeld);
                    effecter.Cleanup();
                }
            }
            if (modExtension.soundDef != null)
            {
                if (Rand.Chance(modExtension.soundChance))
                {
                    modExtension.soundDef.PlayOneShot(SoundInfo.InMap(this));
                }
            }
        }
        private void DoDamage(Pawn pawn)
        {
            DamageInfo damageInfo = new DamageInfo(modExtension.damageDef, modExtension.amount.RandomInRange, instigator: this);
            pawn.TakeDamage(damageInfo);
            if (modExtension.isDoEffectWhenDamagePawn)
            {
                DoEffectOn(pawn.Position);
            }
        }
    }
}