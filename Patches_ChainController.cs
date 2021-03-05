using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;
using HarmonyLib;

namespace DVIndustry
{
    // JobChainController.OnJobCompleted
    [HarmonyPatch(typeof(JobChainController), "OnJobCompleted")]
    static class ChainController_OnJobCompleted_Patch
    {
        static void Postfix( Job completedJob, JobChainController __instance )
        {
            if( completedJob.jobType != JobType.Transport ) return;

            string destYardId = completedJob.chainData.chainDestinationYardId;
            if( YardTransLoadController.At(destYardId) is YardTransLoadController destYard )
            {
                destYard.HandleIncomingConsist(__instance.trainCarsForJobChain);
            }
        }
    }
}
