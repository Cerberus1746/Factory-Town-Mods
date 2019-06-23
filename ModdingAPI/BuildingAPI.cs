using System;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Harmony;

namespace ModdingAPI {
    public class ModdedBuilding {
        public static readonly int invSize;

        public static string verboseName;
        public static string verboseCategory;
        public static readonly BuildingType buildingType = (BuildingType) verboseName.GetHashCode();


        public static readonly BuildingType originalVanillaBuilding;
        public static readonly BuildingDef buildingDef;
        public static readonly Type building_api_type = typeof(ModdedBuilding);

        public BuildingType ModBuildingType => verboseName.ToEnum<BuildingType>();
        public BuildCategoryType ModCategoryBuildingType => verboseCategory.ToEnum<BuildCategoryType>();

        public static ModdedBuilding Instance { get; private set ; }

        public static UnityModManager.ModEntry modEntry;

        public void CreateBuildingType() {

        }

        public void ExecuteHarmony() {
            
            //typeof(TheClass).GetMethod("TheMethod")
        }

        static class PrefabForBuilding_Patch {
            public static bool Prefix(BuildingType type, ref GameObject __result) {
                if(type == ModdedBuilding.buildingType) {
                    __result = PrefabManager.Instance.silo;
                    return false;
                }
                return true;
            }
        }
    }
}
