using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    [HarmonyLib.HarmonyPatch(typeof(ThoughtWorker_PsychicBondProximity), nameof(ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson))]
    public class ThoughtWorker_PsychicBondProximity_Patch
    {
        private static void Postfix(ThoughtWorker_PsychicBondProximity __instance, ref bool __result, Pawn pawn, Hediff_PsychicBond bondHediff)
        {
            if (__result == false)
            {
                Thing thing = bondHediff?.target;
                Pawn bondedPawn = thing as Pawn;
                if (bondedPawn != null)
                {
                    if (Utils.IsUplinkActiveFor(pawn) || Utils.IsUplinkActiveFor(bondedPawn))
                    {
                        __result = pawn.MapHeld == bondedPawn.MapHeld;
                    }
                }
            }
        }
    }
   
}
