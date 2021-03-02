using System;
using System.Collections.Generic;
using System.Linq;
using UnityModManagerNet;

namespace DVIndustry
{
    public static class DVIndustry
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            

            return true;
        }
    }
}
