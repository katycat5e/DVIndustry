using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVIndustry
{
    public class ShipmentOrganizer
    {
        public Waybill[] CreateWaybills( string resourceKey, int carsToAssign )
        {
            // from Industries select (id, demand) where (demand > 1)
            List<Tuple<string, float>> demandVector = IndustryController.AllControllers
                .Select(cont => new Tuple<string, float>(cont.StationId, Mathf.Floor(cont.GetDemand(resourceKey))))
                .Where(tup => tup.Item2 > 1f)
                .ToList();

            bool validDivision = false;
            while( !validDivision && (demandVector.Count > 1) )
            {
                float totalDemand = demandVector.Sum(tup => tup.Item2);
                float scale = (carsToAssign > totalDemand) ? 1f : carsToAssign / totalDemand;

                validDivision = true;
                int minIdx = -1;
                float minDemand = float.PositiveInfinity;

                for( int i = 0; i < demandVector.Count; i++ )
                {
                    float scaledDemand = demandVector[i].Item2 * scale;

                    if( scaledDemand < minDemand )
                    {
                        minIdx = i;
                        minDemand = scaledDemand;
                    }

                    // we can't accept a division of cars with less than a carload going to a destination
                    if( scaledDemand < 1f )
                    {
                        validDivision = false;
                    }
                }

                // if bad division, remove the smallest demand and try again
                if( !validDivision )
                {
                    demandVector.RemoveAt(minIdx);
                }
            }

            // whew okay so we created a valid division somehow, even if it's all just one destination
            Waybill[] output = new Waybill[demandVector.Count];
            int totalAssigned = 0;

            // create the waybills for each destination
            for( int i = 0; i < demandVector.Count; i++ )
            {
                int assignedCars;
                if( i < demandVector.Count - 1 )
                {
                    assignedCars = Mathf.RoundToInt(demandVector[i].Item2);
                }
                else
                {
                    // last destination gets any bonus leftover after rounding (make sure all are used)
                    assignedCars = carsToAssign - totalAssigned;
                }

                output[i] = new Waybill(resourceKey, demandVector[i].Item1, assignedCars);
            }

            return output;
        }
    }

    public class Waybill
    {
        public string ResourceKey;
        public string DestYard;
        public int CarCount;

        public Waybill( string resource, string dest, int nCars )
        {
            ResourceKey = resource;
            DestYard = dest;
            CarCount = nCars;
        }
    }
}
