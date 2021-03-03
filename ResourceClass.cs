using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using DV.Logic.Job;

namespace DVIndustry
{
    public class ResourceClass
    {
        public readonly string ID;
        public readonly CargoType[] Cargos;

        private ResourceClass() { }

        private ResourceClass( string id, IEnumerable<CargoType> types )
        {
            ID = id;
            Cargos = types.ToArray();
        }

        private ResourceClass( CargoType singleType, string overrideId = null )
        {
            ID = overrideId ?? Enum.GetName(typeof(CargoType), singleType);
            Cargos = new CargoType[] { singleType };
        }

        private ResourceClass( string id, IEnumerable<CargoType> types, params ResourceClass[] toInclude )
            : this(id, types.Union(toInclude.SelectMany(rc => rc.Cargos)))
        {
        }

        public bool ContainsCargo( CargoType cargo )
        {
            return Cargos.Contains(cargo);
        }

        public override bool Equals( object obj )
        {
            if( base.Equals(obj) ) return true;
            if( obj is ResourceClass other )
            {
                return string.Equals(ID, other.ID);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        // End Instance Members
        // Static Members:

        private static readonly Dictionary<CargoType, ResourceClass> singleResourceClassMap =
            new Dictionary<CargoType, ResourceClass>();

        public static ResourceClass SingleCargoClass( CargoType singleType )
        {
            if( singleResourceClassMap.TryGetValue(singleType, out var resource) )
            {
                return resource;
            }
            else
            {
                resource = new ResourceClass(singleType);
                singleResourceClassMap[singleType] = resource;
                return resource;
            }
        }

        public static readonly ResourceClass AgProducts = new ResourceClass(
            "AgProducts",
            new CargoType[]
            {
                CargoType.Pigs,
                CargoType.Cows,
                CargoType.Chickens,
                CargoType.Sheep,
                CargoType.Goats,
                CargoType.Wheat,
                CargoType.Corn
            }
        );

        public static readonly ResourceClass FreshFood = new ResourceClass(
            "FreshFood",
            new CargoType[]
            {
                CargoType.Bread,
                CargoType.DairyProducts,
                CargoType.MeatProducts
            }
        );

        public static readonly ResourceClass Steel = new ResourceClass(
            "Steel",
            new CargoType[]
            {
                CargoType.SteelRolls,
                CargoType.SteelBillets,
                CargoType.SteelSlabs,
                CargoType.SteelBentPlates
            }
        );

        public static readonly ResourceClass Lumber = new ResourceClass(
            "Lumber",
            new CargoType[]
            {
                CargoType.Boards,
                CargoType.Plywood
            }
        );

        public static readonly ResourceClass NewVehicles = new ResourceClass(
            "NewVehicles",
            new CargoType[]
            {
                CargoType.NewCars,
                CargoType.Tractors,
                CargoType.Excavators
            }
        );

        public static readonly ResourceClass Electronics = new ResourceClass(
            "Electronics",
            new CargoType[]
            {
                CargoType.ElectronicsIskar,
                CargoType.ElectronicsKrugmann,
                CargoType.ElectronicsAAG,
                CargoType.ElectronicsNovae,
                CargoType.ElectronicsTraeg
            }
        );
        
        public static readonly ResourceClass Tooling = new ResourceClass(
            "Tooling",
            new CargoType[]
            {
            CargoType.ToolsIskar,
            CargoType.ToolsBrohm,
            CargoType.ToolsAAG,
            CargoType.ToolsNovae,
            CargoType.ToolsTraeg
            }
        );

        public static readonly ResourceClass Clothing = new ResourceClass(
            "Clothing",
            new CargoType[]
            {
                CargoType.ClothingObco,
                CargoType.ClothingNeoGamma,
                CargoType.ClothingNovae,
                CargoType.ClothingTraeg
            }
        );

        public static readonly ResourceClass ConsumerGoods = new ResourceClass(
            "ConsumerGoods",
            new CargoType[]
            {
                CargoType.Methane,
                CargoType.NewCars,
                CargoType.CannedFood,
                CargoType.CatFood,
                CargoType.Diesel,
                CargoType.Gasoline,
                CargoType.Medicine,
                CargoType.ImportedNewCars,
                CargoType.ChemicalsIskar,
                CargoType.ChemicalsSperex,
                CargoType.Furniture
            },
            FreshFood,
            Electronics,
            Tooling,
            Clothing
        );


        public static readonly Dictionary<string, ResourceClass> BuiltinClasses = new Dictionary<string, ResourceClass>();
        //{
        //    { AgProducts.ID, AgProducts },
        //    { FreshFood.ID, FreshFood },
        //    { Steel.ID, Steel },
        //    { Lumber.ID, Lumber },
        //    { NewVehicles.ID, NewVehicles },
        //    { Electronics.ID, Electronics },
        //    { Tooling.ID, Tooling },
        //    { Clothing.ID, Clothing },
        //    { ConsumerGoods.ID, ConsumerGoods }
        //};

        static ResourceClass()
        {
            // get all public ResourceClass Fields
            var builtinClassFields = typeof(ResourceClass).GetFields(BindingFlags.Public|BindingFlags.Static)
                .Where(f => f.FieldType.Equals(typeof(ResourceClass)));

            foreach( FieldInfo field in builtinClassFields )
            {
                ResourceClass resource = field.GetValue(null) as ResourceClass;
                BuiltinClasses[resource.ID] = resource;
            }
        }

        public static bool TryParse( string id, out ResourceClass resource )
        {
            if( BuiltinClasses.TryGetValue(id, out resource) )
            {
                return true;
            }
            else if( Enum.TryParse(id, out CargoType cargo) )
            {
                resource = SingleCargoClass(cargo);
                return true;
            }
            return false;
        }
    }
}
