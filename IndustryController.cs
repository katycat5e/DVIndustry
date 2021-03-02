using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DV.Logic.Job;
using UnityEngine;

namespace DVIndustry
{
    public class IndustryResource
    {
        public ResourceClass AcceptedItems;
        public float Amount;

        public string Key => AcceptedItems?.ID;

        public IndustryResource( ResourceClass accepted, float amount )
        {
            AcceptedItems = accepted;
            Amount = amount;
        }

        public IndustryResource( CargoType singleType, float amount )
            : this(new ResourceClass(singleType), amount)
        { }
    }

    public class IndustryProcess
    {
        public int ProcessingTime;
        public IndustryResource[] Inputs;
        public IndustryResource[] Outputs;

        public float StartTime;

        public bool IsWorking { get; set; }
        public bool IsFinished => (Time.time - StartTime) >= ProcessingTime;
    }

    public class IndustryController : MonoBehaviour
    {
        protected readonly Dictionary<string, IndustryResource> stockpileMap;
        protected readonly List<IndustryResource> inputStockpile;
        protected readonly List<IndustryResource> outputStockpile;
        protected readonly List<IndustryProcess> processes;

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

        protected void StoreResource( IndustryResource resource )
        {
            if( stockpileMap.TryGetValue(resource.Key, out IndustryResource stock) )
            {
                stock.Amount += resource.Amount;
                return;
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store an output ({resource.AcceptedItems.ID}) that this industry doesn't produce");
        }

        public void OnEnable()
        {
            foreach( var process in processes )
            {
                process.IsWorking = false;
            }
        }

        public void Update()
        {
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
