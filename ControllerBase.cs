using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVIndustry
{
    public abstract class ControllerBase<T, SaveType> : MonoBehaviour
        where T : ControllerBase<T, SaveType>
    {
        protected StationController AttachedStation = null;

        public string StationId => AttachedStation?.stationInfo.YardID;

        protected static readonly Dictionary<string, T> controllerDict =
               new Dictionary<string, T>();

        public static T At( string yardId )
        {
            if( controllerDict.TryGetValue(yardId, out var controller) ) return controller;
            else return null;
        }

        public static IEnumerable<T> AllControllers => controllerDict.Values;
        public static int ControllerCount => controllerDict.Count;

        protected void RegisterController( string id, T controller )
        {
            controllerDict[id] = controller;
        }

        public abstract SaveType GetSaveData();
        public abstract void ApplySaveData( SaveType data );
    }
}
