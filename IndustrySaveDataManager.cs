using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        const string SAVE_KEY = "DVIndustry";
        private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static void LoadIndustrySaveData()
        {
            if( SaveGameManager.data.GetObject<IndustrySaveData>(SAVE_KEY, serializerSettings) is IndustrySaveData saveData )
            {
                foreach( IndustrySaveDataItem industry in saveData.Industries )
                {
                    stockpileSaveData[industry.StationId] = industry.StockPiles.ToList();
                }
            }
        }

        public static void SetIndustryData( string stationId, IEnumerable<IndustryResource> resources )
        {
            stockpileSaveData[stationId] = resources.ToList();
        }

        public static void ApplySaveData()
        {
            var industryData = new IndustrySaveData()
            {
                Industries = stockpileSaveData.Select(kvp => new IndustrySaveDataItem(kvp.Key, kvp.Value)).ToArray()
            };

            SaveGameManager.data.SetObject(SAVE_KEY, industryData, serializerSettings);
        }
    }
}
