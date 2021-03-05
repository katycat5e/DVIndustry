using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DV.Logic.Job;
using UnityEngine;

namespace DVIndustry
{
    public class IndustryProcess
    {
        public float ProcessingTime;
        public IndustryResource[] Inputs;
        public IndustryResource[] Outputs;

        public float StartTime;

        public bool IsWorking { get; set; } = false;
        public bool IsFinished => (Time.time - StartTime) >= ProcessingTime;
    }

    public class IndustryController : MonoBehaviour
    {
        public StationController StationController = null;
        public string StationId => StationController?.stationInfo.YardID;

        protected Dictionary<string, IndustryResource> stockpileMap;
        protected IndustryResource[] inputStockpile;
        protected IndustryResource[] outputStockpile;
        protected IndustryProcess[] processes;

        public IEnumerable<IndustryResource> AllResources => stockpileMap.Values;

        protected bool waitingForLoadComplete = true;

        public void Initialize( IndustryProcess[] processConfig )
        {
            if( StationController == null ) StationController = gameObject.GetComponent<StationController>();

            processes = processConfig;
            if( processes != null )
            {
                foreach( var proc in processes )
                {
                    proc.IsWorking = false;
                }
            }

            // Create stockpiles for each process I/O resource
            inputStockpile = processes.SelectMany(proc => proc.Inputs.Select(r => r.CloneEmpty())).ToArray();
            outputStockpile = processes.SelectMany(proc => proc.Outputs.Select(r => r.CloneEmpty())).ToArray();

            // Add references for all stockpiles to the map
            stockpileMap = inputStockpile.Union(outputStockpile).ToDictionary(res => res.Key, res => res);

            IndustrySaveDataManager.RegisterIndustry(this);
        }

        public bool IsResourceAvailable( string resourceId, float amount )
        {
            if( stockpileMap.TryGetValue(resourceId, out IndustryResource stock) )
            {
                return stock.Amount >= amount;
            }
            return false;
        }

        protected bool AreIngredientsAvailable( IndustryProcess process )
        {
            foreach( var ingred in process.Inputs )
            {
                if( !IsResourceAvailable(ingred.Key, ingred.Amount) ) return false;
            }

            return true;
        }

        public void StoreInputCargo( CargoType cargoType, float amount )
        {
            foreach( IndustryResource stock in inputStockpile )
            {
                if( stock.AcceptedItems.ContainsCargo(cargoType) )
                {
                    stock.Amount += amount;
                    return;
                }
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store an input ({cargoType}) that this industry doesn't accept");
        }

        public void StoreResource( IndustryResource resource )
        {
            if( stockpileMap.TryGetValue(resource.Key, out IndustryResource stock) )
            {
                stock.Amount += resource.Amount;
                return;
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store a resource ({resource.AcceptedItems.ID}) that this industry doesn't use");
        }

        public void OnEnable()
        {
            if( processes == null ) return;

            foreach( var process in processes )
            {
                process.IsWorking = false;
            }
        }

        public void Update()
        {
            if( !IndustrySaveDataManager.IsLoadCompleted ) return;

            if( waitingForLoadComplete )
            {
                // need to grab saved state of resources before starting processing
                foreach( IndustryResource resource in AllResources )
                {
                    resource.Amount = IndustrySaveDataManager.GetSavedStockpileAmount(StationId, resource.Key);
                }

                waitingForLoadComplete = false;
            }

            foreach( var process in processes )
            {
                if( process.IsWorking && process.IsFinished )
                {
                    // process completed, yeet products
                    foreach( IndustryResource output in process.Outputs )
                    {
                        StoreResource(output);
                    }

                    process.IsWorking = false;
                }

                if( !process.IsWorking )
                {
                    // try to start idle process
                    if( AreIngredientsAvailable(process) )
                    {
                        process.IsWorking = true;
                        process.StartTime = Time.time;
                    }
                }
            }
        }

        public void OnDisable()
        {
            foreach( var process in processes )
            {
                if( process.IsWorking )
                {
                    process.IsWorking = false;
                    foreach( var ingred in process.Inputs )
                    {
                        StoreResource(ingred);
                    }
                }
            }
        }
    }
}
