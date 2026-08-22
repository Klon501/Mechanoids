using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// Makes the starting structure build on every start, not just the ones that went through the
    /// Configure Starting Pawns page.
    ///
    /// <c>KCSG.ScenPart_AddStartingStructure.PostMapGenerate</c> opens with
    ///
    /// <code>
    /// if (TicksGame > 5f || chooseFrom.Count &lt;= 0 || PrepareCarefully_Util.pcScenariosSave.Count &lt;= 0)
    ///     return;
    /// </code>
    ///
    /// and `pcScenariosSave` is filled from one place only: a Harmony postfix on
    /// <c>Page_ConfigureStartingPawns.PreOpen</c>. Any start that does not open that page — dev-mode
    /// quicktest above all — leaves it empty, and the scen part returns having built nothing. No
    /// error is logged and the map is otherwise fine, which is exactly the "sometimes the structure
    /// doesn't spawn" report.
    ///
    /// The dictionary's contents are only read when Prepare Carefully is installed; without it KCSG
    /// re-rolls from <c>chooseFrom</c> anyway. So the entry seeded here exists purely to get past the
    /// count check, and a start that filled the dictionary properly is left untouched.
    ///
    /// Scoped to scenarios that include <see cref="ScenPart_PrepareStructureSite"/>, so this only
    /// ever changes this mod's own starts and never another mod's KCSG scenario.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_KcsgStartingStructureGate
    {
        private const string ScenPartTypeName = "KCSG.ScenPart_AddStartingStructure";

        private const string UtilTypeName = "KCSG.PrepareCarefully_Util";

        private static bool Prepare()
        {
            return TargetMethod() != null
                && AccessTools.Field(AccessTools.TypeByName(UtilTypeName), "pcScenariosSave") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName(ScenPartTypeName), "PostMapGenerate");
        }

        private static void Prefix(object __instance)
        {
            IList chooseFrom = AccessTools.Field(__instance.GetType(), "chooseFrom").GetValue(__instance) as IList;

            if (!OptedIn(chooseFrom))
            {
                return;
            }

            IDictionary save = AccessTools.Field(AccessTools.TypeByName(UtilTypeName), "pcScenariosSave")
                .GetValue(null) as IDictionary;

            if (save != null && save.Count == 0 && chooseFrom != null && chooseFrom.Count > 0)
            {
                bool nearMapCenter =
                    AccessTools.Field(__instance.GetType(), "nearMapCenter").GetValue(__instance) is bool near && near;
                save.Add(chooseFrom[0], nearMapCenter);
            }

            WarnIfStillGated(save, chooseFrom);
        }

        /// <summary>
        /// Says so in the log when the structure is about to be skipped for a reason this patch
        /// cannot seed its way past.
        ///
        /// KCSG returns from that guard in silence, which is why "the domain sometimes isn't there"
        /// has never come with anything to go on. The two remaining causes look nothing alike -- a
        /// game already running when the map is made, against a part with no layouts to choose from
        /// -- and a single line saying which one it was turns the next report into something that
        /// can be answered.
        /// </summary>
        private static void WarnIfStillGated(IDictionary save, IList chooseFrom)
        {
            int ticks = Find.TickManager?.TicksGame ?? 0;
            if (ticks <= 5 && chooseFrom != null && chooseFrom.Count > 0 && save != null && save.Count > 0)
            {
                return;
            }

            Log.Warning("[Apex Mechanoids] The starting structure will not be built. KCSG's gate is closed: "
                + "ticksGame=" + ticks + " (must be 5 or less), "
                + "layouts=" + (chooseFrom?.Count.ToString() ?? "unreadable") + " (must be at least 1), "
                + "prepareCarefullyEntries=" + (save?.Count.ToString() ?? "unreadable") + " (must be at least 1). "
                + "A map generated after the game has started ticking is the usual cause.");
        }

        /// <summary>
        /// The structure now exists, so this is the first moment its real extent can be read. The
        /// site was prepared for the largest layout it might have been; <see cref="StructureSiteCleanup"/>
        /// cleans what was actually taken and gives the rest back.
        ///
        /// A postfix rather than another scen part: this has to run after KCSG whatever order the
        /// scenario lists its parts in, and the cleanup is only ever armed by our own scen part.
        /// </summary>
        private static void Postfix(Map map)
        {
            StructureSiteCleanup.Run(map);
        }

        /// <summary>
        /// Only for our own starts, never another mod's KCSG scenario.
        ///
        /// The shipped scenario is recognised by the site-preparation part it declares. A scenario
        /// somebody built themselves in the editor out of our domain layouts will not have that
        /// part, and used to fall straight through this and build nothing, so the layouts being
        /// built are the second way in.
        /// </summary>
        private static bool OptedIn(IList chooseFrom)
        {
            Scenario scenario = Find.Scenario;
            if (scenario != null && scenario.AllParts.Any(part =>
                part?.def != null && part.def.defName == "APM_PrepareStructureSite"))
            {
                return true;
            }

            if (chooseFrom == null)
            {
                return false;
            }

            foreach (object layout in chooseFrom)
            {
                if (layout is Def def && def.defName != null && def.defName.StartsWith(OurLayoutPrefix))
                {
                    return true;
                }
            }
            return false;
        }

        private const string OurLayoutPrefix = "APM_";
    }
}
