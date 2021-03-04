using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace DVIndustry
{
    public static class DVIndustry
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            string configPath = Path.Combine(ModEntry.Path, "industries.json");
            if( !IndustryConfigManager.LoadConfig(configPath) )
            {
                ModEntry.Logger.Critical("Industry configuration file not found");
                return false;
            }

            var harmony = new Harmony("cc.foxden.dv_industry");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }
    }
}
