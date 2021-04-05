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
        #region Static Members

        public static readonly Dictionary<string, ResourceClass> BuiltinClasses = new Dictionary<string, ResourceClass>();

        private static readonly Random rand = new Random();
        private static readonly Dictionary<CargoType, ResourceClass> singleResourceClassMap =
            new Dictionary<CargoType, ResourceClass>();

        static ResourceClass()
        {
            // get all public ResourceClass Fields
            var builtinClassFields = typeof(ResourceClass).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType.Equals(typeof(ResourceClass)));

            foreach( FieldInfo field in builtinClassFields )
            {
                ResourceClass resource = field.GetValue(null) as ResourceClass;
                BuiltinClasses[resource.ID] = resource;
            }
        }

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

        public static ResourceClass Parse( string id )
        {
            if( TryParse(id, out ResourceClass parsed) ) return parsed;
            else return null;
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

        #endregion
        //============================================================================================
        #region Instance Members

        public readonly string ID;
        public readonly CargoType[] Cargos;
        public float AverageValue { get; private set; }

        public HashSet<TrainCarType> CompatibleCars { get; private set; }

        private void Init()
        {
            var containers = Cargos.SelectMany(carg => CargoTypes.GetCarContainerTypesThatSupportCargoType(carg)).Distinct();
            CompatibleCars = containers.SelectMany(cont => CargoTypes.GetTrainCarTypesThatAreSpecificContainerType(cont)).ToHashSet();

            AverageValue = Cargos.Sum(cargo => ResourceTypes.GetFullDamagePriceForCargo(cargo)) / Cargos.Length;
        }

        private ResourceClass() { }

        private ResourceClass( string id, IEnumerable<CargoType> types )
        {
            ID = id;
            Cargos = types.ToArray();
            Init();
        }

        private ResourceClass( CargoType singleType, string overrideId = null )
        {
            ID = overrideId ?? Enum.GetName(typeof(CargoType), singleType);
            Cargos = new CargoType[] { singleType };
            Init();
        }

        private ResourceClass( string id, IEnumerable<CargoType> types, params ResourceClass[] toInclude )
            : this(id, types.Union(toInclude.SelectMany(rc => rc.Cargos)))
        {
        }

        public bool ContainsClass( ResourceClass subset )
        {
            if( Equals(subset) ) return true;
            return subset.Cargos.All(c => ContainsCargo(c));
        }

        public bool ContainsCargo( CargoType cargo )
        {
            return Cargos.Contains(cargo);
        }

        public bool CanBeHeldBy( TrainCarType carType )
        {
            return CompatibleCars.Contains(carType);
        }

        public CargoType GetCargoForCar( TrainCarType carType )
        {
            if( !CanBeHeldBy(carType) ) return CargoType.None;

            if( Cargos.Length == 1 )
            {
                if( CargoTypes.CanCarContainCargoType(carType, Cargos[0]) ) return Cargos[0];
                else return CargoType.None;
            }

            CargoType[] possibleTypes = Cargos.Where(cargo => CargoTypes.CanCarContainCargoType(carType, cargo)).ToArray();
            if( possibleTypes.Length > 0 )
            {
                return possibleTypes.ChooseOne(rand);
            }
            else return CargoType.None;
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

        public override string ToString()
        {
            return $"[ResourceClass: {ID}]";
        }

        #endregion
        //============================================================================================
        #region Predefined ResourceClasses

        public static readonly ResourceClass Livestock = new ResourceClass(
            "Livestock",
            new CargoType[]
            {
                CargoType.Pigs,
                CargoType.Cows,
                CargoType.Chickens,
                CargoType.Sheep,
                CargoType.Goats
            }
        );

        public static readonly ResourceClass Grains = new ResourceClass(
            "Grains",
            new CargoType[]
            {
                CargoType.Wheat,
                CargoType.Corn
            }
        );

        public static readonly ResourceClass AgProducts = new ResourceClass(
            "AgProducts",
            new CargoType[] { },
            Livestock,
            Grains
        );

        public static readonly ResourceClass FreshMeat = new ResourceClass(
            "FreshMeat",
            new CargoType[]
            {
                CargoType.DairyProducts,
                CargoType.MeatProducts
            }
        );

        public static readonly ResourceClass CannedMeat = new ResourceClass(
            "CannedMeat",
            new CargoType[]
            {
                CargoType.CannedFood,
                CargoType.CatFood
            }
        );

        public static readonly ResourceClass SteelParts = new ResourceClass(
            "SteelParts",
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

        public static readonly ResourceClass DomesticContainers = new ResourceClass(
            "DomesticContainers",
            new CargoType[]
            {
                CargoType.EmptyIskar,
                CargoType.EmptyGoorsk,
                CargoType.EmptyObco,
                CargoType.EmptySunOmni
            }
        );

        public static readonly ResourceClass ImportedContainers = new ResourceClass(
            "ImportedContainers",
            new CargoType[]
            {
                CargoType.EmptyAAG,
                CargoType.EmptyBrohm,
                CargoType.EmptyChemlek,
                CargoType.EmptyKrugmann,
                CargoType.EmptyNeoGamma,
                CargoType.EmptyNovae,
                CargoType.EmptySperex,
                CargoType.EmptyTraeg
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

        public static readonly ResourceClass DomesticChemicals = new ResourceClass(
            "DomesticChemicals",
            new CargoType[]
            {
                CargoType.ChemicalsIskar
            }
        );

        public static readonly ResourceClass ImportedChemicals = new ResourceClass(
            "ImportedChemicals",
            new CargoType[]
            {
                CargoType.ChemicalsSperex
            }
        );

        public static readonly ResourceClass DomesticElectronics = new ResourceClass(
            "DomesticElectronics",
            new CargoType[]
            {
                CargoType.ElectronicsIskar
            }
        );

        public static readonly ResourceClass ImportedElectronics = new ResourceClass(
            "ImportedElectronics",
            new CargoType[]
            {
                CargoType.ElectronicsKrugmann,
                CargoType.ElectronicsAAG,
                CargoType.ElectronicsNovae,
                CargoType.ElectronicsTraeg
            }
        );

        public static readonly ResourceClass DomesticTooling = new ResourceClass(
            "DomesticTooling",
            new CargoType[]
            {
                CargoType.ToolsIskar
            }
        );

        public static readonly ResourceClass ImportedTooling = new ResourceClass(
            "ImportedTooling",
            new CargoType[]
            {
                CargoType.ToolsBrohm,
                CargoType.ToolsAAG,
                CargoType.ToolsNovae,
                CargoType.ToolsTraeg
            }
        );

        public static readonly ResourceClass DomesticClothing = new ResourceClass(
            "DomesticClothing",
            new CargoType[]
            {
                CargoType.ClothingObco
            }
        );

        public static readonly ResourceClass ImportedClothing = new ResourceClass(
            "ImportedClothing",
            new CargoType[]
            {
                CargoType.ClothingNeoGamma,
                CargoType.ClothingNovae,
                CargoType.ClothingTraeg
            }
        );

        public static readonly ResourceClass RefinedPetrol = new ResourceClass(
            "RefinedPetrol",
            new CargoType[]
            {
                CargoType.Diesel,
                CargoType.Gasoline
            }
        );

        public static readonly ResourceClass DomesticLooseGoods = new ResourceClass(
            "DomesticLooseGoods",
            new CargoType[]
            {
                CargoType.Bread,
                CargoType.Methane,
                CargoType.NewCars
            },
            FreshMeat
        );

        public static readonly ResourceClass DomesticContainerGoods = new ResourceClass(
            "DomesticContainerGoods",
            new CargoType[]
            {
                CargoType.Furniture
            },
            DomesticChemicals,
            DomesticClothing,
            DomesticElectronics,
            DomesticTooling,
            CannedMeat
        );

        public static readonly ResourceClass ImportedLooseGoods = new ResourceClass(
            "ImportedLooseGoods",
            new CargoType[]
            {
                CargoType.ImportedNewCars,
                CargoType.Medicine
            },
            RefinedPetrol
        );

        public static readonly ResourceClass ImportedContainerGoods = new ResourceClass(
            "ImportedContainerGoods",
            new CargoType[]
            {
                CargoType.Medicine
            },
            ImportedChemicals,
            ImportedClothing,
            ImportedElectronics,
            ImportedTooling
        );

        public static readonly ResourceClass ImportedGoods = new ResourceClass(
            "ImportedGoods",
            new CargoType[] { },
            ImportedLooseGoods,
            ImportedContainerGoods
        );

        #endregion
    }
}
