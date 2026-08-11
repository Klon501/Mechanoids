using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class ModExt_PawnFlyerExt : DefModExtension
    {
        public bool rope;
    }

    // Partially copied from Rimpact and flangoCore
    [StaticConstructorOnStartup]
    public class PawnFlyerExtended : PawnFlyer // use vanilla pawnflyer with OnJumpCompleted instead
    {
        private static readonly string RopeTexPath = "UI/Overlays/Rope";
        private static readonly Material RopeLineMat = MaterialPool.MatFrom(RopeTexPath, ShaderDatabase.Transparent, GenColor.FromBytes(99, 70, 41));
        private ModExt_PawnFlyerExt ext;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            ext = def.GetModExtension<ModExt_PawnFlyerExt>();
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (ext?.rope ?? false) GenDraw.DrawLineBetween(DrawPos, DestinationPos, AltitudeLayer.PawnRope.AltitudeFor(), RopeLineMat);
        }

        public override void DrawGUIOverlay()
        {
            Vector2 pos = LabelDrawPosFor(this, FlyingPawn, -0.6f);
            GenMapUI.DrawPawnLabel(FlyingPawn, pos);
        }

        public static Vector2 LabelDrawPosFor(Thing thing, Pawn heldPawn, float worldOffsetZ)
        {
            Vector3 drawPos = thing.DrawPos;
            drawPos.z += worldOffsetZ;
            Vector2 result = Find.Camera.WorldToScreenPoint(drawPos) / Prefs.UIScale;
            result.y = UI.screenHeight - result.y;

            if (!heldPawn.RaceProps.Humanlike)
                result.y -= 4f;
            else if (heldPawn.DevelopmentalStage.Baby())
                result.y -= 8f;
            return result;
        }
    }
}
