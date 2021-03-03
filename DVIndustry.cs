using System;
using System.Collections.Generic;
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

            bool success = IndustryConfigManager.LoadConfig();
            if( !success ) return false;

            var harmony = new Harmony("cc.foxden.dv_industry");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }
    }
}
