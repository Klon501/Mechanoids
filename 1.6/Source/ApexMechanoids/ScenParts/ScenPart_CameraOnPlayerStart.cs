using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Puts the opening camera on the player's own pawns.
    ///
    /// Game.InitNewGame jumps the camera to MapGenerator.PlayerStartSpot and only then calls
    /// Scenario.PostGameStart. When the scenario drops the player somewhere other than that spot,
    /// such as at a map edge, the player opens on empty ground and has to go looking for their
    /// colonist. Running here overrides that jump with the position the pawns actually landed at.
    /// </summary>
    public class ScenPart_CameraOnPlayerStart : ScenPart
    {
        public override void PostGameStart()
        {
            base.PostGameStart();

            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            if (!TryFindFocus(map, out IntVec3 focus))
            {
                return;
            }

            Find.CameraDriver.JumpToCurrentMapLoc(focus);
        }

        /// <summary>
        /// Prefers a colonist, then any other player pawn such as a starting mech, so the camera
        /// still lands somewhere useful if the scenario ever starts without a humanlike.
        /// </summary>
        private static bool TryFindFocus(Map map, out IntVec3 focus)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count > 0)
            {
                focus = Average(colonists);
                return true;
            }

            List<Pawn> playerPawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            if (playerPawns.Count > 0)
            {
                focus = Average(playerPawns);
                return true;
            }

            focus = IntVec3.Invalid;
            return false;
        }

        private static IntVec3 Average(List<Pawn> pawns)
        {
            int x = 0;
            int z = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                x += pawns[i].Position.x;
                z += pawns[i].Position.z;
            }

            return new IntVec3(x / pawns.Count, 0, z / pawns.Count);
        }

        public override string Summary(Scenario scen)
        {
            return def.description;
        }
    }
}
