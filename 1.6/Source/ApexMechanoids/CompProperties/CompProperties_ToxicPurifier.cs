using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class CompProperties_ToxicPurifier : CompProperties
    {
        public float toxicPerTileCleaned = 0.01f;

        public int interval = 2500;

        public float radius = 4f;

        public int tileToUnpollutePerTrigger = 1;

        public bool isCleanRandomTileInMap;

        public EffecterDef pumpEffecterDef;

        public IntVec3 effecterOffset;
        public Vector3 effecterVectorOffset;

        public GameConditionDef conditionDef;

        public CompProperties_ToxicPurifier()
        {
            compClass = typeof(CompToxicPurifier);
        }
    }
}
