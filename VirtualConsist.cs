using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVIndustry
{
    class VirtualConsist
    {
        private List<VirtualTrainCar> virtualTrainCars;

        public VirtualConsist( List<TrainCar> trainCars )
        {
            virtualTrainCars = trainCars.Select( tc => new VirtualTrainCar( tc ) ).ToList();
        }

        public bool ShiftCars( double span, bool forward=true )
        {
            // TODO: shift cars algorithm
            return false;
        }

        public bool RelocateCars( Track track )
        {
            // TODO: relocate cars algorithm
            return false;
        }

        public List<TrainCar> Instantiate()
        {
            return virtualTrainCars.Select( vtc => vtc.Instantiate() ).ToList();
        }
    }
}
