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
        public static Job CreateTransportJob( YardController source, YardController dest, YardControlConsist consist, Track destTrack )
        {
            GameObject jobChainGO = new GameObject($"ChainJob[DVI_Transport]: {source.StationId} - {dest.StationId}");
            jobChainGO.transform.SetParent(source.AttachedStation.transform);
            var chainController = new JobChainController(jobChainGO);

            var chainData = new StationsChainData(source.StationId, dest.StationId);

            // calculate payment
            float estimatedMove = JobPaymentCalculator.GetDistanceBetweenStations(source.AttachedStation, dest.AttachedStation);

            // at this point, consist should be loaded
            PaymentCalculationData paymentData = GetLoadedPaymentData(consist.TrainCars);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, estimatedMove, paymentData);

            // bonus time
            float bonusTime = JobPaymentCalculator.CalculateHaulBonusTimeLimit(estimatedMove);

            // licenses
            List<CargoType> distinctCargo = paymentData.cargoData.Keys.ToList();
            JobLicenses licenses = GetRequiredLicensesForJob(distinctCargo, consist.CarCount, JobType.Transport);

            // create that job
            StaticTransportJobDefinition transJob = jobChainGO.AddComponent<StaticTransportJobDefinition>();
            transJob.PopulateBaseJobDefinition(source.AttachedStation.logicStation, bonusTime, payment, chainData, licenses);

            transJob.startingTrack = consist.Track;
            transJob.trainCarsToTransport = consist.LogicCars.ToList();
            transJob.transportedCargoPerCar = consist.TrainCars.Select(tc => tc.LoadedCargo).ToList();
            transJob.cargoAmountPerCar = Enumerable.Repeat(1f, consist.CarCount).ToList();
            transJob.forceCorrectCargoStateOnCars = true; // shouldn't affect anything
            transJob.destinationTrack = destTrack;

            // add to chain
            chainController.AddJobDefinitionToChain(transJob);
            chainController.FinalizeSetupAndGenerateFirstJob();

            DVIndustry.ModEntry.Logger.Log($"Generated shunting job to store train at {source.StationId}");
            Job chainJob = chainController.currentJobInChain;

            // handle job lifecycle
            chainJob.JobTaken += (job, takenViaLoadGame) =>
            {
                source.RemoveConsist(consist);
            };
            chainJob.JobCompleted += job =>
            {
                dest.AddConsist(consist);
                consist.BeginUnloading();
            };
            chainJob.JobAbandoned += job =>
            {
                source.AddConsist(consist);
            };

            return chainJob;
        }

        // a "Logistic Job" as described here involves doing one of three things:
        //     1. haul an empty consist to a loading track in a distant station (to be loaded)
        //     2. shunt an empty consist to a loading track in the local station (to be loaded)
        //     3. shunt a loaded consist to a storage track in the local station (to await shipment when a loading track becomes available at its destination)
        public static Job CreateLogisticJob( YardController source, YardController dest, YardControlConsist consist, Track destTrack )
        {
            JobType jobType = source != dest
                ? JobType.EmptyHaul
                : consist.State == YardConsistState.Empty
                    ? JobType.ShuntingLoad
                    : JobType.ShuntingUnload;

            GameObject jobChainGO = new GameObject($"ChainJob[DVI_Logistic]: {source.StationId} - {dest.StationId}");
            jobChainGO.transform.SetParent(source.AttachedStation.transform);
            var chainController = new JobChainController(jobChainGO);

            var chainData = new StationsChainData(source.StationId, dest.StationId);

            float estimatedMove = EstimateMoveDistance(consist.Track, destTrack);

            // calculate payment
            PaymentCalculationData paymentData = consist.State == YardConsistState.Loaded
                ? GetLoadedPaymentData(consist.TrainCars)
                : GetEmptyPaymentData(consist.TrainCars.Select(tc => tc.carType));
            float payment = JobPaymentCalculator.CalculateJobPayment(jobType, estimatedMove, paymentData);

            // bonus time
            float bonusTime = jobType == JobType.EmptyHaul
                ? JobPaymentCalculator.CalculateHaulBonusTimeLimit(estimatedMove)
                : CalculateShuntingBonusTime(estimatedMove, 1);

            // license
            JobLicenses licenses = GetRequiredLicensesForJob(paymentData.cargoData.Keys.ToList(), consist.CarCount, jobType);

            if( jobType == JobType.EmptyHaul )
            {
                StaticEmptyHaulJobDefinition logiJob = jobChainGO.AddComponent<StaticEmptyHaulJobDefinition>();
                logiJob.PopulateBaseJobDefinition(source.AttachedStation.logicStation, bonusTime, payment, chainData, licenses);

                logiJob.startingTrack = consist.Track;
                logiJob.trainCarsToTransport = consist.LogicCars.ToList();
                logiJob.destinationTrack = destTrack;

                chainController.AddJobDefinitionToChain(logiJob);
            }
            else
            {
                ShuntingJobDefinition shuntJob = jobChainGO.AddComponent<ShuntingJobDefinition>();
                shuntJob.PopulateBaseJobDefinition(dest.AttachedStation.logicStation, bonusTime, payment, chainData, licenses);

                shuntJob.Consist = consist.LogicCars.ToList();
                shuntJob.CarriedCargo = consist.TrainCars.Select(tc => tc.LoadedCargo).ToList();
                shuntJob.PickupTrack = consist.Track;
                shuntJob.DropoffTrack = destTrack;
                shuntJob.LoadingJob = jobType == JobType.ShuntingLoad;

                chainController.AddJobDefinitionToChain(shuntJob);
            }

            chainController.FinalizeSetupAndGenerateFirstJob();

            DVIndustry.ModEntry.Logger.Log($"Generated shunting job to store train at {dest.StationId}");
            Job chainJob = chainController.currentJobInChain;

            // handle job lifecycle
            chainJob.JobTaken += (job, takenViaGameLoad) =>
            {
                source.RemoveConsist(consist);
            };
            chainJob.JobCompleted += job =>
            {
                dest.AddConsist(consist);

                if( consist.State == YardConsistState.Empty )
                {
                    dest.StartLoadingConsist(consist);
                }
            };
            chainJob.JobAbandoned += job =>
            {
                source.AddConsist(consist);
            };

            return chainJob;
        }

        /** Get a rough estimate of the movement distance between tracks in the same yard */
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

        /** Haul value for shunting/logistic jobs */
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

        /** Get the payment data for a loaded consist (reimplements ExtractPaymentCalculationData) */
        private static PaymentCalculationData GetLoadedPaymentData( IEnumerable<TrainCar> cars )
        {
            Dictionary<TrainCarType, int> carTypes = new Dictionary<TrainCarType, int>();
            Dictionary<CargoType, int> cargos = new Dictionary<CargoType, int>();

            foreach( TrainCar car in cars )
            {
                // add car type
                if( !carTypes.TryGetValue(car.carType, out int nType) )
                {
                    nType = 0;
                }

                carTypes[car.carType] = nType + 1;

                // add cargo
                if( !cargos.TryGetValue(car.LoadedCargo, out int nCargo) )
                {
                    nCargo = 0;
                }

                cargos[car.LoadedCargo] = nCargo + 1;
            }

            return new PaymentCalculationData(carTypes, cargos);
        }

        private static float CalculateShuntingBonusTime( float moveDistance, int numberOfTracks )
        {
            // 12 min per track originally
            // estimate: (distance / 5 kph) * 1.5 ^ numberOfTracks
            return moveDistance * Mathf.Pow(1.5f, numberOfTracks - 2);
        }

        private static JobLicenses GetRequiredLicensesForJob( List<CargoType> cargoTypes, int carCount, JobType jobType )
        {
            JobLicenses result = LicenseManager.GetRequiredLicensesForJobType(jobType);
            result |= LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(carCount);
            
            if( cargoTypes != null )
            {
                result |= LicenseManager.GetRequiredLicensesForCargoTypes(cargoTypes);
            }

            return result;
        }
    }
}
