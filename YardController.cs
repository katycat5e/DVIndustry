using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DV.Logic.Job;
using DVIndustry.Jobs;

namespace DVIndustry
{
    public class YardTrackInfo
    {
        public readonly Track Track;
        public readonly ResourceClass LoadingClass;
        public bool Claimed => (Track.OccupiedLength + YardTracksOrganizer.Instance.GetReservedSpace(Track)) > 0;

        public double AvailableSpace => Track.length - Track.OccupiedLength - YardTracksOrganizer.Instance.GetReservedSpace(Track);

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

        public bool InForegroundMode { get; private set; } = false;
        private bool playerInRange = false;
        private bool playerWasInRange = false;

        private IndustryController AttachedIndustry = null;

        private readonly FancyLinkedList<YardControlConsist> activeConsists = new FancyLinkedList<YardControlConsist>();
        private readonly FancyLinkedList<YardControlConsist> emptyConsists = new FancyLinkedList<YardControlConsist>();

        private YardTrackInfo[] loadingTracks;
        private Track[] stagingTracks;
        private static readonly Dictionary<Track, YardTrackInfo> loadTrackMap = new Dictionary<Track, YardTrackInfo>();

        private bool IsOnLoadingTrack( YardControlConsist consist ) => loadTrackMap.ContainsKey(consist.Track);

        private ProductRequestCollection[] outputsDemand = null;


        public void Initialize( YardTrackInfo[] loadTracks, Track[] stageTracks )
        {
            loadingTracks = loadTracks;
            stagingTracks = stageTracks;
            
            foreach( var lt in loadTracks ) loadTrackMap.Add(lt.Track, lt);
        }

        void OnEnable()
        {
            AttachedIndustry = gameObject.GetComponent<IndustryController>();
            AttachedStation = gameObject.GetComponent<StationController>();

            RegisterController(AttachedStation.stationInfo.YardID, this);
        }

        //============================================================================================
        #region Monobehaviour Update Handling

        void Update()
        {
            // wait for loading to finish
            if( !IndustrySaveDataManager.IsLoadCompleted ) return;

            // check if demand cache needs initialized
            if( outputsDemand == null )
            {
                // sort most valuable outputs first
                outputsDemand = AttachedIndustry.OutputResources
                    .Select(resName => new ProductRequestCollection(ResourceClass.Parse(resName)))
                    .OrderByDescending(reqColl => reqColl.Resource.AverageValue)
                    .ToArray();
            }

            // refresh the demand
            foreach( ProductRequestCollection requestCollection in outputsDemand )
            {
                ShipmentOrganizer.UpdateProductDemand(requestCollection);
            }

            // check for player presence
            float playerDistance = StationRange.PlayerSqrDistanceFromStationCenter;
            bool playerTookJob = AttachedStation.logicStation.takenJobs.Count > 0;

            // hysteresis to prevent walking in and out of range
            bool inGenRange = StationRange.IsPlayerInJobGenerationZone(playerDistance);
            bool outDestroyRange = StationRange.IsPlayerOutOfJobDestroyZone(playerDistance, playerTookJob);

            if( inGenRange ) playerInRange = true;
            else if( outDestroyRange ) playerInRange = false;
            
            if( playerInRange && !playerWasInRange )
            {
                // just entered
                SwitchToForegroundMode();
            }
            else if( !playerInRange && playerWasInRange )
            {
                // just left
                SwitchToBackgroundMode();
            }
            else if( playerInRange )
            {
                // normal in-range processing
                ForegroundUpdate();
            }
            else
            {
                // not in range
                BackgroundUpdate();
            }

            playerWasInRange = playerInRange;
        }

        private void SwitchToBackgroundMode()
        {
            InForegroundMode = false;
        }

        private void SwitchToForegroundMode()
        {
            InForegroundMode = true;
        }

        private void ForegroundUpdate()
        {
            float curTime = Time.time;

            // handle consists on the loading tracks first
            foreach( YardControlConsist consist in activeConsists )
            {
                switch( consist.State )
                {
                    case YardConsistState.Empty:
                        if( IsOnLoadingTrack(consist) )
                        {
                            // check if consist is assignable to a new shipment
                            // requests are sorted by cargo value, then car count
                            ProductRequest matchingRequest = outputsDemand
                                .Where(reqColl => consist.CanHoldResource(reqColl.Resource))
                                .SelectMany(reqColl => reqColl.Requests)
                                .FirstOrDefault();

                            if( matchingRequest != null )
                            {
                                // found a match, first entry is best candidate
                                AssignShipmentToConsist(matchingRequest, consist);
                                consist.State = YardConsistState.Loading;
                                consist.LastUpdateTime = curTime;
                                break;
                            }

                            // empty consist is taking up loading track, get it out of here if possible
                            float len = consist.Length;
                            Track shuntCandidate = YardUtil.FindBestFitTrack(stagingTracks, len);
                            if( shuntCandidate != null )
                            {
                                // yay, we can (make the player) move the consist
                                Job storeJob = JobGenerator.CreateShuntingStoreJob(AttachedStation, consist, shuntCandidate);
                                consist.JobEnded += OnConsistStoreJobEnded;
                            }
                        }
                        break;

                    case YardConsistState.Loading:
                        if( (curTime - consist.LastUpdateTime) >= LOAD_UNLOAD_DELAY )
                        {
                            LoadOneCar(consist);
                        }
                        break;

                    case YardConsistState.Unloading:
                        if( (curTime - consist.LastUpdateTime) >= LOAD_UNLOAD_DELAY )
                        {
                            UnloadOneCar(consist);
                        }
                        break;

                    default:
                        break;
                }
            }

            // handle creation of new outgoing jobs
            TryToFillRequest();
        }

