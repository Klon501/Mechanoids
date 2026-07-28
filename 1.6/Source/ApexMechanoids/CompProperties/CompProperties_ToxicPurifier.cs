using RimWorld;
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

        public float radius = 14.9f;

        public bool clearWholeMap;

        public EffecterDef pumpEffecterDef;

        public Vector3 effecterOffsetDefault;

        public Vector3? effecterOffsetNorth;

		public Vector3? effecterOffsetEast;

		public Vector3? effecterOffsetSouth;

		public Vector3? effecterOffsetWest;

		public GameConditionDef conditionDef;

		public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
		{
			if (radius > 79.9f)
			{
				yield return parentDef.defName + " has CompProperties_ToxicPurifier with radius more than 79.9, use clearWholeMap instead.";
			}
		}

        public CompProperties_ToxicPurifier()
        {
            compClass = typeof(CompToxicPurifier);
        }

		public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol, AltitudeLayer drawAltitude, Thing thing = null)
		{
            if (!clearWholeMap)
            {
                GenDraw.DrawRadiusRing(center, radius);
            }
		}

		public Vector3 EffectOffsetForRot(Rot4 rot)
		{
			switch (rot.AsInt)
			{
				case 0:
					return effecterOffsetNorth ?? effecterOffsetDefault;
				case 1:
					return effecterOffsetEast ?? effecterOffsetDefault;
				case 2:
					return effecterOffsetSouth ?? effecterOffsetDefault;
				case 3:
					return effecterOffsetWest ?? effecterOffsetDefault;
				default:
					return effecterOffsetDefault;
			}
		}
	}
}
