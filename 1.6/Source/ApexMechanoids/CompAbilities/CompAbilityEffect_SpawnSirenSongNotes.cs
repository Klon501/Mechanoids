using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class CompAbility_SpawnSirenSongNotes : AbilityComp
    {
        private static readonly Dictionary<int, int> lastSpawnTickByCaster = new Dictionary<int, int>();
        private int ticksUntilNextSpawn;

        public CompProperties_SpawnSirenSongNotes Props => (CompProperties_SpawnSirenSongNotes)props;

        public override void CompTick()
        {
            base.CompTick();

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (caster == null || map == null || !caster.Spawned)
            {
                return;
            }

            bool isChanneling = Props.channelJobDef != null && caster.CurJobDef == Props.channelJobDef;
            if (!parent.wasCastingOnPrevTick && !isChanneling)
            {
                ticksUntilNextSpawn = 0;
                return;
            }

            if (ticksUntilNextSpawn > 0)
            {
                ticksUntilNextSpawn--;
                return;
            }

            ticksUntilNextSpawn = Props.spawnIntervalTicks;

            if (!caster.Position.ShouldSpawnMotesAt(map))
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            int casterId = caster.thingIDNumber;
            if (lastSpawnTickByCaster.TryGetValue(casterId, out int lastTick) && lastTick == currentTick)
            {
                return;
            }

            lastSpawnTickByCaster[casterId] = currentTick;

            int noteCountThisBurst = Props.noteCount;
            if (Props.extraNoteChance > 0f && Rand.Chance(Props.extraNoteChance))
            {
                noteCountThisBurst++;
            }

            for (int i = 0; i < noteCountThisBurst; i++)
            {
                Vector3 pos = caster.DrawPos + new Vector3(Rand.Range(-Props.horizontalJitter, Props.horizontalJitter), 0f, Rand.Range(Props.minVerticalOffset, Props.maxVerticalOffset));
                FleckCreationData data = FleckMaker.GetDataStatic(pos, map, Props.fleckDef, Rand.Range(Props.scaleRange.min, Props.scaleRange.max));
                data.rotation = Props.rotation;
                data.rotationRate = Rand.Range(-Props.rotationRate, Props.rotationRate);
                data.velocityAngle = Rand.Range(Props.velocityAngle.min, Props.velocityAngle.max);
                data.velocitySpeed = Rand.Range(Props.speedRange.min, Props.speedRange.max);
                map.flecks.CreateFleck(data);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextSpawn, nameof(ticksUntilNextSpawn));
        }
    }

    public class CompProperties_SpawnSirenSongNotes : AbilityCompProperties
    {
        public CompProperties_SpawnSirenSongNotes()
        {
            compClass = typeof(CompAbility_SpawnSirenSongNotes);
        }

        public FleckDef fleckDef;
        public int noteCount = 2;
        public float extraNoteChance = 0.25f;
        public int spawnIntervalTicks = 8;
        public float horizontalJitter = 0.16f;
        public float minVerticalOffset = 0.12f;
        public float maxVerticalOffset = 0.56f;
        public FloatRange scaleRange = new FloatRange(0.32f, 0.64f);
        public FloatRange speedRange = new FloatRange(0.52f, 0.74f);
        public FloatRange velocityAngle = new FloatRange(-4f, 4f);
        public float rotation = 0f;
        public float rotationRate = 0f;
        public JobDef channelJobDef;
    }

    public class CompAbilityEffect_SirenLureChannel : CompAbilityEffect
    {
        public CompProperties_SirenLureChannel ChannelProps => (CompProperties_SirenLureChannel)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster == null || caster.Dead || caster.Downed || !caster.Spawned || caster.Map == null ||
                targetPawn == null || targetPawn.Dead || targetPawn.Downed || !targetPawn.Spawned || targetPawn.Map != caster.Map ||
                ChannelProps.jobDef == null)
            {
                return;
            }

            int durationTicks = Mathf.Max(1, parent.def.GetStatValueAbstract(StatDefOf.Ability_Duration, caster).SecondsToTicks());
            Job channelJob = JobMaker.MakeJob(ChannelProps.jobDef, targetPawn, caster);
            channelJob.count = durationTicks;
            channelJob.playerForced = caster.Faction == Faction.OfPlayer;
            caster.jobs.StartJob(channelJob, JobCondition.InterruptForced, cancelBusyStances: true);
        }
    }

    public class CompProperties_SirenLureChannel : CompProperties_AbilityEffect
    {
        public JobDef jobDef;

        public CompProperties_SirenLureChannel()
        {
            compClass = typeof(CompAbilityEffect_SirenLureChannel);
        }
    }

    public class JobDriver_SirenLureChannel : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(delegate
            {
                Pawn targetPawn = TargetThingA as Pawn;
                if (targetPawn?.jobs != null && targetPawn.CurJobDef == JobDefOf.GotoMindControlled)
                {
                    targetPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });

            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map == null);
            this.FailOn(() =>
            {
                Pawn targetPawn = TargetThingA as Pawn;
                return targetPawn == null || targetPawn.Dead || targetPawn.Downed || targetPawn.Map != pawn.Map ||
                    targetPawn.CurJobDef != JobDefOf.GotoMindControlled;
            });

            Toil channel = ToilMaker.MakeToil("SirenLureChannel");
            channel.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            channel.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(TargetA);
            };
            channel.defaultCompleteMode = ToilCompleteMode.Delay;
            channel.defaultDuration = Mathf.Max(1, job.count);
            channel.handlingFacing = true;
            channel.WithProgressBarToilDelay(TargetIndex.B);
            yield return channel;
        }
    }
}
