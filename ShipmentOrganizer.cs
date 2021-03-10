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
            public Job Job;
            public ResourceClass Resource;
            public int Amount;

            public ShipmentData( Job job, ResourceClass resource, int amount )
            {
                Job = job;
                Resource = resource;
                Amount = amount;
            }
        }

        private static readonly Dictionary<string, List<IndustryYardPair>> acceptingIndustries =
            new Dictionary<string, List<IndustryYardPair>>();

        private static readonly Dictionary<string, FancyLinkedList<ShipmentData>> pendingShipments =
            new Dictionary<string, FancyLinkedList<ShipmentData>>();

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
            pendingShipments[industry.StationId] = new FancyLinkedList<ShipmentData>();
        }

        public static void OnShipmentCreated( Job job, ResourceClass resource, int nCars )
        {
            var shipment = new ShipmentData(job, resource, nCars);
            string destYard = job.chainData.chainDestinationYardId;

            pendingShipments[destYard].AddLast(shipment);
        }

        public static void OnShipmentEnded( Job job )
        {
            string destYard = job.chainData.chainDestinationYardId;

            if( pendingShipments.TryGetValue(destYard, out var shipmentList) )
            {
                shipmentList.RemoveFirstWhere(s => s.Job == job);
            }
        }

        private static IEnumerable<ProductRequest> GetRequests( string key )
        {
            // Get industries where demand for product
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
                        yield return new ProductRequest(key, station.ID, demand);
                    }
                }
            }

            yield break;
        }

        public static void UpdateProductDemand( ProductRequestCollection requestCollection )
        {
            requestCollection.Requests.Clear();
            requestCollection.Requests.AddRange(GetRequests(requestCollection.Resource.Key));
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
        public string ResourceKey;
        public string DestYard;
        public int CarCount;

        public ProductRequest( string resource, string dest, int nCars )
        {
            ResourceKey = resource;
            DestYard = dest;
            CarCount = nCars;
        }
    }
}
