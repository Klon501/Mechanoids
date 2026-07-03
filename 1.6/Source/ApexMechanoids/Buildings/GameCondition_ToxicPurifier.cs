using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Verse;
using static HarmonyLib.Code;

namespace ApexMechanoids
{
    public class GameCondition_ToxicPurifier : GameCondition
    {
        public float toxicityLevel = 0f;

        public DefModExtension_ToxicPurifier modExtension => def.GetModExtension<DefModExtension_ToxicPurifier>();

        public override string Label
        {
            get
            {
                if (modExtension.toxicityUnit != null)
                {
                    return $"{base.Label} ({toxicityLevel}{modExtension.toxicityUnit})";
                }
                else
                {
                    return $"{base.Label} ({toxicityLevel.ToStringPercent("0.00")})";
                }
            }
        }
        public List<Thing> purifierOnMap = new List<Thing>();
        public bool ShouldRemove
        {
            get
            {
                if (purifierOnMap.Count <= 0)
                {
                    return toxicityLevel <= 0f;
                }
                return false;
            }
        }
        public override void PostMake()
        {
            base.PostMake();
            toxicityLevel = 0f;
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (ShouldRemove)
            {
                Permanent = false;
                return;
            }
            if (Find.TickManager.TicksGame % 60000 == 0)
            {
                if (toxicityLevel >= modExtension.toxicLevelToStartSpreading)
                {
                    AffectNearbyTiles();
                    Messages.Message($"toxicity in the atmosphere in local area has reach the critical point of {modExtension.toxicLevelToStartSpreading.ToStringPercent("0.00")} that would affect nearby tiles", MessageTypeDefOf.NegativeEvent);
                }
                if (toxicityLevel >= modExtension.toxicLevelToAffectRelation)
                {
                    Messages.Message($"toxicity in the atmosphere in local area has reach the critical point of {toxicityLevel.ToStringPercent("0.00")} that would affect the globe, relation with other faction will deteriorate",MessageTypeDefOf.NegativeEvent);
                    AffectFactionRelation();
                }
                toxicDegradation();                
            }
            
        }

        public void AffectNearbyTiles()
        {            
            List<Map> affectedMaps = base.AffectedMaps;
            foreach (var map in affectedMaps)
            {
                if (Rand.Chance(modExtension.chanceForToxicFallout))
                {
                    GameCondition gameCondition = GameConditionMaker.MakeCondition(GameConditionDefOf.ToxicFallout);
                    gameCondition.Duration = 60000;
                    map.gameConditionManager.RegisterCondition(gameCondition);
                }
                var curTile = map.Tile;
                List<PlanetTile> neighborTileInRange = new List<PlanetTile>();
                foreach (var item in Find.WorldGrid.Tiles)
                {
                    if(item.tile == curTile) continue;
                    if (Find.World.grid.ApproxDistanceInTiles(item.tile, curTile) <= 10f)
                    {
                        neighborTileInRange.Add(item.tile);
                    }
                }
                //Find.World.grid.GetTileNeighbors(curTile, neighborTileInRange);
                if (!neighborTileInRange.NullOrEmpty())
                {
                    foreach (var tile in neighborTileInRange)
                    {
                        Find.World.grid[tile].pollution += modExtension.pollutionChangeOnNeighborsTile;
                    }
                }
            }
        }
        public void AffectFactionRelation()
        {
            
            foreach (var item in Find.FactionManager.AllFactionsVisible)
            {
                if (item == Faction.OfPlayer) continue;
                if (item.HasGoodwill && !item.def.permanentEnemy)
                {
                    Faction.OfPlayer.TryAffectGoodwillWith(item,modExtension.goodWillChangePerDayAboveThreshold);
                    //Messages.Message($"faction relation with {item.Name} changed by {modExtension.goodWillChangePerDayAboveThreshold}. cause: {Label}",MessageTypeDefOf.NegativeEvent);
                }
            }
        }
        public void ChangeToxicity(float value)
        {
            toxicityLevel += value;
            if (modExtension.maxValue > 0)
            {
                if (toxicityLevel > modExtension.maxValue)
                {
                    toxicityLevel = modExtension.maxValue;
                }
            }
        }

        public void toxicDegradation()
        {
            if (toxicityLevel > 0f)
            {
                ChangeToxicity(modExtension.toxicRecoveryPerDay);
                if (toxicityLevel < 0f)
                {
                    toxicityLevel = 0f;
                }
            }
        }
    }

    public class DefModExtension_ToxicPurifier : DefModExtension
    {
        public float maxValue = -1f;

        public float toxicRecoveryPerDay = -0.25f;

        public float toxicLevelToStartSpreading = 0.5f;

        public float toxicLevelToAffectRelation = 0.75f;

        public int goodWillChangePerDayAboveThreshold = -1;

        public float pollutionChangeOnNeighborsTile = 0.01f;

        public float chanceForToxicFallout = 0.1f;

        public float tileRadius = 10f;

        public string toxicityUnit;
    }
}
