using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;

namespace DVIndustry
{
    public class IndustrySaveDataItem
    {
        public readonly string StationId;
        public readonly IndustryResource[] StockPiles;

        public IndustrySaveDataItem( string station, IEnumerable<IndustryResource> stocks )
        {
            StationId = station;
            StockPiles = stocks.ToArray();
        }
    }

    public class IndustrySaveData
    {
        public IndustrySaveDataItem[] Industries;
    }

    public static class IndustrySaveDataManager
    {
        const string SAVE_KEY = "DVIndustry";
        private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static bool IsLoadCompleted { get; private set; } = false;

        private static readonly List<IndustryController> trackedIndustries = new List<IndustryController>();
        private static readonly Dictionary<string, List<IndustryResource>> stockpileSaveData = new Dictionary<string, List<IndustryResource>>();

        public static void RegisterIndustry( IndustryController industry )
        {
            trackedIndustries.Add(industry);
        }

        public static float GetSavedStockpileAmount( string stationId, string resourceId )
        {
            if( stockpileSaveData.TryGetValue(stationId, out var resourceList) )
            {
                foreach( IndustryResource r in resourceList )
                {
                    if( string.Equals(resourceId, r.Key) ) return r.Amount;
                }
            }

            return 0f;
        }

        public static void LoadIndustryData()
        {
            if( SaveGameManager.data.GetObject<IndustrySaveData>(SAVE_KEY, serializerSettings) is IndustrySaveData saveData )
            {
                foreach( IndustrySaveDataItem industry in saveData.Industries )
                {
                    stockpileSaveData[industry.StationId] = industry.StockPiles.ToList();
                }
            }

            foreach( IndustryController controller in trackedIndustries )
            {
                if( stockpileSaveData.TryGetValue(controller.StationId, out var resourceList) )
                {
                    foreach( IndustryResource resource in resourceList )
                    {
                        controller.StoreResource(resource);
                    }
                }
            }

            IsLoadCompleted = true;
            DVIndustry.ModEntry.Logger.Log("Loaded industry save data");
        }

        public static void SaveIndustryData()
        {
            var saveItems = new IndustrySaveDataItem[trackedIndustries.Count];

            for( int i = 0; i < trackedIndustries.Count; i++ )
            {
                var resources = trackedIndustries[i].AllResources;
                stockpileSaveData[trackedIndustries[i].StationId] = resources.ToList();
                saveItems[i] = new IndustrySaveDataItem(trackedIndustries[i].StationId, resources);
            }

            var industryData = new IndustrySaveData()
            {
                Industries = saveItems
            };

            SaveGameManager.data.SetObject(SAVE_KEY, industryData, serializerSettings);
        }
    }

    [HarmonyPatch(typeof(JobSaveManager), "LoadJobSaveGameData")]
    static class OnGameLoad_Patch
    {
        static void Postfix()
        {
            IndustrySaveDataManager.LoadIndustryData();
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    static class OnGameSave_Patch
    {
        // inject our save data before the IO is performed
        static void Prefix()
        {
            IndustrySaveDataManager.SaveIndustryData();
        }
    }
}
