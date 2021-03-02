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

        public bool IsWorking;
        public float StartTime;

        public bool IsFinished => (Time.time - StartTime) >= ProcessingTime;
    }

    public abstract class IndustryBase : MonoBehaviour
    {
        protected readonly Dictionary<string, IndustryResource> inputStockpile;
        protected readonly List<IndustryResource> outputStockpile;
        protected readonly List<IndustryProcess> processes;

        public bool IsResourceAvailable( string resourceId, float amount )
        {
            if( inputStockpile.TryGetValue(resourceId, out IndustryResource stock) )
            {
                return stock.Amount > amount;
            }
            return false;
        }
        protected bool AreIngredientsAvailable( IndustryProcess process )
        {
            foreach( var ingred in process.Inputs )
            {
                if( !IsResourceAvailable(ingred.AcceptedItems.ID, ingred.Amount) ) return false;
            }

            return true;
        }

        public void AddInputResource( CargoType cargoType, float amount )
        {
            foreach( IndustryResource stock in inputStockpile.Values )
            {
                if( stock.AcceptedItems.ContainsCargo(cargoType) )
                {
                    stock.Amount += amount;
                    return;
                }
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store an input ({cargoType}) that this industry doesn't accept");
        }

        protected void AddProcessProduct( IndustryResource product )
        {
            foreach( var stock in outputStockpile )
            {
                if( stock.AcceptedItems == product.AcceptedItems )
                {
                    stock.Amount += product.Amount;
                    return;
                }
            }

            DVIndustry.ModEntry.Logger.Warning($"Tried to store an output ({product.AcceptedItems.ID}) that this industry doesn't produce");
        }

        public void Start()
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
                        AddProcessProduct(output);
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
    }
}
