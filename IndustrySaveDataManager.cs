using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;

namespace DVIndustry
{
    public static class IndustrySaveDataManager
    {
        const string SAVE_KEY = "DVIndustry";
        private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static bool IsLoadCompleted { get; private set; } = false;

        private static readonly Dictionary<string, List<IndustryResource>> stockpileSaveData = new Dictionary<string, List<IndustryResource>>();

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

        private static IEnumerator<IndustryResource> ProcessStockpiles( IndustrySaveDataItem industryData )
        {
            if( industryData.StockPiles == null ) yield break;

            foreach( var kvp in industryData.StockPiles )
            {
                if( IndustryResource.TryParse(kvp.Key, kvp.Value, out IndustryResource nextRes) )
                {
                    // successfully parsed
                    yield return nextRes;
                }
                else
                {
                    DVIndustry.ModEntry.Logger.Warning($"Invalid stockpile resource at {industryData.StationId}");
                }
            }

            yield break;
        }

        public static void LoadIndustryData()
        {
            if( SaveGameManager.data.GetObject<IndustrySaveData>(SAVE_KEY, serializerSettings) is IndustrySaveData saveData )
            {
                // found DVIndustry save data
                // Restore saved stockpile amounts
                foreach( IndustrySaveDataItem industry in saveData.Industries )
                {
                    var stockList = new List<IndustryResource>();

                    IEnumerator<IndustryResource> stocks = ProcessStockpiles(industry);
                    while( stocks.MoveNext() )
                    {
                        stockList.Add(stocks.Current);
                    }

                    stockpileSaveData[industry.StationId] = stockList;

                    if( IndustryController.At(industry.StationId) is IndustryController controller )
                    {
                        foreach( IndustryResource resource in stockList )
                        {
                            controller.StoreResource(resource);
                        }
                    }
                }

                // 
            }

            IsLoadCompleted = true;
            DVIndustry.ModEntry.Logger.Log("Loaded industry save data");
        }

        public static void SaveIndustryData()
        {
            var saveItems = new IndustrySaveDataItem[IndustryController.ControllerCount];

            int i = 0;
            foreach( var industry in IndustryController.AllControllers )
            {
                var resources = industry.AllResources;
                stockpileSaveData[industry.StationId] = resources.ToList();
                saveItems[i] = new IndustrySaveDataItem(industry.StationId, resources);
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
