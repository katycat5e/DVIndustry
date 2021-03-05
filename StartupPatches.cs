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
        static void Postfix( StationController __instance )
        {
            string yardId = __instance.stationInfo.YardID;

            var newIndustry = __instance.gameObject.GetComponent<IndustryController>();
            if( newIndustry == null )
            {
                // check if industry is configured for this station
                var processes = IndustryConfigManager.GetProcesses(yardId);
                if( processes == null ) return;

                newIndustry = __instance.gameObject.AddComponent<IndustryController>();
                newIndustry.Initialize(processes);
                DVIndustry.ModEntry.Logger.Log($"Added industry controller to {yardId}");

                var loadController = __instance.gameObject.AddComponent<YardTransLoadController>();
                DVIndustry.ModEntry.Logger.Log($"Added yard load/unload controller to {yardId}");
            }
        }
    }
}
