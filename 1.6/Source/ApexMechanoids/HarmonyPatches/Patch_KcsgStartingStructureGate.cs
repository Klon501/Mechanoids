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
            if (!OptedIn())
            {
                return;
            }

            if (!(AccessTools.Field(AccessTools.TypeByName(UtilTypeName), "pcScenariosSave")
                    .GetValue(null) is IDictionary save)
                || save.Count > 0)
            {
                return;
            }

            if (!(AccessTools.Field(__instance.GetType(), "chooseFrom").GetValue(__instance) is IList chooseFrom)
                || chooseFrom.Count == 0)
            {
                return;
            }

            bool nearMapCenter =
                AccessTools.Field(__instance.GetType(), "nearMapCenter").GetValue(__instance) is bool near && near;

            save.Add(chooseFrom[0], nearMapCenter);
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
        /// Only for our own scenarios: they are the ones that declare the site-preparation part.
        /// </summary>
        private static bool OptedIn()
        {
            Scenario scenario = Find.Scenario;
            return scenario != null
                && scenario.AllParts.Any(part =>
                    part?.def != null && part.def.defName == "APM_PrepareStructureSite");
        }
    }
}
