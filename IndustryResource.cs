using DV.Logic.Job;

namespace DVIndustry
{
    public class IndustryResource
    {
        public readonly ResourceClass AcceptedItems;
        public float Amount;

        public string Key => AcceptedItems?.ID;

        public IndustryResource( ResourceClass accepted, float amount )
        {
            AcceptedItems = accepted;
            Amount = amount;
        }

        public IndustryResource( CargoType singleType, float amount )
            : this(ResourceClass.SingleCargoClass(singleType), amount)
        { }

        public IndustryResource CloneEmpty()
        {
            return new IndustryResource(AcceptedItems, 0);
        }


        public static bool TryParse( string key, float amount, out IndustryResource resource )
        {
            if( ResourceClass.TryParse(key, out ResourceClass rClass) )
            {
                resource = new IndustryResource(rClass, amount);
                return true;
            }

            resource = null;
            DVIndustry.ModEntry.Logger.Critical($"Unrecognized resource class \"{key}\"");
            return false;
        }
    }
}
