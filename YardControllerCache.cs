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
        public float LastXferTime;
        public float LastShuntTime;

        public List<YCC_CarData> EmptyPool;
        public List<YCC_ConsistData> UnloadConsists;
        public List<YCC_ConsistData> LoadConsists;

        public void ResetTimers()
        {
            LastXferTime = LastShuntTime = UnityEngine.Time.time;
        }

        // TODO: flesh this out, add logic for auto-shunting
    }

    internal class YCC_ConsistData
    {
        public YardConsistState State;
        public int NLoaded;
        public readonly List<YCC_CarData> Cars = new List<YCC_CarData>();
    }

    internal class YCC_CarData
    {
        public readonly TrainCarType CarType;
        public CargoType LoadedCargo;

        public YCC_CarData( TrainCarType carType, CargoType load )
        {
            CarType = carType;
            LoadedCargo = load;
        }
    }
}
