using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DVIndustry
{
    public static class IndustryConfigManager
    {
        private static readonly Dictionary<string, IndustryProcess[]> IndustryConfigs = new Dictionary<string, IndustryProcess[]>();

        public static IndustryProcess[] GetProcesses( string stationId )
        {
            if( IndustryConfigs.TryGetValue(stationId, out var processList) ) return processList;
            else return null;
        }

        public static bool LoadConfig( string configPath )
        {
            JSONIndustryConfigList config;

            try
            {
                string json = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<JSONIndustryConfigList>(json);
            }
            catch( Exception ex )
            {
                DVIndustry.ModEntry.Logger.Critical($"Failed to open industry config file at \"{configPath}\":");
                DVIndustry.ModEntry.Logger.Error(ex.Message);
                return false;
            }

            if( config == null )
            {
                DVIndustry.ModEntry.Logger.Critical($"Failed to parse industry config file at \"{configPath}\":");
                return false;
            }

            foreach( JSONIndustryConfig jsonIndustry in config.Industries )
            {
                var processList = new IndustryProcess[jsonIndustry.Processes.Length];
                for( int i = 0; i < jsonIndustry.Processes.Length; i++ )
                {
                    if( TryConvertProcess(jsonIndustry.Processes[i], out IndustryProcess process) )
                    {
                        processList[i] = process;
                    }
                    else
                    {
                        DVIndustry.ModEntry.Logger.Critical($"Error loading processes for industry at {jsonIndustry.StationId}");
                        return false;
                    }
                }

                IndustryConfigs.Add(jsonIndustry.StationId, processList);
            }

            return true;
        }

        private static bool TryConvertProcess( JSONIndustryProcess jsonProcess, out IndustryProcess process )
        {
            process = new IndustryProcess() { ProcessingTime = jsonProcess.Time };

            process.Inputs = new IndustryResource[jsonProcess.Inputs.Count];
            int i = 0;
            foreach( var kvp in jsonProcess.Inputs )
            {
                if( IndustryResource.TryParse(kvp.Key, kvp.Value, out IndustryResource resource) )
                {
                    process.Inputs[i] = resource;
                }
                else
                {
                    DVIndustry.ModEntry.Logger.Critical("Invalid process input");
                    return false;
                }

                i++;
            }

            process.Outputs = new IndustryResource[jsonProcess.Outputs.Count];
            i = 0;
            foreach( var kvp in jsonProcess.Outputs )
            {
                if( IndustryResource.TryParse(kvp.Key, kvp.Value, out IndustryResource resource) )
                {
                    process.Outputs[i] = resource;
                }
                else
                {
                    DVIndustry.ModEntry.Logger.Critical("Invalid process output");
                    return false;
                }

                i++;
            }

            return true;
        }


        private class JSONIndustryConfigList
        {
            public JSONIndustryConfig[] Industries;
        }

        private class JSONIndustryConfig
        {
            public string StationId;
            public JSONIndustryProcess[] Processes;
        }

        private class JSONIndustryProcess
        {
            public float Time;

            public Dictionary<string, float> Inputs;
            public Dictionary<string, float> Outputs;
        }
    }
}
