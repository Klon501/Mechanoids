using HarmonyLib;
using Verse;



namespace ApexMechanoids
{
    /*
    [HarmonyPatch(typeof(ThingOwnerUtility))]
    [HarmonyPatch("ContentsSuspended")]
    public static class Temporary_ThingOwnerUtility_ContentsSuspended_Patch
    {

        [HarmonyPostfix]
        public static void ContentsSuspended(ref bool __result, IThingHolder holder)
        {
            if(__result == true)
            {
                if (holder is Building_MechCommandCasket)
                {
                    __result = false;   //allows hediffs to tick while insside the casket
                }
            }

        }
    }
    */
}
