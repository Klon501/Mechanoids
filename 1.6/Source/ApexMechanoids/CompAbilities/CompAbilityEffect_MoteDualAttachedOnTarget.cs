using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
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
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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
	public class CompProperties_AbilityMoteDualAttachedOnTarget : CompProperties_AbilityEffect
	{
		public ThingDef moteDef;

		public List<ThingDef> moteDefs;

		public float scale = 1f;

		public int preCastTicks;

		public CompProperties_AbilityMoteDualAttachedOnTarget()
		{
			compClass = typeof(CompAbilityEffect_MoteDualAttachedOnTarget);
		}
	}
	public class CompAbilityEffect_MoteDualAttachedOnTarget : CompAbilityEffect
	{
		public new CompProperties_AbilityMoteDualAttachedOnTarget Props => (CompProperties_AbilityMoteDualAttachedOnTarget)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			if (Props.preCastTicks <= 0)
			{
				Props.sound?.PlayOneShot(new TargetInfo(target.Cell, parent.pawn.Map));
				SpawnAll(target);
			}
		}

		public override IEnumerable<PreCastAction> GetPreCastActions()
		{
			if (Props.preCastTicks > 0)
			{
				yield return new PreCastAction
				{
					action = delegate (LocalTargetInfo t, LocalTargetInfo d)
					{
						SpawnAll(t);
						Props.sound?.PlayOneShot(new TargetInfo(t.Cell, parent.pawn.Map));
					},
					ticksAwayFromCast = Props.preCastTicks
				};
			}
		}

		private void SpawnAll(LocalTargetInfo target)
		{
			if (!Props.moteDefs.NullOrEmpty())
			{
				for (int i = 0; i < Props.moteDefs.Count; i++)
				{
					SpawnMote(target, Props.moteDefs[i]);
				}
			}
			else
			{
				SpawnMote(target, Props.moteDef);
			}
		}

		private void SpawnMote(LocalTargetInfo target, ThingDef def)
		{
			MoteMaker.MakeInteractionOverlay(def, parent.pawn, target.ToTargetInfo(parent.pawn.Map));
		}
	}
}
