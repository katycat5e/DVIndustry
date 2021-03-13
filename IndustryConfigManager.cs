using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.Logic.Job;
using Newtonsoft.Json;

namespace DVIndustry
{
    public static class IndustryConfigManager
    {
        private static readonly Dictionary<string, IndustryProcess[]> industryConfigs = new Dictionary<string, IndustryProcess[]>();

        private static readonly Dictionary<string, YardTrackInfo[]> yardLoadingTracks = new Dictionary<string, YardTrackInfo[]>();
        private static readonly Dictionary<string, Track[]> yardStagingTracks = new Dictionary<string, Track[]>();

        public static IndustryProcess[] GetProcesses( string stationId )
        {
            if( industryConfigs.TryGetValue(stationId, out var processList) ) return processList;
            else return null;
        }

        public static YardTrackInfo[] GetLoadingTracks( string stationId )
        {
            if( yardLoadingTracks.TryGetValue(stationId, out var trackList) ) return trackList;
            else return null;
        }

        public static Track[] GetStagingTracks( string stationId )
        {
            if( yardStagingTracks.TryGetValue(stationId, out var trackList) ) return trackList;
            else return null;
        }

        public static bool LoadConfig( string configPath )
        {
            JSONIndustryConfigList config;

            // Try to load the config file from disk
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

            // Parse the industry controller configurations
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

                industryConfigs.Add(jsonIndustry.StationId, processList);
            }

            // Parse the yard controller configurations
            foreach( JSONYardConfig jsonYard in config.Yards )
            {
                // parse loading tracks at this yard
                YardTrackInfo[] loadTracks;
                if( (jsonYard.LoadTracks != null) && (jsonYard.LoadTracks.Count > 0) )
                {
                    loadTracks = new YardTrackInfo[jsonYard.LoadTracks.Count];
                    int i = 0;
                    foreach( var kvp in jsonYard.LoadTracks )
                    {
                        Track track = GetTrack(kvp.Key);
                        if( track == null )
                        {
                            DVIndustry.ModEntry.Logger.Critical($"Failed to find loading track {kvp.Key} at {jsonYard.StationId}");
                            return false;
                        }

                        // parse the accepted cargo at this track
                        ResourceClass acceptedLoads = null;
                        if( !string.IsNullOrWhiteSpace(kvp.Value) && !kvp.Value.Equals("Any", StringComparison.CurrentCultureIgnoreCase) )
                        {
                            // has a cargo filter set

                            // this was gonna be for allowing union of multiple classes. Idk if worth it
                            //string[] classNames = kvp.Value.Split('|');
                            //var classes = new ResourceClass[classNames.Length];
                            //for( int j = 0; j < classNames.Length; i++ )
                            //{
                            //    if( !ResourceClass.TryParse(classNames[j], out classes[j]) )
                            //    {
                            //        DVIndustry.ModEntry.Logger.Critical($"Invalid resource filter on track {track.ID} at {jsonYard.StationId}");
                            //        return false;
                            //    }
                            //}

                            if( !ResourceClass.TryParse(kvp.Value, out acceptedLoads) )
                            {
                                DVIndustry.ModEntry.Logger.Critical($"Invalid resource class filter on track {track.ID} at {jsonYard.StationId}");
                                return false;
                            }
                        }

                        loadTracks[i] = new YardTrackInfo(track, acceptedLoads);
                        i++;
                    }
                }
                else loadTracks = new YardTrackInfo[0];

                yardLoadingTracks[jsonYard.StationId] = loadTracks;

                // parse staging tracks
                Track[] stageTracks;
                if( (jsonYard.StagingTracks != null) && (jsonYard.StagingTracks.Length > 0) )
                {
                    stageTracks = new Track[jsonYard.StagingTracks.Length];
                    for( int i = 0; i < stageTracks.Length; i++ )
                    {
                        Track track = GetTrack(jsonYard.StagingTracks[i]);
                        if( track == null )
                        {
                            DVIndustry.ModEntry.Logger.Critical($"Failed to find staging track {jsonYard.StagingTracks[i]} for {jsonYard.StationId}");
                            return false;
                        }

                        stageTracks[i] = track;
                    }
                }
                else stageTracks = new Track[0];

                yardStagingTracks[jsonYard.StationId] = stageTracks;
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

        private static Track GetTrack( string trackId )
        {
            if( SingletonBehaviour<YardTracksOrganizer>.Instance.yardTrackIdToTrack.TryGetValue(trackId, out Track track) )
            {
                return track;
            }
            return null;
        }


        public class JSONIndustryConfigList
        {
            public JSONIndustryConfig[] Industries;
            public JSONYardConfig[] Yards;
        }

        public class JSONIndustryConfig
        {
            public string StationId;
            public JSONIndustryProcess[] Processes;
        }

        public class JSONIndustryProcess
        {
            public float Time;

            public Dictionary<string, float> Inputs;
            public Dictionary<string, float> Outputs;
        }

        public class JSONYardConfig
        {
            public string StationId;
            public Dictionary<string, string> LoadTracks;
            public string[] StagingTracks;
        }
    }
}
