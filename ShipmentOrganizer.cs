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

        // consists waiting to be delivered to a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingLoads =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

        // resources waiting to be taken away from a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingShipments =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

        // resources waiting to be received by a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingDeliveries =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

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

            // initialize pending input/output trackers
            pendingLoads[industry.StationId] = new Dictionary<ResourceClass, float>();
            pendingShipments[industry.StationId] = new Dictionary<ResourceClass, float>();
            pendingDeliveries[industry.StationId] = new Dictionary<ResourceClass, float>();
        }

        public static void OnLogisticsPlanned( string destYard, ResourceClass resource, float amount )
        {
            var loadDict = pendingLoads[destYard];

            if( !loadDict.TryGetValue(resource, out float loadAmount) )
            {
                loadAmount = 0;
            }
            loadDict[resource] = loadAmount + amount;
        }

        public static void OnShipmentCreated( string originYard, ResourceClass resource, float amount )
        {
            var shipmentDict = pendingShipments[originYard];

            if( !shipmentDict.TryGetValue(resource, out float shipmentAmt) )
            {
                shipmentAmt = 0;
            }
            shipmentDict[resource] = shipmentAmt + amount;
        }

        public static void OnCarLoaded( string originYard, string destYard, ResourceClass resource, float amountLoaded )
        {
            var loadDict = pendingLoads[originYard];

            if( loadDict.TryGetValue(resource, out float amountToBeLoaded) )
            {
                loadDict[resource] = amountToBeLoaded - amountLoaded;
            }

            var shipmentDict = pendingShipments[originYard];

            if( shipmentDict.TryGetValue(resource, out float amountToBeShipped) )
            {
                shipmentDict[resource] = amountToBeShipped - amountLoaded;
            }

            var deliveryDict = pendingDeliveries[destYard];

            if( !deliveryDict.TryGetValue(resource, out float amountToBeDelivered) )
            {
                amountToBeDelivered = 0;
            }
            deliveryDict[resource] = amountToBeDelivered + amountLoaded;
        }

        public static void OnCarUnloaded( string destYard, ResourceClass resource, float amountUnloaded )
        {
            var deliveryDict = pendingDeliveries[destYard];

            if( deliveryDict.TryGetValue(resource, out float amountToBeDelivered) )
            {
                deliveryDict[resource] = amountToBeDelivered - amountUnloaded;
            }
        }

        public static IEnumerable<ProductCapacity> GetCapacities( YardControlConsist consist )
        {
            List<ProductCapacity> capacities = new List<ProductCapacity>();

            foreach( var (station, pendingResources) in pendingShipments )
            {
                // need to skip other stations if previous job generation has already locked in a station for this consist
                if( consist.Destination != null && consist.Destination != station ) continue;

                foreach( var (resourceToShip, amountToShip) in pendingResources )
                {
                    float supply = amountToShip;

                    if( pendingLoads.TryGetValue(station, out var loads) && (loads.Count > 0) )
                    {
                        foreach( var (resourceToLoad, amountToLoad) in loads )
                        {
                            if( resourceToLoad.ID == resourceToShip.ID )
                            {
                                supply -= amountToLoad;
                            }
                        }
                    }

                    if ( consist.CanHoldResource(resourceToShip) && supply > 0 )
                    {
                        capacities.Add(new ProductCapacity(resourceToShip, station, supply));
                    }
                }
            }

            return capacities.OrderByDescending(cap => cap.Amount);
        }

        private static IEnumerable<ProductRequest> GetRequests( ResourceClass resource )
        {
            // Get industries where demand for product
            List<ProductRequest> requests = new List<ProductRequest>();
            string resourceId = resource.ID;

            if( acceptingIndustries.TryGetValue(resourceId, out List<IndustryYardPair> sinks) )
            {
                foreach( IndustryYardPair station in sinks )
                {
                    // get demand, subtract active/pending shipments
                    float demand = station.Industry.GetDemand(resource);

                    if( pendingDeliveries.TryGetValue(station.ID, out var shipments) && (shipments.Count > 0) )
                    {
                        foreach( var (resourceToDeliver, amountToDeliver) in shipments )
                        {
                            if( resourceToDeliver.ID == resourceId )
                            {
                                demand -= amountToDeliver;
                            }
                        }
                    }

                    if( demand > 0 )
                    {
                        requests.Add(new ProductRequest(resource, station.ID, demand));
                    }
                }
            }

            return requests.OrderByDescending(req => req.Amount);
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
        public float Amount;

        public ProductRequest( ResourceClass resource, string dest, float amount )
        {
            Resource = resource;
            DestYard = dest;
            Amount = amount;
        }
    }

    public class ProductCapacity
    {
        public ResourceClass Resource;
        public string OriginYard;
        public float Amount;

        public ProductCapacity(ResourceClass resource, string origin, float amount)
        {
            Resource = resource;
            OriginYard = origin;
            Amount = amount;
        }
    }
}
