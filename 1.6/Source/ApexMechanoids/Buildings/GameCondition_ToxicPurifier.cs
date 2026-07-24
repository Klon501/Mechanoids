using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace ApexMechanoids
{
    public class GameCondition_ToxicPurifier : GameCondition
    {
        public float toxicityLevel = 0f;

		public List<Thing> purifiersOnMap = new List<Thing>();

		public DefModExtension_ToxicPurifier ModExtension => def.GetModExtension<DefModExtension_ToxicPurifier>();

        public override string Label
        {
            get
            {
				if (toxicityLevel < 0.01f)
				{
					return base.Label;
				}
				var modExtension = ModExtension;
				return $"{base.Label} ({ToxicityToString(Mathf.Clamp(toxicityLevel, 0, modExtension.maxValue))})";
            }
        }

		public string ToxicityToString(float toxicity)
		{
			var modExtension = ModExtension;
			if (modExtension.toxicityUnit != null)
			{
				return toxicity.ToStringByStyle(ToStringStyle.FloatTwo) + modExtension.toxicityUnit;
			}
			else
			{
                return (toxicity / modExtension.maxValue).ToStringPercent("0.00");
			}
		}

		public override string Description
        {
            get
            {
                string s = base.Description;
                s += "\n\n";
                s += "APM_ToxicPurifier_RecoveryPerDay".Translate().Colorize(ColoredText.TipSectionTitleColor) + ": " + ToxicityToString(ModExtension.toxicRecoveryPerDay);
				return s;
            }
        }

        public bool ShouldRemove
        {
            get
            {
                if (purifiersOnMap.NullOrEmpty())
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
                bool flag1 = toxicityLevel >= ModExtension.toxicLevelToAffectRelation;
                bool flag2 = toxicityLevel >= ModExtension.toxicLevelToStartSpreading;
				if (flag1 && flag2)
				{
					Messages.Message("APM_ToxicPurifier_LevelToAffectRelationAndStartSpreading".Translate(ToxicityToString(ModExtension.toxicLevelToStartSpreading), ToxicityToString(ModExtension.toxicLevelToAffectRelation)), MessageTypeDefOf.NegativeEvent);
					AffectFactionRelation();
					AffectNearbyTiles();
				}
				else if (flag1)
				{
					Messages.Message("APM_ToxicPurifier_LevelToAffectRelation".Translate(ToxicityToString(ModExtension.toxicLevelToAffectRelation)), MessageTypeDefOf.NegativeEvent);
					AffectFactionRelation();
				}
				else if (flag2)
                {
                    Messages.Message("APM_ToxicPurifier_LevelToStartSpreading".Translate(ToxicityToString(ModExtension.toxicLevelToStartSpreading)), MessageTypeDefOf.NegativeEvent);
					AffectNearbyTiles();
				}
				ToxicDegradation();                
            }
            
        }

        public void AffectNearbyTiles()
        {            
            List<Map> affectedMaps = base.AffectedMaps;
            foreach (var map in affectedMaps)
            {
                if (Rand.Chance(ModExtension.chanceForToxicFallout) && !map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
                {
                    GameCondition gameCondition = GameConditionMaker.MakeCondition(GameConditionDefOf.ToxicFallout);
                    gameCondition.Duration = ModExtension.toxicFalloutTicksRange.RandomInRange;
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
                        Find.World.grid[tile].pollution += ModExtension.pollutionChangeOnNeighborsTile;
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
                    Faction.OfPlayer.TryAffectGoodwillWith(item, ModExtension.goodWillChangePerDayAboveThreshold, false);
                    //Messages.Message($"faction relation with {item.Name} changed by {modExtension.goodWillChangePerDayAboveThreshold}. cause: {Label}",MessageTypeDefOf.NegativeEvent);
                }
            }
        }
        public void ChangeToxicity(float value)
        {
            toxicityLevel += value;
            if (ModExtension.maxValue > 0)
            {
                if (toxicityLevel > ModExtension.maxValue)
                {
                    toxicityLevel = ModExtension.maxValue;
                }
            }
        }

        public void ToxicDegradation()
        {
            if (toxicityLevel > 0f)
            {
                ChangeToxicity(ModExtension.toxicRecoveryPerDay);
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

        public IntRange toxicFalloutTicksRange = new IntRange(120000, 300000);
	}
}
