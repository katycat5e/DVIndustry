using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DVIndustry
{
    [HarmonyPatch(typeof(StationController), "Start")]
    static class StationController_Awake_Patch
    {
        private static readonly HashSet<string> ManagedYards = new HashSet<string>() { "CSW", "MF", "FF", "HB", "GF" };

        static void Postfix( StationController __instance )
        {
            string yardId = __instance.stationInfo.YardID;
            if( !ManagedYards.Contains(yardId) ) return;

            var newIndustry = __instance.gameObject.GetComponent<IndustryController>();
            if( newIndustry == null )
            {
                newIndustry = __instance.gameObject.AddComponent<IndustryController>();
                newIndustry.Initialize();
                DVIndustry.ModEntry.Logger.Log($"Added industry controller to {yardId}");
            }
        }
    }
}
