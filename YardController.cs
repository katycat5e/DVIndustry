using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DV.Logic.Job;
using DVIndustry.Jobs;
using HarmonyLib;

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

        private readonly System.Random rand = new System.Random();
        private bool playerInRange = false;

        private IndustryController AttachedIndustry = null;

        private FancyLinkedList<YardControlConsist> consists = null;
        private IEnumerable<YardControlConsist> LoadingConsists =>
            (consists != null) ? consists.Where(c => c.State == YardConsistState.Loading) : Enumerable.Empty<YardControlConsist>();
        private IEnumerable<YardControlConsist> UnloadingConsists =>
            (consists != null) ? consists.Where(c => c.State == YardConsistState.Unloading) : Enumerable.Empty<YardControlConsist>();

        private IEnumerable<YardControlConsist> LoadedConsists =>
            (consists != null) ? consists.Where(c => c.State == YardConsistState.Loaded) : Enumerable.Empty<YardControlConsist>();
        private IEnumerable<YardControlConsist> EmptyConsists =>
            (consists != null) ? consists.Where(c => c.State == YardConsistState.Empty) : Enumerable.Empty<YardControlConsist>();

        private YardTrackInfo[] loadingTracks;
        private Track[] stagingTracks;
        private static readonly Dictionary<Track, YardTrackInfo> loadTrackMap = new Dictionary<Track, YardTrackInfo>();
        private List<Track> AllTracks => stagingTracks.Union(loadingTracks.Select(lt => lt.Track)).ToList();
        public static bool IsOnLoadingTrack( YardControlConsist consist ) => consist.Track != null && loadTrackMap.ContainsKey(consist.Track);


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
        #region Consist Handling

        private void InstantiateConsists()
        {
            if (consists == null)
            {
                return;
            }

            // TODO: should this use a coroutine for better performance?
            foreach (var consist in consists)
            {
                consist.Instantiate();
            }
        }

        private void VirtualizeConsists()
        {
            if (consists == null)
            {
                return;
            }

            foreach (var consist in consists)
            {
                consist.Virtualize();
            }
        }

        public void AddConsist( YardControlConsist consist )
        {
            if( consists == null || consists.Contains(consist) ) return;
            consists.AddItem(consist);
        }

        public void RemoveConsist( YardControlConsist consist )
        {
            if( consists == null ) return;
            consists.Remove(consist);
        }

        public void StartLoadingConsist( YardControlConsist consist )
        {
            var requests = ShipmentOrganizer.GetRequests(consist.CargoClass);
            if( requests.Count() == 0 )
            {
                throw new Exception($"{StationId} - found no requests for ResourceClass {consist.CargoClass.ID}");
            }

            consist.BeginLoading(requests.ElementAt(0).DestYard);
        }

        #endregion
        //============================================================================================
        #region Track Handling

        public Track GetAvailableReceivingTrack( float minLength, ResourceClass toUnload = null )
        {
            IEnumerable<YardTrackInfo> pool =
                loadingTracks.Where(lt => (lt.AvailableSpace >= minLength) && (toUnload == null || lt.LoadingClass.ContainsClass(toUnload)));

            if( playerInRange )
            {
                // cars are instantiated
                List<Track> candidates = pool.Where(yt => !yt.Claimed).Select(yt => yt.Track).ToList();
                return candidates.ChooseOne(rand);
            }
            else
            {
                // TODO handle reservations of virtual cars
                return null;
            }
        }

        public Track GetAvailableStagingTrack( float minLength )
        {
            if( playerInRange )
            {
                // cars are instantiated, use occupied length
                return YardUtil.FindBestFitTrack(stagingTracks, minLength);
            }
            else
            {
                // TODO handle reservations of virtual cars
                return null;
            }
        }

        #endregion
        //============================================================================================
        #region Monobehaviour Update Handling

        void Update()
        {
            // wait for loading to finish
            if( !IndustrySaveDataManager.IsLoadCompleted ) return;
            
            if( playerInRange )
            {
                // normal in-range processing
                LoadAndStoreCargo();
            }
            else
            {
                // not in range
                LoadAndStoreCargo();
            }
        }

        private void LoadAndStoreCargo()
        {
            float curTime = Time.time;

            // handle consists on the loading tracks
            foreach (YardControlConsist consist in consists)
            {
                if ((curTime - consist.LastUpdateTime) >= LOAD_UNLOAD_DELAY)
                {
                    if( consist.State == YardConsistState.Loading )
                    {
                        var (cargoLoaded, amountLoaded) = consist.LoadNextCar();
                        if( amountLoaded > 0 )
                        {
                            var resource = AttachedIndustry.TakeOutputCargo(consist.CargoClass, amountLoaded);
                            if( resource != null )
                            {
                                ShipmentOrganizer.OnCarLoaded(StationId, consist.Destination, resource, amountLoaded);
                            }
#if DEBUG
                            DVIndustry.ModEntry.Logger.Log($"{StationId} - Loaded {cargoLoaded} ({amountLoaded})");
#endif
                        }
                        else if( amountLoaded == 0 )
                        {
                            TryCreateJobLoadedConsist(consist);
                        }
                    }
                    else if( consist.State == YardConsistState.Unloading )
                    {
                        var (cargoUnloaded, amountUnloaded) = consist.UnloadNextCar();
                        if( amountUnloaded > 0 )
                        {
                            var resource = AttachedIndustry.StoreInputCargo(consist.CargoClass, amountUnloaded);
                            if( resource != null )
                            {
                                ShipmentOrganizer.OnCarUnloaded(consist.Destination, resource, amountUnloaded);
                            }
#if DEBUG
                            DVIndustry.ModEntry.Logger.Log($"{StationId} - Unloaded {cargoUnloaded} ({amountUnloaded})");
#endif
                        }
                        else if( amountUnloaded == 0 )
                        {
                            TryCreateJobEmptyConsist(consist);
                        }
                    }
                }
            }
        }

        private void BackgroundUpdate()
        {

        }

        #endregion
        //============================================================================================
        #region Job Creation

        private Coroutine hydrationCoro;
        private Coroutine generationCoro;

        public void HydrateOrGenerateCars()
        {
            if (hydrationCoro != null || generationCoro != null)
            {
                return;
            }

            if (consists != null)
            {
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - hydrating {consists.Count} consists...");
#endif
                hydrationCoro = StartCoroutine(HydrateConsistsCoro());
            }
            else
            {
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - no consists found! generating new consists...");
#endif
                consists = new FancyLinkedList<YardControlConsist>();
                generationCoro = StartCoroutine(GenerateConsistsCoro());
            }
        }

        public void CancelCarHydrationOrGeneration()
        {
            if (hydrationCoro != null)
            {
                StopCoroutine(hydrationCoro);
                hydrationCoro = null;
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - train car hydration stopped ({AttachedStation.logicStation.availableJobs} jobs generated)");
#endif
            }

            if (generationCoro != null)
            {
                StopCoroutine(generationCoro);
                generationCoro = null;
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - train car generation stopped ({AttachedStation.logicStation.availableJobs} jobs generated)");
#endif
            }
        }

        private IEnumerator HydrateConsistsCoro()
        {
            // preferentially assign loaded consists to receiving tracks
            var loadedSorted = LoadedConsists.ToList();
            yield return null; // next frame
            loadedSorted.Sort(YardControlConsist.CompareByLicense);
            yield return null; // next frame
            foreach ( YardControlConsist consist in loadedSorted )
            {
                TryCreateJobLoadedConsist(consist);
                yield return null; // next frame
            }

            // secondarily assign empty consists to receiving tracks
            var emptySorted = EmptyConsists.ToList();
            yield return null; // next frame
            emptySorted.Sort(YardControlConsist.CompareByLicense);
            yield return null; // next frame
            foreach( YardControlConsist consist in emptySorted )
            {
                TryCreateJobEmptyConsist(consist);
                yield return null; // next frame
            }

            hydrationCoro = null;
            yield break;
        }

        private IEnumerator GenerateConsistsCoro()
        {
            var industry = IndustryController.At(StationId);
            var resources = industry.OutputResources.Select(id => ResourceClass.Parse(id)).Where(r => r != null).ToList();
            yield return null; // next frame

            // Give licensed cargos priority
            resources.Sort((x, y) =>
            {
                int result = 0;
                if( LicenseManager.GetMissingLicensesForJob(LicenseManager.GetRequiredLicensesForCargoTypes(x.Cargos.ToList())) == 0 )
                {
                    result -= 1;
                }
                if( LicenseManager.GetMissingLicensesForJob(LicenseManager.GetRequiredLicensesForCargoTypes(y.Cargos.ToList())) == 0 )
                {
                    result += 1;
                }
                return result;
            });
            yield return null; // next frame

            foreach( var resource in resources )
            {
                int carCount = rand.Next(LicenseManager.STARTING_TRAIN_LENGTH_MIN, LicenseManager.STARTING_TRAIN_LENGTH_MAX);
                List<TrainCarType> carTypesToSpawn = new List<TrainCarType>(carCount)
                    .Select(tct => resource.CompatibleCars.ElementAt(rand.Next(0, resource.CompatibleCars.Count)))
                    .ToList();
                yield return null; // next frame

                var yto = YardTracksOrganizer.Instance;
                float consistLength = yto.GetTotalCarTypesLength(carTypesToSpawn);
                var possibleTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(AllTracks, consistLength + yto.GetSeparationLengthBetweenCars(carCount));
                var trackToSpawnOn = possibleTracks.ElementAt(rand.Next(0, possibleTracks.Count));
                var railTrackToSpawnOn = CarsSaveManager.Instance.OrderedRailtracks.First(rt => rt.logicTrack == trackToSpawnOn);
                yield return null; // next frame

                var trainCars = CarSpawner.SpawnCarTypesOnTrack(carTypesToSpawn, railTrackToSpawnOn, true);
                yield return null; // next frame

                foreach( var tc in trainCars )
                {
                    tc.logicCar.LoadCargo(tc.logicCar.capacity, resource.Cargos.ElementAt(rand.Next(0, resource.Cargos.Length)));
                }
                yield return null; // next frame

                var requests = ShipmentOrganizer.GetRequests(resource);
                if( requests.Count() == 0 )
                {
                    throw new Exception($"{StationId} - found no requests for ResourceClass {resource.ID}");
                }
                var consist = new YardControlConsist(trainCars, YardConsistState.Loaded, resource, requests.ElementAt(0).DestYard);
                AddConsist(consist);
                yield return null; // next frame

                TryCreateJobLoadedConsist(consist);
                yield return null; // next frame
            }

            yield break;
        }

        private Job TryCreateJobLoadedConsist( YardControlConsist consist )
        {
            if( !playerInRange ) return null;

            YardController destYard = At(consist.Destination);
            Track loadingTrack = destYard.GetAvailableReceivingTrack(consist.Length, consist.CargoClass);
            if( loadingTrack != null )
            {
                // found a destination track, create haul job
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - found loading track for loaded consist in {consist.Destination}");
#endif
                return JobGenerator.CreateTransportJob(this, destYard, consist, loadingTrack);
            }

            if( !IsOnLoadingTrack(consist) ) return null; // nothing to do with this one

            // can't transport now, shunt to siding
            Track storageTrack = GetAvailableStagingTrack(consist.Length);
            if( storageTrack != null )
            {
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - no loading track found for loaded consist. shunting to storage track");
#endif
                return JobGenerator.CreateLogisticJob(this, this, consist, storageTrack);
            }

            // no track available ;_;
            DVIndustry.ModEntry.Logger.Log($"{StationId} - loaded consist is stuck on track {consist.Track}");
            return null;
        }

        private Job TryCreateJobEmptyConsist( YardControlConsist consist )
        {
            if( !playerInRange ) return null;

            var offerings = ShipmentOrganizer.GetOfferings(consist);

            // preferentially use the consist where it already is
            foreach( var offering in offerings.Where(cap => cap.SourceYard == StationId) )
            {
                if( offering.Amount < consist.Capacity ) continue;

                // can it be loaded on the current track?
                if( IsOnLoadingTrack(consist) && loadTrackMap.TryGetValue(consist.Track, out var yardTrackInfo) && yardTrackInfo.LoadingClass.ContainsClass(offering.Resource))
                {
#if DEBUG
                    DVIndustry.ModEntry.Logger.Log($"{StationId} - skipping logistic job: loading consist on current track");
#endif
                    consist.PlanLogistics(offering.Resource, offering.SourceYard);
                    ShipmentOrganizer.OnLogisticsPlanned(offering.SourceYard, offering.Resource, consist.Capacity);
                    StartLoadingConsist(consist);
                    return null;
                }

                Track loadingTrack = GetAvailableReceivingTrack(consist.Length, consist.CargoClass);
                if( loadingTrack != null )
                {
                    // found a destination track, lock in station/resource & create shunting job
#if DEBUG
                    DVIndustry.ModEntry.Logger.Log($"{StationId} - found loading track for empty consist in {offering.SourceYard}");
#endif
                    if ( consist.Destination == null )
                    {
                        consist.PlanLogistics(offering.Resource, offering.SourceYard);
                        ShipmentOrganizer.OnLogisticsPlanned(offering.SourceYard, offering.Resource, consist.Capacity);
                    }
                    return JobGenerator.CreateLogisticJob(this, this, consist, loadingTrack);
                }
            }

            // secondarily haul the consist to another station
            foreach( var offering in offerings.Where(cap => cap.SourceYard != StationId) )
            {
                if( offering.Amount < consist.Capacity ) continue;

                YardController destYard = At(offering.SourceYard);
                Track loadingTrack = destYard.GetAvailableReceivingTrack(consist.Length, consist.CargoClass);
                if( loadingTrack != null )
                {
                    // found a destination track, lock in station/resource & create empty haul job
#if DEBUG
                    DVIndustry.ModEntry.Logger.Log($"{StationId} - found loading track for empty consist in {offering.SourceYard}");
#endif
                    if ( consist.Destination == null )
                    {
                        consist.PlanLogistics(offering.Resource, offering.SourceYard);
                        ShipmentOrganizer.OnLogisticsPlanned(offering.SourceYard, offering.Resource, consist.Capacity);
                    }
                    return JobGenerator.CreateLogisticJob(this, destYard, consist, loadingTrack);
                }
            }

            if (!IsOnLoadingTrack(consist)) return null; // nothing to do with this one

            // can't load now, shunt to siding
            Track storageTrack = GetAvailableStagingTrack(consist.Length);
            if( storageTrack != null )
            {
#if DEBUG
                DVIndustry.ModEntry.Logger.Log($"{StationId} - no loading track found for empty consist. shunting to storage track");
#endif
                return JobGenerator.CreateLogisticJob(this, this, consist, storageTrack);
            }

            // no track available ;_;
            DVIndustry.ModEntry.Logger.Log($"{StationId} - empty consist is stuck on track {consist.Track}");
            return null;
        }

        #endregion
        //============================================================================================
        #region Range Handling

        public void PlayerHasEnteredRange()
        {
#if DEBUG
            DVIndustry.ModEntry.Logger.Log($"{StationId} - player entering station range");
#endif
            playerInRange = true;
            InstantiateConsists();
            HydrateOrGenerateCars();
        }

        public void PlayerHasExitedRange()
        {
#if DEBUG
            DVIndustry.ModEntry.Logger.Log($"{StationId} - player exiting station range");
#endif
            playerInRange = false;
            CancelCarHydrationOrGeneration();
            VirtualizeConsists();
        }

        #endregion
        //============================================================================================
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

    #region Method Patches

    [HarmonyPatch(typeof(StationProceduralJobsController), "TryToGenerateJobs")]
    static class SignalPlayerEnteredRange
    {
        static bool Prefix(StationProceduralJobsController __instance)
        {
            if (YardController.At(__instance.stationController.stationInfo.YardID) is YardController yard)
            {
                yard.PlayerHasEnteredRange();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(StationController), "ExpireAllAvailableJobsInStation")]
    static class SignalPlayerExitedRange
    {
        static void Prefix(StationController __instance)
        {
            if (YardController.At(__instance.stationInfo.YardID) is YardController yard)
            {
                yard.PlayerHasExitedRange();
            }
        }
    }

    #endregion
}
