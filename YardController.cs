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

        private bool playerInRange = false;

        private IndustryController AttachedIndustry = null;

        private readonly FancyLinkedList<YardControlConsist> consists = null;
        private FancyLinkedList<YardControlConsist> LoadingConsists =>
            new FancyLinkedList<YardControlConsist>( consists != null ? consists.Where(c => c.State == YardConsistState.Loading) : new YardControlConsist[] { } );
        private FancyLinkedList<YardControlConsist> UnloadingConsists =>
            new FancyLinkedList<YardControlConsist>(consists != null ? consists.Where(c => c.State == YardConsistState.Unloading) : new YardControlConsist[] { });
        private FancyLinkedList<YardControlConsist> LoadedConsists =>
            new FancyLinkedList<YardControlConsist>(consists != null ? consists.Where(c => c.State == YardConsistState.Loaded) : new YardControlConsist[] { });
        private FancyLinkedList<YardControlConsist> EmptyConsists =>
            new FancyLinkedList<YardControlConsist>(consists != null ? consists.Where(c => c.State == YardConsistState.Empty) : new YardControlConsist[] { });

        private YardTrackInfo[] loadingTracks;
        private Track[] stagingTracks;
        private static readonly Dictionary<Track, YardTrackInfo> loadTrackMap = new Dictionary<Track, YardTrackInfo>();
        public static bool IsOnLoadingTrack( YardControlConsist consist ) => consist.Track != null && loadTrackMap.ContainsKey(consist.Track);

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
            foreach (YardControlConsist consist in LoadingConsists)
            {
                if ((curTime - consist.LastUpdateTime) >= LOAD_UNLOAD_DELAY)
                {
                    var (cargoLoaded, amountLoaded) = consist.LoadNextCar();
                    IndustryController.At(StationId).TakeOutputCargo(cargoLoaded, amountLoaded);
#if DEBUG
                    DVIndustry.ModEntry.Logger.Log($"{StationId} - Loaded {cargoLoaded} ({amountLoaded})");
#endif
                }
            }
            foreach (YardControlConsist consist in UnloadingConsists)
            {
                if ((curTime - consist.LastUpdateTime) >= LOAD_UNLOAD_DELAY)
                {
                    var (cargoUnloaded, amountUnloaded) = consist.UnloadNextCar();
                    IndustryController.At(StationId).StoreInputCargo(cargoUnloaded, amountUnloaded);
#if DEBUG
                    DVIndustry.ModEntry.Logger.Log($"{StationId} - Unloaded {cargoUnloaded} ({amountUnloaded})");
#endif
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
            throw new NotImplementedException();
            // yield return null;
        }

        private IEnumerator GenerateConsistsCoro()
        {
            throw new NotImplementedException();
            // yield return null;
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
