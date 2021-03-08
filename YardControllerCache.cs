using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;

namespace DVIndustry
{
    internal class YardControllerCache
    {
        public List<YardControlCarData> EmptyPool;
        public List<YardControlConsistData> UnloadConsists;
        public List<YardControlConsistData> LoadConsists;

        // TODO: flesh this out, add logic for auto-shunting
    }

    internal class YardControlConsistData
    {
        public int NLoaded;
        public readonly List<YardControlCarData> Cars = new List<YardControlCarData>();
    }

    internal class YardControlCarData
    {
        public readonly TrainCarType CarType;
        public CargoType LoadedCargo;

        public YardControlCarData( TrainCarType carType, CargoType load )
        {
            CarType = carType;
            LoadedCargo = load;
        }
    }
}
