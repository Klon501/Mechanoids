using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;

namespace ApexMechanoids
{
	public class CompProperties_AbilityBash : CompProperties_AbilityEffect
	{
		public JobDef job;

		public int jobFactor = 1;

		public float moveSpeed;

		public CompProperties_AbilityBash()
		{
			compClass = typeof(CompAbilityEffect_Bash);
		}
	}
	public class CompAbilityEffect_Bash : CompAbilityEffect
	{
		public new CompProperties_AbilityBash Props => (CompProperties_AbilityBash)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Map map = parent.pawn.Map;
			Job job = JobMaker.MakeJob(Props.job, target.Cell, target);
			job.playerForced = true;
			job.maxNumMeleeAttacks = Props.jobFactor;
			parent.pawn.jobs.StartJob(job, JobCondition.InterruptForced, cancelBusyStances: true);
			if(parent.pawn.jobs?.curDriver is JobDriver_Bash bash)
			{
				bash.moveSpeed = Props.moveSpeed;
			}
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			Map map = parent.pawn.Map;
			if(!target.Cell.InBounds(map) || target.Cell.Fogged(map))
			{
				return false;
			}
			if(!target.Cell.StandableBy(map, parent.pawn))
			{
				return false;
			}
			return base.Valid(target, throwMessages);
		}
	}
}