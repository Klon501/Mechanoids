using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexMechanoids
{
    [Verse.StaticConstructorOnStartup]
    internal static class StartUp
    {
        static HarmonyLib.Harmony harmony;
        static StartUp()
        {
            harmony = new HarmonyLib.Harmony("ApexMechanoids");
            harmony.PatchAll();
        }
    }
}
