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

    public class IndustryController : ControllerBase<IndustryController, IndustrySaveData>
    {
        protected Dictionary<string, IndustryResource> stockpileMap;
        protected IndustryResource[] inputStockpile;
        protected IndustryResource[] outputStockpile;
        protected IndustryProcess[] processes;

        protected Dictionary<string, float> resourceRates = new Dictionary<string, float>();

        public IEnumerable<string> InputResources => inputStockpile.Select(r => r.Key);
        public IEnumerable<string> OutputResources => outputStockpile.Select(r => r.Key);
        public IEnumerable<string> AllResources => stockpileMap.Keys;

        protected bool waitingForLoadComplete = true;

        public void Initialize( IndustryProcess[] processConfig )
        {
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

            CalculateProcessRates();
        }

        protected void CalculateProcessRates()
        {
            resourceRates.Clear();
            foreach( IndustryProcess process in processes )
            {
                foreach( IndustryResource ingred in process.Inputs )
                {
                    if( !resourceRates.TryGetValue(ingred.Key, out float current) )
                    {
                        current = 0;
                    }

                    resourceRates[ingred.Key] = current + (ingred.Amount / process.ProcessingTime);
                }
            }
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

        public ResourceClass StoreInputCargo( CargoType cargoType, float amount )
        {
            foreach( IndustryResource stock in inputStockpile )
            {
                if( stock.AcceptedItems.ContainsCargo(cargoType) )
                {
                    stock.Amount += amount;
                    return stock.AcceptedItems;
                }
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store an input ({cargoType}) that this industry doesn't accept");
            return null;
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

        public int GetDemand( string resourceKey )
        {
            if( resourceRates.TryGetValue(resourceKey, out float consumeRate) )
            {
                double curAmount = stockpileMap[resourceKey].Amount;

                // logistic curve for demand based on lack of resource (lower stock -> higher demand)
                double amt40min = consumeRate * 2400;
                double exponent = -(10f / amt40min) * (curAmount - (amt40min / 2));
                return Mathd.FloorToInt(amt40min * 1.2d * (1 - 1 / (1 + Math.Exp(exponent))));
            }

            return 0;
        }

        public void OnEnable()
        {
            AttachedStation = gameObject.GetComponent<StationController>();
            RegisterController(StationId, this);

            if( processes == null ) return;

            foreach( var process in processes )
            {
                process.IsWorking = false;
            }
        }

        public void Update()
        {
            if( !IndustrySaveDataManager.IsLoadCompleted ) return;

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

        public override IndustrySaveData GetSaveData()
        {
            return new IndustrySaveData(StationId, stockpileMap.Values);
        }

        private static IEnumerator<IndustryResource> ProcessStockpiles( IndustrySaveData industryData )
        {
            if( industryData.StockPiles == null ) yield break;

            foreach( var kvp in industryData.StockPiles )
            {
                if( IndustryResource.TryParse(kvp.Key, kvp.Value, out IndustryResource nextRes) )
                {
                    // successfully parsed
                    yield return nextRes;
                }
                else
                {
                    DVIndustry.ModEntry.Logger.Warning($"Invalid stockpile resource at {industryData.StationId}");
                }
            }

            yield break;
        }

        public override void ApplySaveData( IndustrySaveData data )
        {
            var stonks = ProcessStockpiles(data);

            while( stonks.MoveNext() )
            {
                IndustryResource resource = stonks.Current;
                StoreResource(resource);
            }
        }
    }
}
