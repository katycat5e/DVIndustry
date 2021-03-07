using System.Collections.Generic;
using System.Linq;

namespace DVIndustry
{
    public class IndustrySaveDataCollection
    {
        public IndustrySaveData[] Industries;
        public YardControllerSaveData[] Yards;
    }

    public class IndustrySaveData
    {
        public string StationId = null;
        public Dictionary<string, float> StockPiles = null;

        public IndustrySaveData() { }

        public IndustrySaveData( string station, IEnumerable<IndustryResource> stocks )
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
        public string[][] IncomingCars;
        public string[] CurrentUnloadCars;
        public int CurrentUnloadIdx;
    }
}
