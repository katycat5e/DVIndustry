using System;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;

namespace DVIndustry.Jobs
{
    public class ShuntingStoreJobDefinition : StaticJobDefinition
    {
        public List<Car> Consist;
        public Track PickupTrack;
        public List<CarsPerTrack> Dropoffs;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            string[] guidsFromCars = GetGuidsFromCars(Consist);
            if( guidsFromCars == null )
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            };

            CarGuidsPerTrackId[] dropoffIds = Dropoffs
                .Select(cpt => new CarGuidsPerTrackId(cpt.track.ID.FullID, GetGuidsFromCars(cpt.cars)))
                .ToArray();

            return new ShuntingStoreDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID,
                chainData.chainOriginYardId, chainData.chainDestinationYardId,
                (int)requiredLicenses, guidsFromCars, PickupTrack.ID.FullID, dropoffIds);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            return Dropoffs
                .Select(cpt => new TrackReservation(
                    cpt.track,
                    YardUtil.GetConsistLength(cpt.cars)))
                .ToList();
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (Consist == null) || (Consist.Count == 0) ||
                (PickupTrack == null) || (Dropoffs == null) || (Dropoffs.Count == 0) )
            {
                Consist = null;
                PickupTrack = null;
                Dropoffs = null;
                return;
            }

            List<Task> moveTasks = Dropoffs
                .Select(cpt => new TransportTask(cpt.cars, cpt.track, PickupTrack))
                .Cast<Task>()
                .ToList();

            var mainTask = new ParallelTasks(moveTasks);
            job = new Job(mainTask, JobType.ShuntingUnload, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);
        }
    }

    public class ShuntingStoreDefinitionData : JobDefinitionDataBase
    {
        public string[] TrainCarGuids;
        public string StartTrack;
        public CarGuidsPerTrackId[] DestTracks;

        public ShuntingStoreDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, CarGuidsPerTrackId[] destTracks ) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            StartTrack = startTrackId;
            DestTracks = destTracks;
        }
    }
}
