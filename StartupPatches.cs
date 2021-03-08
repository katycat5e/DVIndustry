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

                var loadTracks = IndustryConfigManager.GetLoadingTracks(yardId);
                var stageTracks = IndustryConfigManager.GetStagingTracks(yardId);
                if( loadTracks == null || stageTracks == null )
                {
                    DVIndustry.ModEntry.Logger.Error($"Industry configured, but missing yard config at {yardId}");
                    return;
                }

                newIndustry = __instance.gameObject.AddComponent<IndustryController>();
                newIndustry.Initialize(processes);
                DVIndustry.ModEntry.Logger.Log($"Added industry controller to {yardId}");

                var loadController = __instance.gameObject.AddComponent<YardController>();
                loadController.Initialize(loadTracks, stageTracks);
                DVIndustry.ModEntry.Logger.Log($"Added yard load/unload controller to {yardId}");
            }
        }
    }
}