        private void BackgroundUpdate()
        {

        }

        #endregion
        //============================================================================================
        #region Job Creation

        private void TryToFillRequest()
        {
            List<YardControlConsist> loadCandidates = new List<YardControlConsist>();

            bool multipleRequests = outputsDemand.Length > 1;

            foreach( ProductRequestCollection prodCollection in outputsDemand )
            {
                loadCandidates.Clear();

                YardTrackInfo loadTrack = loadingTracks.FirstOrDefault(track =>
                    (track.LoadingClass == prodCollection.Resource) && !track.Claimed);

                if( loadTrack == null ) continue; // no available track

                loadCandidates.AddRange(emptyConsists.Where(cars => cars.CanHoldResource(prodCollection.Resource)));

                // if no consists available, try next product
                // TODO: pull cars from other stations
                if( loadCandidates.Count < 1 ) continue;

                // dang this is a knapsack problem isn't it
                ProductRequest toFill = prodCollection.Requests.First();
                YardControlConsist[] selectedConsists = FindBestConsistCombo(loadCandidates, toFill.CarCount, multipleRequests);

                Job shuntJob = JobGenerator.CreateShuntingPickupJob(AttachedStation, selectedConsists, loadTrack.Track);

                // setup each cut of cars for loading
                int nCars = 0;
                foreach( var cut in selectedConsists )
                {
                    emptyConsists.Remove(cut);
                    cut.LoadResource = toFill.Resource;
                    cut.LoadDestination = toFill.DestYard;
                    nCars += cut.CarCount;
                }

                ShipmentOrganizer.OnShipmentCreated(toFill.DestYard, toFill.Resource, nCars);

                // TODO: handle job completion
                return;
            }
        }

        private static YardControlConsist[] FindBestConsistCombo( IList<YardControlConsist> pool, int desiredLength, bool limitSingle )
        {
            YardControlConsist a = null, b = null;

            // only option
            if( pool.Count == 1 ) return new[] { pool[0] };

            foreach( var consist in pool )
            {
                if( consist.CarCount >= desiredLength ) return new[] { consist };

                if( (a == null) || (consist.CarCount > a.CarCount) ) a = consist;

                if( !limitSingle && (consist != a) && ((b == null) || (consist.CarCount < b.CarCount)) ) b = consist;
            }

            // longest consist < desiredLength in a
            // shortest consist in b

            if( limitSingle ) return new[] { a };
            else return new[] { a, b };
        }

        private void AssignShipmentToConsist( ProductRequest request, YardControlConsist consist )
        {
            consist.LoadResource = request.Resource;
            consist.LoadDestination = request.DestYard;

            request.CarCount -= consist.CarCount;
            ShipmentOrganizer.OnShipmentCreated(request.DestYard, request.Resource, consist.CarCount);
        }

        private void OnConsistStoreJobEnded( YardControlConsist consist, Job job )
        {
            // forget original consist
            activeConsists.Remove(consist);

            // calculate the new consist split
            List<YardControlConsist> newConsists = new List<YardControlConsist>();
            foreach( TrainCar car in consist )
            {
                if( !(newConsists.Find(c => c.Track == car.logicCar.CurrentTrack) is YardControlConsist subConsist) )
                {
                    subConsist = new YardControlConsist(car.logicCar.CurrentTrack, new[] { car }, YardConsistState.Empty);
                    emptyConsists.AddLast(subConsist);
                }
                subConsist.Cars.Add(car);
            }
        }

        #endregion
        //============================================================================================
        #region Loading/Unloading

        private static bool CarIsStationary( TrainCar car ) => Math.Abs(car.GetForwardSpeed()) < CAR_STOPPED_EPSILON;
        
        private void LoadOneCar( YardControlConsist consist )
        {
            TrainCar car = consist.FirstOrDefault(c => c.LoadedCargoAmount < 1f);
            if( car == null )
            {
                consist.State = YardConsistState.WaitingForTransport;
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
            ResourceClass storedClass = AttachedIndustry.StoreInputCargo(toUnload, 1f);

            ShipmentOrganizer.OnCarUnloaded(StationId, storedClass);

#if DEBUG
            DVIndustry.ModEntry.Logger.Log($"{StationId} - Unloaded {toUnload} from {car.ID}");
#endif
        }

        #endregion

        #region ControllerBase interface

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

        #endregion
    }
}
