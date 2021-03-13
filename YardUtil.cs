using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;

namespace DVIndustry
{
    static class YardUtil
    {
        public static YardTrackInfo FindBestFitTrack( IEnumerable<YardTrackInfo> trackPool, float requiredLength )
        {
            YardTrackInfo bestCandidate = null;
            double minSpace = float.PositiveInfinity;

            foreach( YardTrackInfo trackInfo in trackPool )
            {
                double freeSpace = YardTracksOrganizer.Instance.GetFreeSpaceOnTrack(trackInfo.Track);
                if( freeSpace >= requiredLength )
                {
                    if( freeSpace < minSpace )
                    {
                        bestCandidate = trackInfo;
                        minSpace = freeSpace;
                    }
                }
            }

            return bestCandidate;
        }

        public static float GetConsistLength( ICollection<Car> cars )
        {
            float totalLength = 0;
            foreach( Car car in cars )
            {
                totalLength += car.length;
            }

            return totalLength + 0.3f * (cars.Count + 1);
        }

        public static float GetConsistLength( ICollection<TrainCar> cars )
        {
            float totalLength = 0;
            foreach( TrainCar car in cars )
            {
                totalLength += car.InterCouplerDistance;
            }

            return totalLength + 0.3f * (cars.Count + 1);
        }
    }
}
