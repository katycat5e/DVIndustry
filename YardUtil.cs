using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
