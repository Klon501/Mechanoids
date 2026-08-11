using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    /// <summary>
    /// Put on a weapon ThingDef alongside the guided-missile projectile to let it shoot from behind
    /// its own cover.
    /// </summary>
    public class DefModExtension_JavelinIndirectFire : DefModExtension
    {
        // Allow shots the line of sight does not support, when the launcher is the thing behind
        // cover and the missile's curve is simulated to actually get around it.
        public bool fireFromBehindCover = true;

        // How far out an obstruction may sit and still count as the launcher's own cover rather than
        // as terrain the target is buried behind.
        public float maxBlockerTiles = 6f;

        // Refuse any shot - blocked line or not - that the missile provably cannot complete. Off by
        // default: it only ever removes shots, and a mis-set flight model would silence the weapon
        // rather than merely waste a missile. See the note on the minRange dead band.
        public bool skipUnreachableShots;

        // The obstacle preflight walks the simulated path a quarter tile at a time so it cannot step
        // over a one-tile rock. Only ever runs after the cheap checks have already passed.
        public int obstacleSampleStrideTicks = 1;
        public int obstacleSampleMaxPoints = 320;

        // Reach-only preflight does not touch the map, so it can afford a coarser stride.
        public int reachSampleStrideTicks = 4;
        public int reachSampleMaxPoints = 128;
    }

    /// <summary>
    /// A shooting verb for guided missiles that can be lobbed around the launcher's own cover.
    ///
    /// Vanilla refuses any shot without line of sight. That is right for a bullet, but a missile that
    /// leaves on a cardinal heading and then turns can legitimately come round a rock the launcher is
    /// standing behind. Two things have to be true before the shot is allowed: the obstruction has to
    /// be the launcher's cover rather than terrain around the target (JavelinIndirectFire), and the
    /// missile's actual curve has to clear it - which is settled by flying the shot with the same
    /// guidance the projectile uses, so the launcher never spends a missile on a path into a wall.
    ///
    /// Because it only ever permits shots vanilla was already refusing, the worst case is that the
    /// flight model never finds a way round and the verb behaves exactly as before.
    /// </summary>
    public class Verb_ShootJavelin : Verb_Shoot
    {
        // Reused across calls. Target scanning is main-thread only, so no locking is needed.
        private static float[] reachX = new float[1];
        private static float[] reachZ = new float[1];
        private static float[] obstacleX = new float[1];
        private static float[] obstacleZ = new float[1];

        private DefModExtension_JavelinIndirectFire Props =>
            EquipmentSource?.def?.GetModExtension<DefModExtension_JavelinIndirectFire>();

        private DefModExtension_JavelinMissile MissileProps =>
            Projectile?.GetModExtension<DefModExtension_JavelinMissile>();

        public bool IndirectFireEnabled
        {
            get
            {
                DefModExtension_JavelinIndirectFire props = Props;
                return props != null && props.fireFromBehindCover && MissileProps != null;
            }
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            DefModExtension_JavelinIndirectFire props = Props;

            if (base.CanHitTargetFrom(root, targ))
            {
                // The line is clear. Optionally still refuse it if the flight model says the missile
                // cannot physically get there, which is what the minRange floor only approximates.
                return props == null || !props.skipUnreachableShots || MissileReaches(root, targ, props);
            }

            if (props == null || !props.fireFromBehindCover || MissileProps == null || caster?.Map == null)
            {
                return false;
            }

            // base returned false for one of several reasons. Re-check the ones that are refusals
            // rather than line-of-sight problems, so the cover path does not quietly re-permit them.
            if (targ.Thing == caster || ApparelPreventsShooting())
            {
                return false;
            }

            if (targ.Pawn != null && targ.Pawn.IsPsychologicallyInvisible() && caster.HostileTo(targ.Pawn))
            {
                return false;
            }

            CellRect occupiedRect = targ.HasThing ? targ.Thing.OccupiedRect() : CellRect.SingleCell(targ.Cell);
            if (OutOfRange(root, targ, occupiedRect))
            {
                return false;
            }

            return LineBlockedOnlyByOwnCover(root, targ.Cell, props.maxBlockerTiles)
                   && MissileReaches(root, targ, props)
                   && MissilePathClearOfSolids(root, targ, props);
        }

        /// <summary>
        /// True when the line to the target is obstructed, and every obstruction on it sits close
        /// enough to the launcher to be its own cover.
        /// </summary>
        private bool LineBlockedOnlyByOwnCover(IntVec3 root, IntVec3 targetCell, float maxBlockerTiles)
        {
            Map map = caster.Map;
            bool anyBlocker = false;
            float farthest = 0f;

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(root, targetCell))
            {
                if (cell == root || cell == targetCell || cell.CanBeSeenOverFast(map))
                {
                    continue;
                }

                anyBlocker = true;
                float distance = (cell - root).LengthHorizontal;
                if (distance > farthest)
                {
                    farthest = distance;
                }
            }

            return JavelinIndirectFire.AllowsBlockedShot(anyBlocker, farthest, maxBlockerTiles);
        }

        private JavelinFlightParams FlightParams(DefModExtension_JavelinMissile missile)
        {
            return new JavelinFlightParams
            {
                speedPerTick = Projectile.projectile.SpeedTilesPerTick,
                maxTurnPerTick = missile.turnDegreesPerTick * Mathf.Deg2Rad,
                boostTicks = missile.boostTicks,
                hitRadius = missile.hitRadius,
                terminalRadius = missile.terminalRadius,
                lifetimeTicks = missile.lifetimeTicks
            };
        }

        // The mech turns to face its target before firing, so the cardinal the missile will leave on
        // is the one that facing snaps to.
        private static JavelinFlightState LaunchState(Vector3 origin, Vector3 targetPos)
        {
            Vector3 toTarget = (targetPos - origin).Yto0();
            int rot = toTarget.sqrMagnitude < 0.0001f ? Rot4.North.AsInt : Rot4.FromAngleFlat(toTarget.AngleFlat()).AsInt;
            return JavelinMissileGuidance.CreateState(origin.x, origin.z, JavelinMissileGuidance.CardinalHeading(rot));
        }

        /// <summary>
        /// Flies the shot without a map and reports whether it ends on the target. No cell lookups,
        /// so this is cheap enough to sit in front of the obstacle walk.
        /// </summary>
        private bool MissileReaches(IntVec3 root, LocalTargetInfo targ, DefModExtension_JavelinIndirectFire props)
        {
            DefModExtension_JavelinMissile missile = MissileProps;
            if (missile == null)
            {
                return true;
            }

            Vector3 origin = root.ToVector3Shifted();
            Vector3 targetPos = targ.HasThing ? targ.Thing.DrawPos : targ.Cell.ToVector3Shifted();
            JavelinFlightParams flightParams = FlightParams(missile);

            float[] xs = Buffer(ref reachX, props.reachSampleMaxPoints);
            float[] zs = Buffer(ref reachZ, props.reachSampleMaxPoints);

            int points = JavelinMissileGuidance.SamplePath(
                LaunchState(origin, targetPos), targetPos.x, targetPos.z, flightParams,
                Mathf.Max(1, props.reachSampleStrideTicks), xs, zs);

            return points > 0
                   && JavelinMissileGuidance.Distance(xs[points - 1], zs[points - 1], targetPos.x, targetPos.z) <= flightParams.hitRadius;
        }

        /// <summary>
        /// Flies the shot again, densely, and reports whether the curve stays clear of anything the
        /// missile cannot pass. This is what stops the launcher taking a cover shot whose path runs
        /// straight into the rock it is hiding behind.
        /// </summary>
        private bool MissilePathClearOfSolids(IntVec3 root, LocalTargetInfo targ, DefModExtension_JavelinIndirectFire props)
        {
            DefModExtension_JavelinMissile missile = MissileProps;
            Map map = caster.Map;
            if (missile == null || map == null || !missile.solidObstaclesBlock)
            {
                return true;
            }

            Vector3 origin = root.ToVector3Shifted();
            Vector3 targetPos = targ.HasThing ? targ.Thing.DrawPos : targ.Cell.ToVector3Shifted();
            Thing targetThing = targ.Thing;

            float[] xs = Buffer(ref obstacleX, props.obstacleSampleMaxPoints);
            float[] zs = Buffer(ref obstacleZ, props.obstacleSampleMaxPoints);

            int points = JavelinMissileGuidance.SamplePath(
                LaunchState(origin, targetPos), targetPos.x, targetPos.z, FlightParams(missile),
                Mathf.Max(1, props.obstacleSampleStrideTicks), xs, zs);

            IntVec3 lastCell = IntVec3.Invalid;
            for (int i = 0; i < points; i++)
            {
                IntVec3 cell = new Vector3(xs[i], 0f, zs[i]).ToIntVec3();
                if (cell == lastCell || !cell.InBounds(map))
                {
                    continue;
                }

                lastCell = cell;
                if (CellBlocksMissile(cell, map, targetThing, origin, missile))
                {
                    return false;
                }
            }

            return true;
        }

        // Deliberately the same rule the projectile itself flies on, so a shot the verb allows is one
        // the missile can actually complete.
        private static bool CellBlocksMissile(IntVec3 cell, Map map, Thing targetThing, Vector3 origin, DefModExtension_JavelinMissile missile)
        {
            float tilesFromLaunch = (cell.ToVector3Shifted() - origin).Yto0().magnitude;
            List<Thing> things = cell.GetThingList(map);

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                bool openDoor = thing is Building_Door door && door.Open;
                if (JavelinObstacleRules.Blocks(thing.def.Fillage == FillCategory.Full, openDoor, thing == targetThing, tilesFromLaunch, missile.solidObstacleArmTiles))
                {
                    return true;
                }
            }

            return false;
        }

        private static float[] Buffer(ref float[] buffer, int capacity)
        {
            int size = Mathf.Max(2, capacity);
            if (buffer.Length != size)
            {
                buffer = new float[size];
            }

            return buffer;
        }
    }

    /// <summary>
    /// The AI half of firing from behind cover. AttackTargetFinder drops any target the searcher has
    /// no line of sight to before the verb is ever consulted, so without this the launcher would be
    /// allowed to take a cover shot it is never offered.
    ///
    /// Dropping the flags widens the candidate set, not the shots: every candidate still has to pass
    /// CanShootAtFromCurrentPosition, which is Verb_ShootJavelin.CanHitTargetFrom, and that only
    /// admits a blocked line when the launcher's own cover is the thing in the way and the simulated
    /// flight gets round it.
    /// </summary>
    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
    internal static class Patch_AttackTargetFinder_BestAttackTarget
    {
        private static void Prefix(IAttackTargetSearcher searcher, ref TargetScanFlags flags)
        {
            if (searcher?.CurrentEffectiveVerb is Verb_ShootJavelin verb && verb.IndirectFireEnabled)
            {
                flags &= ~(TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedLOSToNonPawns);
            }
        }
    }
}
