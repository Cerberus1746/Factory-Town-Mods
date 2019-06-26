using System;
using System.Reflection;

namespace ModdingAPI {
    public class ModdedBuilding {
        public static int invSize;

        public static string verboseName;
        public static string verboseCategory;

        public static BuildingType buildingType;
        public static BuildCategoryType buildCategoryType;

        public BuildingType originalVanillaBuilding;
        public static BuildingDef buildingDef;

        public Type buildingApiType = typeof(ModdedBuilding);

        public MethodInfo[] methodAtributtes;

        static private void InitializeEnums() {
            buildingType = (BuildingType) verboseName.GetHashCode();
            buildCategoryType = (BuildCategoryType) verboseCategory.GetHashCode();
        }

        public void ApplyPatches() {

        }
    }
}