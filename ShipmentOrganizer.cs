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

        // resources waiting to be taken away from a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingShipments =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

        // consists waiting to be delivered to a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingConsists =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

        // resources waiting to be received by a station
        private static readonly Dictionary<string, Dictionary<ResourceClass, float>> pendingDeliveries =
            new Dictionary<string, Dictionary<ResourceClass, float>>();

        public static void RegisterStation( IndustryController industry, YardController yard )
        {
            var pair = new IndustryYardPair(industry, yard);

            // add ref to this station under each resource it accepts
            foreach( var resource in industry.InputResources )
            {
                if( !acceptingIndustries.TryGetValue(resource, out List<IndustryYardPair> destList) )
                {
                    destList = new List<IndustryYardPair>();
                    acceptingIndustries[resource] = destList;
                }

                destList.Add(pair);
            }

            // initialize pending input/output trackers
            pendingShipments[industry.StationId] = new Dictionary<ResourceClass, float>();
            pendingConsists[industry.StationId] = new Dictionary<ResourceClass, float>();
            pendingDeliveries[industry.StationId] = new Dictionary<ResourceClass, float>();
        }

        public static void OnShipmentCreated( string outputYard, ResourceClass resource, float amount )
        {
            var shipmentDict = pendingShipments[outputYard];

            if( !shipmentDict.TryGetValue(resource, out float shipmentAmt) )
            {
                shipmentAmt = 0;
            }
            shipmentDict[resource] = shipmentAmt + amount;
        }

        public static void OnLogisticsPlanned( string outputYard, ResourceClass resource, float amount )
        {

            var shipmentDict = pendingShipments[outputYard];

            if( shipmentDict.TryGetValue(resource, out float amountToBeShipped) )
            {
                shipmentDict[resource] = amountToBeShipped - amount;
            }

            var loadDict = pendingConsists[outputYard];

            if( !loadDict.TryGetValue(resource, out float loadAmount) )
            {
                loadAmount = 0;
            }
            loadDict[resource] = loadAmount + amount;
        }

        public static void OnCarLoaded( string outputYard, string destYard, ResourceClass resource, float amountLoaded )
        {
            var consistDict = pendingConsists[outputYard];

            if( consistDict.TryGetValue(resource, out float amountToBeLoaded) )
            {
                consistDict[resource] = amountToBeLoaded - amountLoaded;
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

        public static IEnumerable<ProductOffering> GetOfferings( YardControlConsist consist )
        {
            List<ProductOffering> offerings = new List<ProductOffering>();

            foreach( var (station, pendingResources) in pendingShipments )
            {
                // need to skip other stations if previous job generation has already locked in a station for this consist
                if( consist.Destination != null && consist.Destination != station ) continue;

                foreach( var (resourceToBeShippped, amountToBeShipped) in pendingResources )
                {
                    // get available supply, subtract planned shipments
                    float supply = amountToBeShipped;

                    if( pendingConsists.TryGetValue(station, out var consistDict) && (consistDict.Count > 0) )
                    {
                        foreach( var (resourceToBeLoaded, amountToBeLoaded) in consistDict )
                        {
                            if( resourceToBeLoaded.ID == resourceToBeShippped.ID )
                            {
                                supply -= amountToBeLoaded;
                            }
                        }
                    }

                    if ( consist.CanHoldResource(resourceToBeShippped) && supply > 0 )
                    {
                        offerings.Add(new ProductOffering(resourceToBeShippped, station, supply));
                    }
                }
            }

            return offerings.OrderByDescending(cap => cap.Amount);
        }

        public static IEnumerable<ProductRequest> GetRequests( ResourceClass resource )
        {
            // Get industries where demand for product
            List<ProductRequest> requests = new List<ProductRequest>();
            string resourceId = resource.ID;

            if( acceptingIndustries.TryGetValue(resourceId, out List<IndustryYardPair> sinks) )
            {
                foreach( IndustryYardPair station in sinks )
                {
                    // get demand, subtract planned shipments
                    float demand = station.Industry.GetDemand(resource);

                    if( pendingConsists.TryGetValue(station.ID, out var consistDict) && (consistDict.Count > 0) )
                    {
                        foreach( var (resourceToBeLoaded, amountToBeLoaded) in consistDict )
                        {
                            if( resourceToBeLoaded.ID == resourceId )
                            {
                                demand -= amountToBeLoaded;
                            }
                        }
                    }

                    if( pendingDeliveries.TryGetValue(station.ID, out var shipmentDict) && (shipmentDict.Count > 0) )
                    {
                        foreach( var (resourceToBeDelivered, amountToBeDelivered) in shipmentDict )
                        {
                            if( resourceToBeDelivered.ID == resourceId )
                            {
                                demand -= amountToBeDelivered;
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

    public class ProductOffering
    {
        public ResourceClass Resource;
        public string SourceYard;
        public float Amount;

        public ProductOffering(ResourceClass resource, string source, float amount)
        {
            Resource = resource;
            SourceYard = source;
            Amount = amount;
        }
    }
}
