using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace ApexMechanoids
{
    public class CompProperties_RepairStation : CompProperties
    {
        public float maxMechBodySize = 999f;
        public float minMechBodySize = 0f;
        public float activePowerConsumption = 500f;
        public float healHpPerSecond = 6f;
        public ArmsAnimationConfig armsAnimation;
        public PlatformAnimationConfig platformAnimation;
        public GraphicData topGraphic;
        public Vector3 mechPositionOffsetNorth = Vector3.zero;
        public Vector3 mechPositionOffsetEast = Vector3.zero;
        public Vector3 mechPositionOffsetSouth = Vector3.zero;
        public Vector3 mechPositionOffsetWest = Vector3.zero;

        public CompProperties_RepairStation()
        {
            compClass = typeof(CompRepairStation);
        }
    }
    public class CompRepairStation : ThingComp
    {
        public CompProperties_RepairStation Props => (CompProperties_RepairStation)props;

        public float MaxMechBodySize => Props.maxMechBodySize;
        public float MinMechBodySize => Props.minMechBodySize;
        public float ActivePowerConsumption => Props.activePowerConsumption;
        public float HealHpPerTick => Props.healHpPerSecond / 60f;
        public ArmsAnimationConfig ArmsAnimation => Props.armsAnimation;
        public PlatformAnimationConfig PlatformAnimation => Props.platformAnimation;
        public GraphicData TopGraphic => Props.topGraphic;
        public Vector3 MechPositionOffsetNorth => Props.mechPositionOffsetNorth;
        public Vector3 MechPositionOffsetEast => Props.mechPositionOffsetEast;
        public Vector3 MechPositionOffsetSouth => Props.mechPositionOffsetSouth;
        public Vector3 MechPositionOffsetWest => Props.mechPositionOffsetWest;
    }

    public class ArmConfig
    {
        public GraphicData graphicData;
        public float maxReach = 0.8f;
        public Vector3 drawOffsetNorth;
        public Vector3 drawOffsetEast;
        public Vector3 drawOffsetSouth;
        public Vector3 drawOffsetWest;
        public IntRange? randomInterval;
        public FloatRange? randomReach;
        
        // New parameters for random stops and faster retraction
        public int fastRetractionSpeed = 2; // Speed multiplier for retraction
        public float randomStopChance = 0.1f; // Chance of stopping arms randomly during repair (0-1)
        public int randomStopDurationMin = 60; // Minimum duration of random stops in ticks
        public int randomStopDurationMax = 180; // Maximum duration of random stops in ticks
    }

    public class ArmsAnimationConfig
    {
        public int extendTicks = 60;
        public int retractTicks = 60;
        public List<ArmConfig> arms = new List<ArmConfig>();
    }

    public class PlatformAnimationConfig
    {
        public int ticksToMove = 80;
        public GraphicData graphicData;
        public float animationStart = 0f;
        public float animationEnd = 1f;
        
        // Random slowdown parameters
        public int randomSlowdownFrequency = 75; // How often slowdowns occur (on average, every N ticks - 0 means disabled)
        public int randomSlowdownMinTicks = 60; // Minimum duration of slowdown in ticks
        public int randomSlowdownMaxTicks = 180; // Maximum duration of slowdown in ticks
    }
}
