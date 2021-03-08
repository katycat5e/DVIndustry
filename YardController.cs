using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DV.Logic.Job;

namespace DVIndustry
{
    public class YardTrackInfo
    {
        public readonly Track Track;
        public readonly ResourceClass LoadingClass;
        public YardConsistState CurrentUse = YardConsistState.None;

        public double AvailableSpace => Track.length - Track.OccupiedLength;

        public YardTrackInfo( Track track, ResourceClass loadResource = null )
        {
            Track = track;
            LoadingClass = loadResource;
        }
    }

    public class YardController : ControllerBase<YardController, YardControllerSaveData>
    {
        private const float LOAD_UNLOAD_DELAY = 10f;
        private const float CAR_STOPPED_EPSILON = 0.2f;

        public bool InAutoMode = false;

        private IndustryController AttachedIndustry = null;

        private readonly List<YardControlConsist> consistList = new List<YardControlConsist>();
        private YardTrackInfo[] LoadingTracks;
        private YardTrackInfo[] StagingTracks;

        private float lastUnloadTime = 0f;


        public void Initialize( YardTrackInfo[] loadTracks, YardTrackInfo[] stagingTracks )
        {
            LoadingTracks = loadTracks;
            StagingTracks = stagingTracks;
        }

        void OnEnable()
        {
            AttachedIndustry = gameObject.GetComponent<IndustryController>();
            AttachedStation = gameObject.GetComponent<StationController>();

            RegisterController(AttachedStation.stationInfo.YardID, this);
        }

        void Update()
        {
            float curTime = Time.time;

            // check if any consists are done

            if( (curTime - lastUnloadTime) >= LOAD_UNLOAD_DELAY )
            {
                // Transfer load on a car from each loading/unloading consist
                lastUnloadTime = curTime;

                foreach( YardControlConsist consist in consistList )
                {
                    switch( consist.State )
                    {
                        case YardConsistState.Loading:
                            LoadOneCar(consist);
                            break;

                        case YardConsistState.Unloading:
                            UnloadOneCar(consist);
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private static bool CarIsStationary( TrainCar car ) => Math.Abs(car.GetForwardSpeed()) < CAR_STOPPED_EPSILON;
        
        private void LoadOneCar( YardControlConsist consist )
        {
            TrainCar car = consist.FirstOrDefault(c => c.LoadedCargoAmount < 1f);
            if( car == null )
            {
                consist.State = YardConsistState.Full;
                return;
            }

            if( !CarIsStationary(car) ) return;

            CargoType toLoad = consist.LoadResource.GetCargoForCar(car.carType);
            car.logicCar.LoadCargo(1f, toLoad);

#if DEBUG
            DVIndustry.ModEntry.Logger.Log($"{StationId} - Loaded {toLoad} to {car.ID}");
#endif
        }

        private void UnloadOneCar( YardControlConsist consist )
        {
            TrainCar car = consist.FirstOrDefault(c => c.LoadedCargoAmount > 0f);
            if( car == null )
            {
                consist.State = YardConsistState.Empty;
                return;
            }

            if( !CarIsStationary(car) ) return;

            CargoType toUnload = car.LoadedCargo;
            car.logicCar.UnloadCargo(car.LoadedCargoAmount, toUnload);
            AttachedIndustry.StoreInputCargo(toUnload, 1f);

#if DEBUG
            DVIndustry.ModEntry.Logger.Log($"{StationId} - Unloaded {toUnload} from {car.ID}");
#endif
        }

        public void SaveCarStates()
        {

        }

        // ControllerBase interface
        public override YardControllerSaveData GetSaveData()
        {
            return null;
            //List<TrainCar>[] consists = consistList.ToArray();

            //string[][] waitingCarGuids = new string[consists.Length][];
            //for( int i = 0; i < consists.Length; i++ )
            //{
            //    waitingCarGuids[i] = consists[i].Select(car => car.CarGUID).ToArray();
            //}

            //return new YardControllerSaveData()
            //{
            //    StationId = StationId,
            //    IncomingCars = waitingCarGuids,
            //    CurrentUnloadCars = currentUnloadConsist.Select(car => car.CarGUID).ToArray(),
            //    CurrentUnloadIdx = currentUnloadIdx
            //};
        }

        public override void ApplySaveData( YardControllerSaveData data )
        {
            //foreach( string[] consistIds in data.IncomingCars )
            //{
            //    List<TrainCar> parsedCars = Utilities.GetTrainCarsFromCarGuids(consistIds);
            //    if( parsedCars != null )
            //    {
            //        consistList.Enqueue(parsedCars);
            //    }
            //    else
            //    {
            //        DVIndustry.ModEntry.Logger.Warning($"Couldn't find incoming consist for yard controller at {StationId}");
            //    }
            //}

            //if( (data.CurrentUnloadCars != null) && (data.CurrentUnloadCars.Length > 0) )
            //{
            //    currentUnloadConsist = Utilities.GetTrainCarsFromCarGuids(data.CurrentUnloadCars);
            //    if( currentUnloadConsist != null )
            //    {
            //        currentUnloadIdx = data.CurrentUnloadIdx;
            //    }
            //    else
            //    {
            //        DVIndustry.ModEntry.Logger.Warning($"Couldn't find unloading consist for yard controller at {StationId}");
            //        currentUnloadConsist = null;
            //        currentUnloadIdx = 0;
            //    }
            //}
            //else
            //{
            //    currentUnloadConsist = null;
            //    currentUnloadIdx = 0;
            //}
        }
    }
}
