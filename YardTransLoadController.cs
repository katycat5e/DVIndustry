using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DV.Logic.Job;

namespace DVIndustry
{
    public class YardTransLoadController : ControllerBase<YardTransLoadController>
    {
        private const float LOAD_UNLOAD_DELAY = 10f;
        private const float CAR_STOPPED_EPSILON = 0.2f;

        public IndustryController AttachedIndustry = null;
        public StationController StationController = null;

        private readonly Queue<List<TrainCar>> incomingConsistQueue = new Queue<List<TrainCar>>();
        private List<TrainCar> currentUnloadConsist = null;
        private int currentUnloadIdx = 0;

        private float lastUnloadTime = 0f;

        public void HandleIncomingConsist( List<TrainCar> consist )
        {
            incomingConsistQueue.Enqueue(consist);
        }

        void OnEnable()
        {
            AttachedIndustry = gameObject.GetComponent<IndustryController>();
            StationController = gameObject.GetComponent<StationController>();

            RegisterController(StationController.stationInfo.YardID, this);
        }

        void Update()
        {
            if( (currentUnloadConsist == null) || (currentUnloadIdx >= currentUnloadConsist.Count) )
            {
                if( !incomingConsistQueue.TryDequeue(out currentUnloadConsist) )
                {
                    currentUnloadConsist = null;
                }
                currentUnloadIdx = 0;
            }

            float curTime = Time.time;
            if( (currentUnloadConsist != null) && ((curTime - lastUnloadTime) >= LOAD_UNLOAD_DELAY) )
            {
                // Unload a single car from the current consist
                lastUnloadTime = curTime;

                TrainCar curCar = currentUnloadConsist[currentUnloadIdx];
                if( Math.Abs(curCar.GetForwardSpeed()) < CAR_STOPPED_EPSILON )
                {
                    // car is stationary, let us proceed...
                    Car lCar = curCar.logicCar;
                    CargoType loadedType = lCar.CurrentCargoTypeInCar;
                    float loadedAmount = lCar.LoadedCargoAmount;

                    DVIndustry.ModEntry.Logger.Log($"Unloading {loadedAmount} of {loadedType} from {lCar.ID}");

                    lCar.UnloadCargo(loadedAmount, loadedType);
                    AttachedIndustry.StoreInputCargo(loadedType, loadedAmount);
                    currentUnloadIdx += 1; // move to next car in consist
                }
            }
        }
    }
}
