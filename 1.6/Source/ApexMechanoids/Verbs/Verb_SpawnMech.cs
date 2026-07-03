using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ApexMechanoids
{
    public class Verb_SpawnMech : Verb
    {
        public DefModExtension_MechPack ModExtension => EquipmentSource?.def?.GetModExtension<DefModExtension_MechPack>();

        public CompApparelReloadable ReloadableComp => EquipmentSource?.TryGetComp<CompApparelReloadable>();

        public List<Pawn> spawnedThing = new List<Pawn>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref spawnedThing, "spawnedThing", LookMode.Reference);
        }

        public override bool TryCastShot()
        {
            DefModExtension_MechPack modExtension = ModExtension;
            CompApparelReloadable comp = ReloadableComp;
            Map map = Caster?.MapHeld;

            if (modExtension?.spawnedKind == null || comp == null || map == null)
            {
                try
                {
                    if (comp.remainingCharges > 0)
                    {
                        comp.UsedOnce();
                        List<Pawn> list = new List<Pawn>(spawnedThing);
                        foreach (var item in list)
                        {
                            if (item.Dead || item.DestroyedOrNull())
                            {
                                spawnedThing.Remove(item);
                            }
                        }
                        if (spawnedThing.Count > 2)
                        {                            
                            Pawn pawn = spawnedThing[0];
                            pawn.Kill(new DamageInfo(DamageDefOf.ElectricalBurn,99999f,2f,instigator:Caster));
                            spawnedThing.Remove(pawn);
                        }
                        Pawn innerSpawnedOne = PawnGenerator.GeneratePawn(modExtension.spawnedKind);
                        innerSpawnedOne.mindState.mentalStateHandler.ClearMentalStateDirect();
                        innerSpawnedOne.SetFaction(Caster.Faction);
                        GenSpawn.Spawn(innerSpawnedOne, CurrentTarget.Cell, Caster.MapHeld);
                        CompUnity compUnity = innerSpawnedOne.TryGetComp<CompUnity>();
                        if (compUnity != null)
                        {
                            compUnity.ForceUpdateNow();
                        }
                        spawnedThing.Add(innerSpawnedOne);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ApexMechanoids] Error in Verb_SpawnMech.TryCastShot {ex}");
                }
            }

            CleanSpawnedList();

            if (spawnedThing.Count >= modExtension.maxNum)
            {
                Messages.Message(
                    "APM.MechamancerPack.MaxSatellites".Translate(modExtension.maxNum),
                    Caster,
                    MessageTypeDefOf.RejectInput,
                    false);
                return false;
            }

            if (!comp.CanBeUsed(out string reason))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, Caster, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            if (!TryFindSpawnCell(Caster.Position, map, out IntVec3 spawnCell))
            {
                Messages.Message(
                    "APM.MechamancerPack.NoValidSpawnCell".Translate(),
                    Caster,
                    MessageTypeDefOf.RejectInput,
                    false);
                return false;
            }

            Pawn spawnedOne = PawnGenerator.GeneratePawn(modExtension.spawnedKind);
            spawnedOne.mindState.mentalStateHandler.ClearMentalStateDirect();
            if (Caster?.Faction != null)
            {
                spawnedOne.SetFaction(Caster.Faction);
            }

            comp.UsedOnce();
            GenSpawn.Spawn(spawnedOne, spawnCell, map);
            spawnedThing.Add(spawnedOne);
            return true;
        }

        public override bool Available()
        {
            CompApparelReloadable comp = ReloadableComp;
            return comp != null ? comp.CanBeUsed(out _) : base.Available();
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return Caster?.MapHeld != null;
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return Caster?.MapHeld != null;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages)
        {
            return Caster?.MapHeld != null;
        }

        private void CleanSpawnedList()
        {
            for (int i = spawnedThing.Count - 1; i >= 0; i--)
            {
                Pawn pawn = spawnedThing[i];
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    spawnedThing.RemoveAt(i);
                }
            }
        }

        private static bool TryFindSpawnCell(IntVec3 targetCell, Map map, out IntVec3 spawnCell)
        {
            if (IsValidSpawnCell(targetCell, map))
            {
                spawnCell = targetCell;
                return true;
            }

            return CellFinder.TryFindRandomSpawnCellForPawnNear(
                targetCell,
                map,
                out spawnCell,
                2,
                cell => IsValidSpawnCell(cell, map));
        }

        private static bool IsValidSpawnCell(IntVec3 cell, Map map)
        {
            return cell.InBounds(map)
                && !cell.Fogged(map)
                && cell.Standable(map)
                && cell.GetFirstPawn(map) == null;
        }

        private static void AssignGuardLord(Pawn satellite, Pawn caster, Map map)
        {
            if (satellite == null || caster == null || map == null || satellite.Faction == null)
            {
                return;
            }

            LordMaker.MakeNewLord(
                satellite.Faction,
                new LordJob_EscortPawn(caster, null),
                map,
                new[] { satellite });
        }
    }

    public class DefModExtension_MechPack : DefModExtension
    {
        public int maxNum = 2;

        public PawnKindDef spawnedKind;

    }
}
