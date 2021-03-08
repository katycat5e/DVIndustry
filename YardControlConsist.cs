using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;

namespace DVIndustry
{
    public enum YardConsistState
    {
        None = 0,
        Empty,
        Full,
        Loading,
        Unloading,
        WaitingForLoad,
        WaitingForUnload
    }

    public class YardControlConsist : IEnumerable<TrainCar>
    {
        public readonly List<TrainCar> Cars;
        public Track Track;
        public YardConsistState State;
        public ResourceClass LoadResource = null;

        public CarsPerTrack CarsPerTrack => new CarsPerTrack(Track, Cars.Select(tc => tc.logicCar).ToList());
        public List<Car> LogicCars => Cars.Select(tc => tc.logicCar).ToList();

        public YardControlConsist( Track track, IEnumerable<TrainCar> cars, YardConsistState state )
        {
            Cars = cars.ToList();
            Track = track;
            State = state;
        }

        public IEnumerator<TrainCar> GetEnumerator()
        {
            return ((IEnumerable<TrainCar>)Cars).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Cars).GetEnumerator();
        }
    }
}
