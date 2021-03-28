using DV.Logic.Job;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVIndustry
{
    class VirtualTrainCar
    {
        public VirtualTrainCar(TrainCar trainCar)
        {
            type = trainCar.carType;
            playerSpawn = trainCar.playerSpawnedCar;
            position = trainCar.transform.position - WorldMover.currentMove;
            rotation = trainCar.transform.rotation.eulerAngles;
            length = trainCar.InterCouplerDistance;
            exploded = trainCar.useExplodedModel;
            loadedCargo = trainCar.logicCar.CurrentCargoTypeInCar;
            loadedAmount = trainCar.logicCar.LoadedCargoAmount;
            cargoCapacity = trainCar.cargoCapacity;

            var bogie = trainCar.Bogies[0];
            bog1Derailed = bogie.HasDerailed;
            if (bog1Derailed)
            {
                bog1TrackChildInd = null;
                bog1PosOnTrack = null;
            }
            else
            {
                bog1TrackChildInd = Array.IndexOf(CarsSaveManager.Instance.OrderedRailtracks, bogie.track);
                bog1PosOnTrack = bogie.traveller.Span;
            }

            bogie = trainCar.Bogies[1];
            bog2Derailed = bogie.HasDerailed;
            if (bog2Derailed)
            {
                bog2TrackChildInd = null;
                bog2PosOnTrack = null;
            }
            else
            {
                bog2TrackChildInd = Array.IndexOf(CarsSaveManager.Instance.OrderedRailtracks, bogie.track);
                bog2PosOnTrack = bogie.traveller.Span;
            }

            var carStateSave = trainCar.GetComponent<CarStateSave>();
            if (carStateSave != null)
            {
                carState = carStateSave.GetCarStateSaveData();
            }
            else
            {
                carState = null;
            }

            var locoStateSave = trainCar.GetComponent<LocoStateSave>();
            if (locoStateSave)
            {
                locoState = locoStateSave.GetLocoStateSaveData();
            }
            else
            {
                locoState = null;
            }
        }

        public TrainCar Instantiate()
        {
            var carPrefab = CarTypes.GetCarPrefab(type);
            var orderedRailTracks = CarsSaveManager.Instance.OrderedRailtracks;
            var bog1Track = bog1TrackChildInd.HasValue ? orderedRailTracks[bog1TrackChildInd.Value] : null;
            var bog1PositionAlongTrack = bog1PosOnTrack.HasValue ? bog1PosOnTrack.Value : 0.0;
            var bog2Track = bog2TrackChildInd.HasValue ? orderedRailTracks[bog2TrackChildInd.Value] : null;
            var bog2PositionAlongTrack = bog2PosOnTrack.HasValue ? bog2PosOnTrack.Value : 0.0;

            var trainCar = CarSpawner.SpawnLoadedCar(
                carPrefab,
                SingletonBehaviour<IdGenerator>.Instance.GenerateCarID(type),
                Guid.NewGuid().ToString(),
                playerSpawn,
                position,
                Quaternion.Euler(rotation),
                bog1Derailed,
                bog1Track,
                bog1PositionAlongTrack,
                bog2Derailed,
                bog2Track,
                bog2PositionAlongTrack,
                coupledF,
                coupledR);

            if (loadedCargo != CargoType.None)
            {
                trainCar.logicCar.LoadCargo(loadedAmount, loadedCargo, null);
            }

            if (exploded)
            {
                TrainCarExplosion.UpdateTrainCarModelToExploded(trainCar);
            }

            if (carState != null)
            {
                var carStateSave = trainCar.GetComponent<CarStateSave>();
                if (carStateSave != null)
                {
                    carStateSave.SetCarStateSaveData(carState);
                }
            }

            if (locoState != null)
            {
                var locoStateSave = trainCar.GetComponent<LocoStateSave>();
                if (locoStateSave != null)
                {
                    locoStateSave.SetLocoStateSaveData(locoState);
                }
            }

            return trainCar;
        }

        public float LoadCargo(CargoType cargoType)
        {
            loadedCargo = cargoType;
            return loadedAmount = cargoCapacity;
        }

        public (CargoType, float) UnloadCargo()
        {
            var cargoUnloaded = loadedCargo;
            loadedCargo = CargoType.None;
            var amountUnloaded = loadedAmount;
            loadedAmount = 0f;
            return (cargoUnloaded, amountUnloaded);
        }

        public Track Track
        {
            get
            {
                if (bog1TrackChildInd.HasValue && bog2TrackChildInd.HasValue && bog1TrackChildInd.Value == bog2TrackChildInd.Value)
                {
                    return CarsSaveManager.Instance.OrderedRailtracks[bog1TrackChildInd.Value].logicTrack;
                }
                return null;
            }
        }

        public readonly TrainCarType type;
        private bool playerSpawn;
        private Vector3 position;
        private Vector3 rotation;
        public readonly float length;
        private int? bog1TrackChildInd;
        private double? bog1PosOnTrack;
        private bool bog1Derailed;
        private int? bog2TrackChildInd;
        private double? bog2PosOnTrack;
        private bool bog2Derailed;
        private bool coupledF;
        private bool coupledR;
        private bool exploded;
        public CargoType loadedCargo { get; private set; }
        private float loadedAmount;
        public readonly float cargoCapacity;
        private JObject carState;
        private JObject locoState; 
    }
}
