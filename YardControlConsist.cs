using DV.Logic.Job;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DVIndustry
{
    public enum YardConsistState
    {
        Empty = 0,
        Loaded,
        Loading,
        Unloading
    }

    public class YardControlConsist
    {
        private const float CAR_STOPPED_EPSILON = 0.2f;

        private List<TrainCar> Cars;
        private List<VirtualTrainCar> VirtualCars;
        public YardConsistState State { get; private set; }
        public ResourceClass CargoClass { get; private set; }
        public string Destination { get; private set; }

        public float LastUpdateTime = 0;

        public Track Track
        {
            get
            {
                if (Cars != null && Cars.Count > 0 && !Cars.Any(tc => tc.Bogies[0].track != tc.Bogies[1].track || tc.Bogies[0].track != Cars[0].Bogies[0].track))
                {
                    return Cars[0].Bogies[0].track.logicTrack;
                }
                else if (VirtualCars != null && VirtualCars.Count > 0 && !VirtualCars.Any(vtc => vtc.Track != VirtualCars[0].Track))
                {
                    return VirtualCars[0].Track;
                }
                return null;
            }
        }

        public float Length => Cars != null ? YardUtil.GetConsistLength(Cars) : VirtualCars != null ? YardUtil.GetConsistLength(VirtualCars) : 0;

        public int CarCount => Cars != null ? Cars.Count : VirtualCars != null ? VirtualCars.Count : 0;

        public IEnumerable<TrainCar> TrainCars => Cars;
        public IEnumerable<Car> LogicCars => Cars.Select(tc => tc.logicCar);

        public YardControlConsist( IEnumerable<TrainCar> cars, YardConsistState state )
        {
            Cars = cars.ToList();
            State = state;
        }

        public void Instantiate()
        {
            if (VirtualCars != null)
            {
                Cars = VirtualCars.Select(vtc => vtc.Instantiate()).ToList();
                VirtualCars = null;
            }
        }

        public void Virtualize()
        {
            if (Cars != null)
            {
                VirtualCars = Cars.Select(tc => new VirtualTrainCar(tc)).ToList();
                Cars = null;
            }
        }

        public bool CanHoldResource( ResourceClass resource )
        {
            if (Cars != null)
            {
                return Cars.All(tc => resource.CanBeHeldBy(tc.carType));
            }

            if (VirtualCars != null)
            {
                return VirtualCars.All(vtc => resource.CanBeHeldBy(vtc.type));
            }

            return false;
        }

        private static bool CarIsStationary(TrainCar car) => Math.Abs(car.GetForwardSpeed()) < CAR_STOPPED_EPSILON;

        #region Loading/Unloading

        public bool BeginLoading( ResourceClass resourceClass, string dest )
        {
            if (State != YardConsistState.Empty) return false;

            CargoClass = resourceClass;
            Destination = dest;
            State = YardConsistState.Loading;
            return true;
        }

        // returns the amount of cargo loaded, 0 if finished, or -1 for failure
        public (CargoType, float) LoadNextCar()
        {
            if (State != YardConsistState.Loading || !YardController.IsOnLoadingTrack(this))
            {
                return ( CargoType.None, -1 );
            }

            if (Cars != null)
            {
                var carToLoad = Cars.FirstOrDefault(tc => tc.logicCar.CurrentCargoTypeInCar == CargoType.None);
                if (carToLoad == null)
                {
                    FinishLoading();
                    return ( CargoType.None, 0 );
                }
                if (!CarIsStationary(carToLoad))
                {
                    return ( CargoType.None, -1 );
                }
                var cargoToLoad = CargoClass.GetCargoForCar(carToLoad.carType);
                if (cargoToLoad == CargoType.None)
                {
                    return ( CargoType.None, -1 );
                }
                carToLoad.logicCar.LoadCargo(carToLoad.cargoCapacity, cargoToLoad);
                LastUpdateTime = Time.time;
                return ( cargoToLoad, carToLoad.cargoCapacity );
            }
            
            if (VirtualCars != null)
            {
                var carToLoad = VirtualCars.FirstOrDefault(vtc => vtc.loadedCargo == CargoType.None);
                if (carToLoad == null)
                {
                    FinishLoading();
                    return ( CargoType.None, 0 );
                }
                var cargoToLoad = CargoClass.GetCargoForCar(carToLoad.type);
                if (cargoToLoad == CargoType.None)
                {
                    return ( CargoType.None, -1 );
                }
                LastUpdateTime = Time.time;
                return ( cargoToLoad, carToLoad.LoadCargo(cargoToLoad) );
            }

            return ( CargoType.None, -1 );
        }

        private void FinishLoading()
        {
            State = YardConsistState.Loaded;
        }

        public bool BeginUnloading(ResourceClass resourceClass)
        {
            if (State != YardConsistState.Loaded) return false;

            State = YardConsistState.Unloading;
            return true;
        }

        // returns the amount of cargo unloaded, 0 if finished, or -1 for failure
        public (CargoType, float) UnloadNextCar()
        {
            if (State != YardConsistState.Unloading || !YardController.IsOnLoadingTrack(this))
            {
                return ( CargoType.None, -1 );
            }

            if (Cars != null)
            {
                var carToUnload = Cars.FirstOrDefault(tc => tc.logicCar.CurrentCargoTypeInCar != CargoType.None);
                if (carToUnload == null)
                {
                    FinishUnloading();
                    return (CargoType.None, 0);
                }
                var cargoToUnload = carToUnload.LoadedCargo;
                var amountToUnload = carToUnload.cargoCapacity;
                carToUnload.logicCar.UnloadCargo(amountToUnload, cargoToUnload);
                LastUpdateTime = Time.time;
                return ( cargoToUnload, amountToUnload );
            }

            if (VirtualCars != null)
            {
                var carToLoad = VirtualCars.FirstOrDefault(vtc => vtc.loadedCargo != CargoType.None);
                if (carToLoad == null)
                {
                    FinishUnloading();
                    return (CargoType.None, 0);
                }
                LastUpdateTime = Time.time;
                return carToLoad.UnloadCargo();
            }

            return (CargoType.None, -1);
        }

        private void FinishUnloading()
        {
            State = YardConsistState.Empty;
            CargoClass = null;
            Destination = null;
        }

        #endregion

        #region Repositioning

        public bool ShiftCars(double span, bool forward = true)
        {
            // TODO: shift cars algorithm
            throw new NotImplementedException();
        }

        public bool RelocateCars(Track track)
        {
            // TODO: relocate cars algorithm
            throw new NotImplementedException();
        }

        #endregion

        #region Utilities

        public JobLicenses GetRequiredLicensesForConsist()
        {
            var result = LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(CarCount);
            if( State != YardConsistState.Empty )
            {
                result |= LicenseManager.GetRequiredLicensesForCargoTypes(CargoClass.Cargos.ToList());
            }
            return result;
        }

        public static int CompareByLicense( YardControlConsist x, YardControlConsist y )
        {
            int result = 0;
            JobLicenses xReqs = x.GetRequiredLicensesForConsist();
            if( LicenseManager.GetMissingLicensesForJob(xReqs) == 0 )
            {
                // player has all licenses required for consist x
                result -= 1;
            }
            JobLicenses yReqs = y.GetRequiredLicensesForConsist();
            if( LicenseManager.GetMissingLicensesForJob(yReqs) == 0 )
            {
                // player has all licenses required for consist y
                result += 1;
            }
            return result;
        }

        #endregion
    }
}
