using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DVIndustry
{
    [HarmonyPatch(typeof(StationController), "ExpireAllAvailableJobsInStation")]
    static class StationController_ExpireJobs_Patch
    {
        static void Prefix( StationController __instance )
        {
            if( YardController.At(__instance.stationInfo.YardID) is YardController yard )
            {

            }
        }
    }
}
