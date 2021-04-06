using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;

namespace DVIndustry
{
    public class IndustryResource
    {
        public readonly ResourceClass AcceptedItems;
        public float Amount;

        public string Key => AcceptedItems?.ID;

        private static readonly Dictionary<string, Dictionary<string, IndustryResource>> knownIndustryResources
            = new Dictionary<string, Dictionary<string, IndustryResource>>();

        public IndustryResource( ResourceClass accepted, float amount )
        {
            AcceptedItems = accepted;
            Amount = amount;
        }

        public IndustryResource( CargoType singleType, float amount )
            : this(ResourceClass.SingleCargoClass(singleType), amount)
        { }


        public static bool TryParse( string stationId, string key, float amount, out IndustryResource resource )
        {
            if( knownIndustryResources.ContainsKey(stationId) && knownIndustryResources[stationId].ContainsKey(key) )
            {
                resource = knownIndustryResources[stationId][key];
                resource.Amount += amount;
                return true;
            }

            if( ResourceClass.TryParse(key, out ResourceClass rClass) )
            {
                resource = knownIndustryResources[stationId][key] = new IndustryResource(rClass, amount);
                return true;
            }

            resource = null;
            DVIndustry.ModEntry.Logger.Critical($"Unrecognized resource class \"{key}\"");
            return false;
        }
    }
}
