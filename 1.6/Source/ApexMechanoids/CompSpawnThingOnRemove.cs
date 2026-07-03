using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace ApexMechanoids
{
    public class CompSpawnThingOnRemove : HediffComp
    {
        public ThingDef thing;
        public int count;
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            var pos = CellFinder.FindNoWipeSpawnLocNear(parent.pawn.Position, parent.pawn.Map, thing, Rot4.North, count);
            GenSpawn.Spawn(thing, pos, parent.pawn.Map, WipeMode.VanishOrMoveAside).stackCount = Mathf.Max(count, 1);
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref thing, "thingToSpawn");
            Scribe_Values.Look(ref count, nameof(count));
        }
    }
}
