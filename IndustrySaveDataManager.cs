using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;

namespace DVIndustry
{
    public static class IndustrySaveDataManager
    {
        const string SAVE_KEY = "DVIndustry";
        private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static bool IsLoadCompleted { get; private set; } = false;


        

        public static void LoadIndustryData()
        {
            if( SaveGameManager.data.GetObject<IndustrySaveDataCollection>(SAVE_KEY, serializerSettings) is IndustrySaveDataCollection saveData )
            {
                // found DVIndustry save data
                // Restore saved stockpile amounts
                if( saveData.Industries != null )
                {
                    foreach( IndustrySaveData indData in saveData.Industries )
                    {
                        if( IndustryController.At(indData.StationId) is IndustryController controller )
                        {
                            controller.ApplySaveData(indData);
                        }
                    }
                }

                // Restore yard load/unload controller state
                if( saveData.Yards != null )
                {
                    foreach( YardControllerSaveData yardData in saveData.Yards )
                    {
                        if( YardController.At(yardData.StationId) is YardController controller )
                        {
                            controller.ApplySaveData(yardData);
                        }
                    }
                }
            }

            IsLoadCompleted = true;
            DVIndustry.ModEntry.Logger.Log("Loaded industry save data");
        }

        public static void SaveIndustryData()
        {
            IndustrySaveData[] indData = IndustryController.AllControllers
                .Select(ind => ind.GetSaveData()).ToArray();

            YardControllerSaveData[] yardData = YardController.AllControllers
                .Select(yard => yard.GetSaveData()).ToArray();

            var industryData = new IndustrySaveDataCollection()
            {
                Industries = indData,
                Yards = yardData
            };

            SaveGameManager.data.SetObject(SAVE_KEY, industryData, serializerSettings);
        }
    }

    [HarmonyPatch(typeof(JobSaveManager), "LoadJobSaveGameData")]
    static class OnGameLoad_Patch
    {
        static void Postfix()
        {
            IndustrySaveDataManager.LoadIndustryData();
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    static class OnGameSave_Patch
    {
        // inject our save data before the IO is performed
        static void Prefix()
        {
            IndustrySaveDataManager.SaveIndustryData();
        }
    }
}
