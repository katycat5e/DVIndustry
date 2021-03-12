using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            throw new NotImplementedException();
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            throw new NotImplementedException();
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            throw new NotImplementedException();
        }
    }
}
