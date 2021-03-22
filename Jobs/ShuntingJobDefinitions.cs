using System;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;

namespace DVIndustry.Jobs
{
    public class ShuntingJobDefinition : StaticJobDefinition
    {
        public bool LoadingJob;
        public List<Car> Consist;
        public List<CargoType> CarriedCargo = null;
        public Track PickupTrack;
        public Track DropoffTrack;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            string[] guidsFromCars = GetGuidsFromCars(Consist);
            if( guidsFromCars == null )
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            };

            return new ShuntingStoreDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID,
                chainData.chainOriginYardId, chainData.chainDestinationYardId,
                (int)requiredLicenses, guidsFromCars, CarriedCargo.ToArray(),
                PickupTrack.ID.FullID, DropoffTrack.ID.FullID, LoadingJob);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            return new List<TrackReservation>(1) { new TrackReservation(DropoffTrack, YardUtil.GetConsistLength(Consist)) };
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (Consist == null) || (Consist.Count == 0) ||
                (PickupTrack == null) || (DropoffTrack == null) )
            {
                Consist = null;
                PickupTrack = null;
                DropoffTrack = null;
                return;
            }

            JobType subType = LoadingJob ? JobType.ShuntingLoad : JobType.ShuntingUnload;

            var mainTask = new TransportTask(Consist, DropoffTrack, PickupTrack, CarriedCargo);
            job = new Job(mainTask, subType, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);
        }
    }

    public class ShuntingStoreDefinitionData : JobDefinitionDataBase
    {
        public bool LoadingJob;
        public string[] TrainCarGuids;
        public CargoType[] Cargo;
        public string StartTrack;
        public string DestTrack;

        public ShuntingStoreDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, CargoType[] cargo, string startTrackId, string destTrack, bool loadJob ) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            Cargo = cargo;
            StartTrack = startTrackId;
            DestTrack = destTrack;
            LoadingJob = loadJob;
        }
    }

    /*
    public class ShuntingPickupJobDefinition : StaticJobDefinition
    {
        public List<Car> Consist;
        public Track DropoffTrack;
        public List<CarsPerTrack> Pickups;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            string[] guidsFromCars = GetGuidsFromCars(Consist);
            if( guidsFromCars == null )
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            };

            CarGuidsPerTrackId[] pickupIds = Pickups
                .Select(cpt => new CarGuidsPerTrackId(cpt.track.ID.FullID, GetGuidsFromCars(cpt.cars)))
                .ToArray();

            return new ShuntingPickupDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID,
                chainData.chainOriginYardId, chainData.chainDestinationYardId,
                (int)requiredLicenses, guidsFromCars, DropoffTrack.ID.FullID, pickupIds);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            return new List<TrackReservation>(1) { new TrackReservation(DropoffTrack, YardUtil.GetConsistLength(Consist)) };
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (Consist == null) || (Consist.Count == 0) ||
                (DropoffTrack == null) || (Pickups == null) || (Pickups.Count == 0) )
            {
                Consist = null;
                DropoffTrack = null;
                Pickups = null;
                return;
            }

            List<Task> moveTasks = Pickups
                .Select(cpt => new TransportTask(cpt.cars, DropoffTrack, cpt.track))
                .Cast<Task>()
                .ToList();

            var mainTask = new ParallelTasks(moveTasks);
            job = new Job(mainTask, JobType.ShuntingLoad, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);
        }
    }

    public class ShuntingPickupDefinitionData : JobDefinitionDataBase
    {
        public string[] TrainCarGuids;
        public string EndTrack;
        public CarGuidsPerTrackId[] SrcTracks;

        public ShuntingPickupDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, string endTrackId, CarGuidsPerTrackId[] startTracks ) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            EndTrack = endTrackId;
            SrcTracks = startTracks;
        }
    }
    */
}
