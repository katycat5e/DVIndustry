using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DV.Logic.Job;
using System.Collections;

namespace DVIndustry
{
    public static class ShipmentOrganizer
    {
        class IndustryYardPair
        {
            public readonly IndustryController Industry;
            public readonly YardController Yard;

            public string ID => Industry.StationId;

            public IndustryYardPair( IndustryController industry, YardController yard )
            {
                Industry = industry;
                Yard = yard;
            }
        }

        class ShipmentData
        {
            public ResourceClass Resource;
            public int Amount;

            public ShipmentData( ResourceClass resource, int amount )
            {
                Resource = resource;
                Amount = amount;
            }
        }

        private static readonly Dictionary<string, List<IndustryYardPair>> acceptingIndustries =
            new Dictionary<string, List<IndustryYardPair>>();

        private static readonly Dictionary<string, Dictionary<ResourceClass, int>> pendingShipments =
            new Dictionary<string, Dictionary<ResourceClass, int>>();

        public static void RegisterStation( IndustryController industry, YardController yard )
        {
            var pair = new IndustryYardPair(industry, yard);

            // add ref to this station under each resource it accepts
            foreach( string resource in industry.InputResources )
            {
                if( !acceptingIndustries.TryGetValue(resource, out List<IndustryYardPair> destList) )
                {
                    destList = new List<IndustryYardPair>();
                    acceptingIndustries[resource] = destList;
                }

                destList.Add(pair);
            }

            // add station to the pending shipments dict
            pendingShipments[industry.StationId] = new Dictionary<ResourceClass, int>();
        }

        public static void OnShipmentCreated( string destYard, ResourceClass resource, int nCars )
        {
            var shipmentDict = pendingShipments[destYard];

            if( shipmentDict.TryGetValue(resource, out int shipmentAmt) )
            {
                shipmentDict[resource] = shipmentAmt + nCars;
            }
            else
            {
                shipmentDict[resource] = nCars;
            }
        }

        public static void OnCarUnloaded( string stationId, ResourceClass resource )
        {
            var shipmentDict = pendingShipments[stationId];

            if( shipmentDict.TryGetValue(resource, out int shipmentAmt) )
            {
                shipmentDict[resource] = shipmentAmt - 1;
            }
        }

        private static IEnumerable<ProductRequest> GetRequests( ResourceClass resource )
        {
            // Get industries where demand for product
            List<ProductRequest> requests = new List<ProductRequest>();
            string key = resource.ID;

            if( acceptingIndustries.TryGetValue(key, out List<IndustryYardPair> sinks) )
            {
                foreach( IndustryYardPair station in sinks )
                {
                    // get demand, subtract active/pending shipments
                    int demand = station.Industry.GetDemand(key);
                    if( pendingShipments.TryGetValue(station.ID, out var shipments) && (shipments.Count > 0) )
                    {
                        foreach( ShipmentData shipment in shipments )
                        {
                            if( key == shipment.Resource.ID )
                            {
                                demand -= shipment.Amount;
                            }
                        }
                    }

                    if( demand > 0 )
                    {
                        requests.Add(new ProductRequest(resource, station.ID, demand));
                    }
                }
            }

            return requests.OrderByDescending(req => req.CarCount);
        }

        public static void UpdateProductDemand( ProductRequestCollection requestCollection )
        {
            requestCollection.Requests.Clear();
            requestCollection.Requests.AddRange(GetRequests(requestCollection.Resource));
        }
    }

    public class ProductRequestCollection
    {
        public readonly ResourceClass Resource;
        public readonly List<ProductRequest> Requests = new List<ProductRequest>();

        public ProductRequestCollection( ResourceClass resource )
        {
            Resource = resource;
        }
    }

    public class ProductRequest
    {
        public ResourceClass Resource;
        public string DestYard;
        public int CarCount;

        public ProductRequest( ResourceClass resource, string dest, int nCars )
        {
            Resource = resource;
            DestYard = dest;
            CarCount = nCars;
        }
    }
}
