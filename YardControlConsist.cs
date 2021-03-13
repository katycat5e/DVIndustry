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
        WaitingForTransport,
        WaitingForPlayerShunt,
        Loading,
        Unloading,
        WaitingForLoad,
        WaitingForUnload
    }

    public class YardControlConsist : IEnumerable<TrainCar>
    {
        public readonly List<TrainCar> Cars;
        public Track Track { get; private set; }
        public YardConsistState State;
        public float LastUpdateTime = 0;

        public event Action<YardControlConsist, Job> JobEnded;

        private Job _currentJob = null;
        public Job CurrentJob
        {
            get => _currentJob;
            set
            {
                _currentJob = value;
                if( _currentJob != null )
                {
                    _currentJob.JobCompleted += OnCurrentJobEnded;
                    _currentJob.JobAbandoned += OnCurrentJobEnded;
                    _currentJob.JobExpired += OnCurrentJobEnded;

                    LoadDestination = _currentJob.chainData.chainDestinationYardId;
                }
                Track = null;
            }
        }

        public string LoadDestination = null;
        public ResourceClass LoadResource = null;


        public float Length => YardUtil.GetConsistLength(Cars);

        public int CarCount => Cars.Count;

        public List<Car> LogicCars => Cars.Select(tc => tc.logicCar).ToList();

        public YardControlConsist( Track track, IEnumerable<TrainCar> cars, YardConsistState state )
        {
            Cars = cars.ToList();
            Track = track;
            State = state;
        }

        public bool CanHoldResource( ResourceClass resource )
        {
            foreach( TrainCar car in Cars )
            {
                if( !resource.CanBeHeldBy(car.carType) ) return false;
            }

            return true;
        }

        private void OnCurrentJobEnded( Job job )
        {
            Track = Cars[0].logicCar.CurrentTrack;
            _currentJob = null;
            JobEnded?.Invoke(this, job);
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
