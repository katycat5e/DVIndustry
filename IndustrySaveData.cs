using System.Collections.Generic;
using System.Linq;

namespace DVIndustry
{
    public class IndustrySaveData
    {
        public IndustrySaveDataItem[] Industries;
        public YardControllerSaveData[] Yards;
    }

    public class IndustrySaveDataItem
    {
        public string StationId = null;
        public Dictionary<string, float> StockPiles = null;

        public IndustrySaveDataItem() { }

        public IndustrySaveDataItem( string station, IEnumerable<IndustryResource> stocks )
        {
            StationId = station;
            if( stocks == null )
            {
                StockPiles = new Dictionary<string, float>();
            }
            else
            {
                StockPiles = stocks.ToDictionary(s => s.Key, s => s.Amount);
            }
        }
    }

    public class YardControllerSaveData
    {
        public string StationId = null;
        public string[][] incomingCars;
        public string[] currentUnloadCars;
        public int currentUnloadIdx;
    }
}
