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
            pendingShipments[industry.StationId] = new Dictionary<ResourceClass, float>();
            pendingDeliveries[industry.StationId] = new Dictionary<ResourceClass, float>();
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
                    float demand = station.Industry.GetDemand(resource);

                    if( pendingDeliveries.TryGetValue(station.ID, out var shipments) && (shipments.Count > 0) )
                    {
                        foreach( var shipmentPair in shipments )
                        {
                            if( key == shipmentPair.Key.ID )
                            {
                                demand -= shipmentPair.Value;
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
}
