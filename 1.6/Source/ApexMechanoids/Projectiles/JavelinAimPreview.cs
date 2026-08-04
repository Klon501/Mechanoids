using HarmonyLib;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Draws the path a guided missile will take while its launcher is still winding up the shot.
    ///
    /// The missile leaves along the mech's cardinal facing and only then turns onto the target, so a
    /// shot at something off to the side departs sideways and curves back in. Without a preview that
    /// reads as the weapon firing in the wrong direction. The path is simulated from
    /// JavelinMissileGuidance - the same code the projectile flies on - so what is drawn is what will
    /// happen, including the shots that curve wide and miss.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class JavelinAimPreview
    {
        private static readonly Material PathMaterial =
            MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.55f, 0.2f, 0.5f));

        // Reused across draws. Drawing is main-thread only, so no locking is needed.
        private static float[] pathX = new float[1];
        private static float[] pathZ = new float[1];

        public static void Draw(Pawn caster, Verb verb, LocalTargetInfo target)
        {
            if (caster == null || !caster.Spawned || verb == null || !target.IsValid)
            {
                return;
            }

            ThingDef projectile = (verb as Verb_LaunchProjectile)?.Projectile;
            DefModExtension_JavelinMissile props = projectile?.GetModExtension<DefModExtension_JavelinMissile>();
            if (props == null || !props.drawAimPreview)
            {
                return;
            }

            int capacity = Mathf.Max(2, props.aimPreviewMaxPoints);
            if (pathX.Length != capacity)
            {
                pathX = new float[capacity];
                pathZ = new float[capacity];
            }

            Vector3 origin = caster.DrawPos;
            Vector3 targetPos = target.CenterVector3;

            JavelinFlightState state = JavelinMissileGuidance.CreateState(
                origin.x, origin.z, JavelinMissileGuidance.CardinalHeading(caster.Rotation.AsInt));

            JavelinFlightParams flightParams = new JavelinFlightParams
            {
                speedPerTick = projectile.projectile.SpeedTilesPerTick,
                maxTurnPerTick = props.turnDegreesPerTick * Mathf.Deg2Rad,
                boostTicks = props.boostTicks,
                hitRadius = props.hitRadius,
                terminalRadius = props.terminalRadius,
                lifetimeTicks = props.lifetimeTicks
            };

            int points = JavelinMissileGuidance.SamplePath(
                state, targetPos.x, targetPos.z, flightParams, Mathf.Max(1, props.aimPreviewStrideTicks), pathX, pathZ);

            float altitude = AltitudeLayer.MetaOverlays.AltitudeFor();
            for (int i = 1; i < points; i++)
            {
                GenDraw.DrawLineBetween(
                    new Vector3(pathX[i - 1], altitude, pathZ[i - 1]),
                    new Vector3(pathX[i], altitude, pathZ[i]),
                    PathMaterial,
                    0.15f);
            }

            // Where the missile actually ends up, which is not the target's feet on a shot the
            // launcher cannot make. That gap is the whole point of showing this.
            if (points > 0)
            {
                GenDraw.DrawCircleOutline(new Vector3(pathX[points - 1], altitude, pathZ[points - 1]), 0.6f, PathMaterial);
            }
        }
    }

    /// <summary>
    /// Stance_Warmup.StanceDraw is called from PawnRenderer for every spawned pawn, not only selected
    /// ones, which is what makes it the right seam: an incoming missile is as surprising to read as an
    /// outgoing one. The postfix is inert for every weapon whose projectile has no javelin extension.
    /// </summary>
    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceDraw))]
    internal static class Patch_Stance_Warmup_StanceDraw
    {
        private static void Postfix(Stance_Warmup __instance)
        {
            JavelinAimPreview.Draw(__instance.stanceTracker?.pawn, __instance.verb, __instance.focusTarg);
        }
    }
}
