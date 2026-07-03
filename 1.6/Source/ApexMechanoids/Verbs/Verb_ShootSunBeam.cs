using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class Verb_ShootSunBeam : Verb_ShootBeam
    {
        private Vector3 LasttargetPosition;
        private int min => Mathf.RoundToInt(verbProps.minRange);

        public new Vector3 InterpolatedPosition
        {
            get
            {
                Vector3 val;
                if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
                {
                    val = LasttargetPosition - initialTargetPosition;
                }
                else
                {
                    LasttargetPosition = base.CurrentTarget.CenterVector3;
                    val = LasttargetPosition - initialTargetPosition;
                }
                return Vector3.Lerp(path[Mathf.Max(burstShotsLeft - min, 0)], path[Mathf.Min(Mathf.Max(burstShotsLeft + 1 - min, 1), path.Count - 1 - min)], ShotProgress) + val;
            }
        }

        public override float? AimAngleOverride
        {
            get
            {
                if (state != VerbState.Bursting)
                {
                    return null;
                }
                return (InterpolatedPosition - caster.DrawPos).AngleFlat();
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            verbProps.DrawRadiusRing(caster.Position, this);
            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlight(target);
                DrawHighlightFieldRadiusAroundTarget(target);
            }
            if (target == null)
            {
                return;
            }
            CalculatePath(target.CenterVector3, tmpPath, tmpPathCells, addRandomOffset: false);
            foreach (IntVec3 tmpPathCell in tmpPathCells)
            {
                ShootLine resultingLine;
                bool flag = TryFindShootLineFromTo(caster.Position, target, out resultingLine);
                if ((verbProps.stopBurstWithoutLos && !flag) || !TryGetHitCell(resultingLine.Source, tmpPathCell, out var hitCell))
                {
                    continue;
                }
                foreach (IntVec3 item in GenRadial.RadialCellsAround(hitCell, 2f, useCenter: true).InRandomOrder())
                {
                    tmpHighlightCells.Add(item);
                }
                if (!verbProps.beamHitsNeighborCells)
                {
                    continue;
                }
                foreach (IntVec3 beamHitNeighbourCell in GetBeamHitNeighbourCells(resultingLine.Source, hitCell))
                {
                    if (tmpHighlightCells.Contains(beamHitNeighbourCell))
                    {
                        continue;
                    }
                    foreach (IntVec3 item2 in GenRadial.RadialCellsAround(beamHitNeighbourCell, 2f, useCenter: true).InRandomOrder())
                    {
                        tmpSecondaryHighlightCells.Add(item2);
                    }
                }
            }
            tmpSecondaryHighlightCells.RemoveWhere((IntVec3 x) => tmpHighlightCells.Contains(x));
            if (tmpHighlightCells.Any())
            {
                GenDraw.DrawFieldEdges(tmpHighlightCells.ToList(), (Color)((verbProps.highlightColor) ?? Color.white));
            }
            if (tmpSecondaryHighlightCells.Any())
            {
                GenDraw.DrawFieldEdges(tmpSecondaryHighlightCells.ToList(), (Color)((verbProps.secondaryHighlightColor) ?? Color.white));
            }
            tmpHighlightCells.Clear();
            tmpSecondaryHighlightCells.Clear();
        }

        public override bool TryCastShot()
        {
            ShootLine resultingLine;
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                resultingLine = new ShootLine(caster.Position, LasttargetPosition.ToIntVec3());
            }
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }
            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
            }
            lastShotTick = Find.TickManager.TicksGame;
            ticksToNextPathStep = base.TicksBetweenBurstShots;
            IntVec3 targetCell = InterpolatedPosition.Yto0().ToIntVec3();
            if (!TryGetHitCell(resultingLine.Source, targetCell, out var hitCell))
            {
                return true;
            }
            HitCell(hitCell, resultingLine.Source);
            if (verbProps.beamHitsNeighborCells)
            {
                hitCells.Add(hitCell);
                foreach (IntVec3 beamHitNeighbourCell in GetBeamHitNeighbourCells(resultingLine.Source, hitCell))
                {
                    if (!hitCells.Contains(beamHitNeighbourCell))
                    {
                        float damageFactor = (pathCells.Contains(beamHitNeighbourCell) ? 1f : 0.5f);
                        HitCell(beamHitNeighbourCell, resultingLine.Source, damageFactor);
                        hitCells.Add(beamHitNeighbourCell);
                    }
                }
            }
            return true;
        }

        public override void BurstingTick()
        {
            ticksToNextPathStep--;
            Vector3 val = InterpolatedPosition;
            IntVec3 intVec = val.ToIntVec3();
            Vector3 val2 = InterpolatedPosition - caster.Position.ToVector3Shifted();
            float num = val2.MagnitudeHorizontal();
            Vector3 val3 = val2.Yto0();
            Vector3 normalized = ((Vector3)(val3)).normalized;
            IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(caster.Position, intVec, (IntVec3 c) => c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
            if (intVec2.IsValid)
            {
                num -= (intVec - intVec2).LengthHorizontal;
                val = caster.Position.ToVector3Shifted() + normalized * num;
                intVec = val.ToIntVec3();
            }
            Vector3 offsetA = normalized * verbProps.beamStartOffset;
            Vector3 val4 = val - intVec.ToVector3Shifted();
            if (mote != null)
            {
                mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map), new TargetInfo(intVec, caster.Map), offsetA, val4);
                mote.Maintain();
            }
            if (verbProps.beamGroundFleckDef != null && Rand.Chance(verbProps.beamFleckChancePerTick))
            {
                FleckMaker.Static(val, caster.Map, verbProps.beamGroundFleckDef);
            }
            if (endEffecter == null && verbProps.beamEndEffecterDef != null)
            {
                endEffecter = verbProps.beamEndEffecterDef.Spawn(intVec, caster.Map, val4);
            }
            if (endEffecter != null)
            {
                endEffecter.offset = val4;
                endEffecter.EffectTick(new TargetInfo(intVec, caster.Map), TargetInfo.Invalid);
                endEffecter.ticksLeft--;
            }
            if (verbProps.beamLineFleckDef != null)
            {
                float num2 = 1f * num;
                for (int i = 0; (float)i < num2; i++)
                {
                    if (Rand.Chance(verbProps.beamLineFleckChanceCurve.Evaluate((float)i / num2)))
                    {
                        Vector3 val5 = (float)i * normalized - normalized * Rand.Value + normalized / 2f;
                        FleckMaker.Static(caster.Position.ToVector3Shifted() + val5, caster.Map, verbProps.beamLineFleckDef);
                    }
                }
            }
            if (sustainer != null && !sustainer.Ended)
            {
                sustainer.Maintain();
            }
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            castTarg = verbProps.beamTargetsGround ? ((LocalTargetInfo)castTarg.Cell) : castTarg;
            if (caster == null)
            {
                Log.Error("Verb " + GetUniqueLoadID() + " needs caster to work (possibly lost during saving/loading).");
                return false;
            }
            if (!caster.Spawned)
            {
                return false;
            }
            if (state == VerbState.Bursting || !CanHitTarget(castTarg))
            {
                return false;
            }
            this.surpriseAttack = surpriseAttack;
            canHitNonTargetPawnsNow = canHitNonTargetPawns;
            this.preventFriendlyFire = preventFriendlyFire;
            this.nonInterruptingSelfCast = nonInterruptingSelfCast;
            currentTarget = castTarg;
            currentDestination = destTarg;
            if (CasterIsPawn && WarmupTime > 0f)
            {
                if (!TryFindShootLineFromTo(caster.Position, castTarg, out var resultingLine))
                {
                    return false;
                }
                CasterPawn.Drawer.Notify_WarmingCastAlongLine(resultingLine, caster.Position);
                float statValue = CasterPawn.GetStatValue(StatDefOf.AimingDelayFactor);
                int ticks = (WarmupTime * statValue).SecondsToTicks();
                CasterPawn.stances.SetStance(new Stance_Warmup(ticks, castTarg, this));
                if (verbProps.stunTargetOnCastStart && castTarg.Pawn != null)
                {
                    castTarg.Pawn.stances.stunner.StunFor(ticks, null, addBattleLog: false);
                }
            }
            else
            {
                if (verbTracker.directOwner is Ability ability)
                {
                    ability.lastCastTick = Find.TickManager.TicksGame;
                }
                WarmupComplete();
            }
            return true;
        }

        public override void WarmupComplete()
        {
            state = VerbState.Bursting;
            initialTargetPosition = currentTarget.CenterVector3;
            CalculatePath(currentTarget.CenterVector3, path, pathCells);
            burstShotsLeft = path.Count - 1;
            hitCells.Clear();
            if (verbProps.beamMoteDef != null)
            {
                mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster, new TargetInfo(path[0].ToIntVec3(), caster.Map));
            }
            TryCastNextBurstShot();
            ticksToNextPathStep = base.TicksBetweenBurstShots;
            endEffecter?.Cleanup();
            if (verbProps.soundCastBeam != null)
            {
                sustainer = verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(caster, MaintenanceType.PerTick));
            }
        }



        private new void CalculatePath(Vector3 target, List<Vector3> pathList, HashSet<IntVec3> pathCellsList, bool addRandomOffset = true)
        {
            pathList.Clear();
            IntVec3 intVec = target.ToIntVec3();
            float lengthHorizontal = (intVec - caster.Position).LengthHorizontal;
            float num = (float)(intVec.x - caster.Position.x) / lengthHorizontal;
            float num2 = (float)(intVec.z - caster.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt((float)caster.Position.x + num * verbProps.range);
            intVec.z = Mathf.RoundToInt((float)caster.Position.z + num2 * verbProps.range);
            List<IntVec3> list = GenSight.BresenhamCellsBetween(caster.Position, intVec);

            for (int i = 0; i < list.Count; i++)
            {
                IntVec3 c = list[i];
                if (c.InBounds(Caster.Map))
                {
                    pathList.Add(c.ToVector3Shifted());
                }
            }
            pathCellsList.Clear();
            foreach (Vector3 path in pathList)
            {
                pathCellsList.Add(path.ToIntVec3());
            }
            pathList.Reverse();
            pathCellsList.Reverse();
        }

        private new void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
        {
            if (!cell.InBounds(caster.Map))
            {
                return;
            }
            foreach (IntVec3 item in GenRadial.RadialCellsAround(cell, 2f, useCenter: true).InRandomOrder())
            {
                if (item.InBounds(caster.Map))
                {
                    ApplyDamageSafe(VerbUtility.ThingsToHit(item, caster.Map, CanHit).RandomElementWithFallback(), sourceCell, damageFactor);
                }
            }
            if (verbProps.beamSetsGroundOnFire && Rand.Chance(verbProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(cell, caster.Map, 1f, caster);
            }
        }

        private void ApplyDamageSafe(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
        {
            if (thing == null || verbProps.beamDamageDef == null)
            {
                return;
            }
            IntVec3 intVec = InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(sourceCell, intVec, (IntVec3 c) => c.InBounds(caster.Map) && c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
            if (intVec2.IsValid)
            {
                intVec = intVec2;
            }
            Map map = caster.Map;
            float angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;
            BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(caster, thing, currentTarget.Thing, null, null, null);
            float amount = (float)verbProps.beamDamageDef.defaultDamage * damageFactor;
            DamageInfo dinfo = new DamageInfo(verbProps.beamDamageDef, amount, verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, caster, null, null, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
            thing.TakeDamage(dinfo).AssociateWithLog(log);
            if (thing.CanEverAttachFire())
            {
                if (Rand.Chance(verbProps.beamChanceToAttachFire))
                {
                    thing.TryAttachFire(verbProps.beamFireSizeRange.RandomInRange, caster);
                }
            }
            else if (Rand.Chance(verbProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(intVec, map, verbProps.beamFireSizeRange.RandomInRange, caster, verbProps.flammabilityAttachFireChanceCurve);
            }
        }

    }
}
