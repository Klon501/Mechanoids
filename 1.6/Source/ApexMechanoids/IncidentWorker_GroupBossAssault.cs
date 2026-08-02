using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace ApexMechanoids
{
    public class IncidentWorker_GroupBossAssault : IncidentWorker
    {
        public DefModExtension_Incident_CallMulBossgroup Ext => cachedExt ?? (cachedExt = def.GetModExtension<DefModExtension_Incident_CallMulBossgroup>());
        private DefModExtension_Incident_CallMulBossgroup cachedExt;

        public int BossMultPerThreat(float points)
        {
            return 2 + Ext.bossMultPerThreat.FindLastIndex(t => t <= points);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            List<Pawn> pawns = new List<Pawn>();
            List<Pawn> bosses = new List<Pawn>();
            float points = parms.points;
            int mult = BossMultPerThreat(points);
            foreach (PawnKindDef boss in Ext.bosses)
            {
                PawnKindDef kind = boss;
                Faction faction = parms.faction;
                PlanetTile? tile = map.Tile;
                PawnGenerationRequest pawnGenerationRequest = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile);
                for (int i = 0; i < mult; i++)
                {
                    points -= boss.combatPower;
                    bosses.Add(PawnGenerator.GeneratePawn(pawnGenerationRequest));
                }
            }
            pawns.AddRange(bosses);
            int amount = bosses.Count;
            float minCost = Ext.escorts.Min((PawnGenOption opt) => opt.Cost);
            List<Pawn> escorts = new List<Pawn>();
            while (points > minCost && amount < 200)
            {
                amount++;
                Ext.escorts.TryRandomElementByWeight((PawnGenOption gr) => gr.selectionWeight, out var escort);
                if (!(escort.Cost > points))
                {
                    PawnKindDef kind = escort.kind;
                    Faction faction = parms.faction;
                    PlanetTile? tile = map.Tile;
                    points -= escort.Cost;
                    escorts.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile)));
                }
            }
            pawns.AddRange(escorts);
            IntVec3 stageLocation = DropCellFinder.RandomDropSpot(map);
            DropPodUtility.DropThingsNear(stageLocation, map, pawns, parms.podOpenDelay, canInstaDropDuringInit: false, leaveSlag: true, parms.canRoofPunch ?? true, forbid: true, allowFogged: false, parms.faction);
            LordMaker.MakeNewLord(parms.faction, new LordJob_BossgroupAssaultColony(parms.faction, stageLocation, bosses), map, pawns);
            List<string> tmpEntries = new List<string>();
            foreach (var type in pawns.GroupBy(p => p).Select(g => new { Name = g.Key, Count = g.Count() }))
            {
                tmpEntries.Add(GenLabel.BestKindLabel(type.Name.kindDef, Gender.None).CapitalizeFirst() + " x" + type.Count);
            }
            SendStandardLetter(def.letterLabel, def.letterText.Translate(parms.faction.NameColored.ToString(), parms.faction.def.pawnsPlural, tmpEntries.ToLineList("  - ")), def.letterDef, parms, pawns);
            if (!parms.silent)
            {
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }
            Find.StoryWatcher.statsRecord.numRaidsEnemy++;
            parms.target.StoryState.lastRaidFaction = parms.faction;
            return true;
        }
    }
}
