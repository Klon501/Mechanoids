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
	public class GestatorExtension : DefModExtension
	{
	}

	[HarmonyPatch(typeof(Bill_Mech), nameof(Bill_Mech.PawnAllowedToStartAnew))]
	public static class Bill_Mech_PawnAllowedToStartAnew
	{
		public static void Prefix(ref Pawn p)
		{
			if (p.def.HasModExtension<GestatorExtension>() && p.GetOverseer() != null)
			{
				p = p.GetOverseer();
			}
		}
	}

	[HarmonyPatch(typeof(Bill_Mech), nameof(Bill_Mech.Notify_DoBillStarted))]
	public static class Bill_Mech_Notify_DoBillStarted
	{
		public static void Postfix(ref Pawn ___boundPawn, Pawn billDoer)
		{
			if (billDoer.def.HasModExtension<GestatorExtension>())
			{
				___boundPawn = billDoer.GetOverseer();
			}
		}
	}
}
