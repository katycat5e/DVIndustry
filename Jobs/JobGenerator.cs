using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;
using UnityEngine;

namespace DVIndustry.Jobs
{
    public static class JobGenerator
    {
        public static Job CreateShuntingStoreJob( StationController station, YardControlConsist consist, Track destTrack )
        {
            GameObject jobChainGO = new GameObject($"ChainJob[DVI_Store]: {station.logicStation.ID}");
            jobChainGO.transform.SetParent(station.transform);
            var chainController = new JobChainController(jobChainGO);

            var chainData = new StationsChainData(station.stationInfo.YardID, station.stationInfo.YardID);

            float estimatedMove = EstimateMoveDistance(consist.Track, destTrack);

            // calculate payment
            PaymentCalculationData emptyPaymentData = GetEmptyPaymentData(consist.Cars.Select(c => c.carType));
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, estimatedMove, emptyPaymentData);

            // bonus time
            float bonusTime = CalculateShuntingBonusTime(estimatedMove, 1);

            ShuntingStoreJobDefinition shuntJob = jobChainGO.AddComponent<ShuntingStoreJobDefinition>();
            shuntJob.PopulateBaseJobDefinition(station.logicStation, bonusTime, payment, chainData, JobLicenses.Shunting);

            shuntJob.Consist = consist.LogicCars;
            shuntJob.PickupTrack = consist.Track;
            shuntJob.Dropoffs = new List<CarsPerTrack>() { new CarsPerTrack(destTrack, shuntJob.Consist) };

            chainController.AddJobDefinitionToChain(shuntJob);
            chainController.FinalizeSetupAndGenerateFirstJob();

            consist.State = YardConsistState.WaitingForPlayerShunt;
            consist.CurrentJob = chainController.currentJobInChain;

            DVIndustry.ModEntry.Logger.Log($"Generated shunting job to store train at {station.stationInfo.YardID}");
            return chainController.currentJobInChain;
        }

        public static Job CreateShuntingPickupJob( StationController station, IList<YardControlConsist> consists, Track destTrack )
        {
            GameObject jobChainGO = new GameObject($"ChainJob[DVI_Store]: {station.logicStation.ID}");
            jobChainGO.transform.SetParent(station.transform);
            var chainController = new JobChainController(jobChainGO);

            var chainData = new StationsChainData(station.stationInfo.YardID, station.stationInfo.YardID);

            float estimatedMove = consists.Sum(cut => EstimateMoveDistance(cut.Track, destTrack));
            List<Car> allCars = consists.SelectMany(cut => cut.LogicCars).ToList();

            // calculate payment
            PaymentCalculationData emptyPaymentData = GetEmptyPaymentData(allCars.Select(c => c.carType));
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, estimatedMove, emptyPaymentData);

            // bonus time
            float bonusTime = CalculateShuntingBonusTime(estimatedMove, consists.Count);

            ShuntingPickupJobDefinition shuntJob = jobChainGO.AddComponent<ShuntingPickupJobDefinition>();
            shuntJob.PopulateBaseJobDefinition(station.logicStation, bonusTime, payment, chainData, JobLicenses.Shunting);

            shuntJob.Consist = allCars;
            shuntJob.DropoffTrack = destTrack;
            shuntJob.Pickups = consists.Select(cut => new CarsPerTrack(cut.Track, cut.LogicCars)).ToList();

            chainController.AddJobDefinitionToChain(shuntJob);
            chainController.FinalizeSetupAndGenerateFirstJob();

            foreach( YardControlConsist cut in consists )
            {
                cut.State = YardConsistState.WaitingForPlayerShunt;
                cut.CurrentJob = chainController.currentJobInChain;
            }

            DVIndustry.ModEntry.Logger.Log($"Generated shunting job to assemble train at {station.stationInfo.YardID}");
            return chainController.currentJobInChain;
        }

        private static float EstimateMoveDistance( Track a, Track b )
        {
            RailTrack aRT = LogicController.Instance.LogicToRailTrack[a];
            RailTrack bRT = LogicController.Instance.LogicToRailTrack[b];

            DV.PointSet.EquiPointSet.Point[] aPts = aRT.GetPointSet().points;
            Vector3d aCenter = aPts[aPts.Length / 2].position;

            DV.PointSet.EquiPointSet.Point[] bPts = bRT.GetPointSet().points;
            Vector3d bCenter = bPts[bPts.Length / 2].position;

            double crowDist = (aCenter - bCenter).magnitude;
            double moveDistance = crowDist + (a.length + b.length) / 2;
            return (float)moveDistance;
        }

        private static PaymentCalculationData GetEmptyPaymentData( IEnumerable<TrainCarType> carTypes )
        {
            var carTypeCount = new Dictionary<TrainCarType, int>();
            int totalCars = 0;

            foreach( TrainCarType type in carTypes )
            {
                if( carTypeCount.TryGetValue(type, out int curCount) )
                {
                    carTypeCount[type] = curCount + 1;
                }
                else carTypeCount[type] = 1;

                totalCars += 1;
            }

            var cargoTypeDict = new Dictionary<CargoType, int>(0);

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }

        private static float CalculateShuntingBonusTime( float moveDistance, int numberOfTracks )
        {
            // 12 min per track originally
            // estimate: (distance / 5 kph) * 1.5 ^ numberOfTracks
            return moveDistance * Mathf.Pow(1.5f, numberOfTracks - 2);
        }
    }
}
