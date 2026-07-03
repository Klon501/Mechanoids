using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApexMechanoids
{
    public class CompAbilityEffect_Starfall : CompAbilityEffect
    {
        public new CompProperties_Starfall Props => (CompProperties_Starfall)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.MapHeld;
            LocalTargetInfo selectedTarget = target.IsValid ? target : dest;
            if (caster == null || map == null || !caster.Spawned || !selectedTarget.IsValid)
            {
                return;
            }

            if (Props.carrierProjectileDef == null)
            {
                return;
            }

            Projectile projectile = GenSpawn.Spawn(Props.carrierProjectileDef, caster.PositionHeld, map) as Projectile;
            if (projectile == null)
            {
                return;
            }

            projectile.Launch(caster, caster.DrawPos, selectedTarget, selectedTarget, ProjectileHitFlags.NonTargetPawns, false);
            if (!Props.launchSoundDefName.NullOrEmpty())
            {
                SoundDef launchSound = DefDatabase<SoundDef>.GetNamedSilentFail(Props.launchSoundDefName);
                launchSound?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            }
        }
    }

    public class CompProperties_Starfall : CompProperties_AbilityEffect
    {
        public CompProperties_Starfall()
        {
            compClass = typeof(CompAbilityEffect_Starfall);
        }

        public ThingDef carrierProjectileDef;
        public string launchSoundDefName = "Shot_IncendiaryLauncher";
    }

    public class DefModExtension_StarfallCarrier : DefModExtension
    {
        public ThingDef splitProjectileDef;
        public float splitAfterProgress = 0.45f;
        public float splitSpreadDistance = 2.2f;
        public float splitSpreadJitter = 1.15f;
        public float splitForwardJitter = 1.35f;
        public float splitTargetJitter = 0.75f;
    }

    public class Projectile_StarfallCarrier : Projectile
    {
        private const float SplitSpawnSeparation = 0.42f;
        private const float CenterForwardOffset = 0.18f;

        private bool splitOccurred;

        public override void Tick()
        {
            base.Tick();

            if (!splitOccurred && ShouldSplitNow())
            {
                SplitNow();
                VanishImmediately();
            }
        }

        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (!splitOccurred)
            {
                SplitNow();
            }

            VanishImmediately();
        }

        private bool ShouldSplitNow()
        {
            DefModExtension_StarfallCarrier modExt = def.GetModExtension<DefModExtension_StarfallCarrier>();
            if (modExt == null)
            {
                return false;
            }

            int splitAfterTicks = Mathf.Max(Mathf.RoundToInt(StartingTicksToImpact * Mathf.Clamp01(modExt.splitAfterProgress)), 1);
            return StartingTicksToImpact - ticksToImpact >= splitAfterTicks;
        }

        private void SplitNow()
        {
            if (splitOccurred)
            {
                return;
            }

            splitOccurred = true;
            DefModExtension_StarfallCarrier modExt = def.GetModExtension<DefModExtension_StarfallCarrier>();
            if (modExt?.splitProjectileDef == null || Map == null || launcher == null)
            {
                return;
            }

            IntVec3 centerCell = intendedTarget.IsValid ? intendedTarget.Cell : destination.ToIntVec3();
            Vector3 centerVec = centerCell.ToVector3Shifted();
            Vector3 splitOrigin = DrawPos;
            Vector3 direction = centerVec - splitOrigin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = new Vector3(1f, 0f, 0f);
            }
            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);

            FleckMaker.Static(splitOrigin, Map, FleckDefOf.ExplosionFlash, 1.6f);

            LaunchSplitProjectile(OffsetCell(centerVec + RandomForwardOffset(direction, modExt), modExt), splitOrigin + direction * CenterForwardOffset, modExt);
            LaunchSplitProjectile(RandomizedSideTarget(centerVec, direction, side, modExt, true), splitOrigin + side * SplitSpawnSeparation, modExt);
            LaunchSplitProjectile(RandomizedSideTarget(centerVec, direction, side, modExt, false), splitOrigin - side * SplitSpawnSeparation, modExt);
        }

        private IntVec3 RandomizedSideTarget(Vector3 centerVec, Vector3 direction, Vector3 side, DefModExtension_StarfallCarrier modExt, bool rightSide)
        {
            float sideSign = rightSide ? 1f : -1f;
            float sideDistance = modExt.splitSpreadDistance + Rand.Range(-modExt.splitSpreadJitter, modExt.splitSpreadJitter);
            if (sideDistance < 0.4f)
            {
                sideDistance = 0.4f;
            }

            Vector3 baseVec = centerVec + side * sideSign * sideDistance + RandomForwardOffset(direction, modExt);
            return OffsetCell(baseVec, modExt);
        }

        private Vector3 RandomForwardOffset(Vector3 direction, DefModExtension_StarfallCarrier modExt)
        {
            return direction * Rand.Range(-modExt.splitForwardJitter, modExt.splitForwardJitter);
        }

        private IntVec3 OffsetCell(Vector3 baseVec, DefModExtension_StarfallCarrier modExt)
        {
            Vector3 jitter = new Vector3(Rand.Range(-modExt.splitTargetJitter, modExt.splitTargetJitter), 0f, Rand.Range(-modExt.splitTargetJitter, modExt.splitTargetJitter));
            IntVec3 cell = (baseVec + jitter).ToIntVec3();
            if (!cell.InBounds(Map))
            {
                cell = intendedTarget.IsValid ? intendedTarget.Cell : destination.ToIntVec3();
            }

            return cell;
        }

        private void LaunchSplitProjectile(IntVec3 targetCell, Vector3 origin, DefModExtension_StarfallCarrier modExt)
        {
            Projectile projectile = GenSpawn.Spawn(modExt.splitProjectileDef, origin.ToIntVec3(), Map) as Projectile;
            if (projectile == null)
            {
                return;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(targetCell);
            projectile.Launch(launcher, origin, targetInfo, targetInfo, ProjectileHitFlags.NonTargetPawns, false);
        }

        private void VanishImmediately()
        {
            if (Destroyed)
            {
                return;
            }

            if (Spawned)
            {
                DeSpawn(DestroyMode.Vanish);
            }

            Destroy(DestroyMode.Vanish);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref splitOccurred, nameof(splitOccurred));
        }
    }

    public class Projectile_StarfallFragment : Bullet
    {
        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;
            Thing launcherThing = launcher;
            ThingDef equipment = equipmentDef;
            Thing targetThing = intendedTarget.Thing;

            base.Impact(hitThing, blockedByShield);

            if (map == null)
            {
                return;
            }

            float radius = def.projectile.explosionRadius;
            DamageDef damageDef = def.projectile.damageDef;
            if (damageDef == null || radius <= 0f)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            GenExplosion.DoExplosion(cell, map, radius, damageDef, launcherThing, DamageAmount, ArmorPenetration, null, equipment, def);
        }
    }
}
