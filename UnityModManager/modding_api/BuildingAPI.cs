using System;
using UnityEngine;
using UnityModManagerNet;
using System.Collections.Generic;
using System.Reflection;
using Harmony;

namespace ModdingAPI {
    public class ModdedBuilding {
        public int invSize;

        public string verboseName;
        public string verboseCategory;

        public static BuildingType buildingType;
        public static BuildCategoryType buildCategoryType;

        public BuildingType originalVanillaBuilding;
        public BuildingDef buildingDef;

        public Type buildingApiType = typeof(ModdedBuilding);

        public UnityModManager.ModEntry modEntry;

        public MethodInfo[] methodAtributtes;
        public HarmonyInstance harmony;

        private void InitializeEnums() {
            buildingType = (BuildingType) verboseName.GetHashCode();
            buildCategoryType = (BuildCategoryType) verboseCategory.GetHashCode();
        }

        public void ExecuteHarmony() {
            modEntry.Logger.Log("Registered Building with name: " + verboseName);
            InitializeEnums();
            Type apiType = this.GetType();
            string outputStrDebug = "\n";

            foreach(MethodInfo currentMethod in apiType.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                object[] attrs = currentMethod.GetCustomAttributes(typeof(RegisterPatch), true);
                outputStrDebug += "Current Method: " + currentMethod + "\n";

                if(attrs.Length > 0) {
                    outputStrDebug += "Anottated: " + currentMethod;

                    foreach(RegisterPatch current_attr in attrs) {
                        MethodInfo original = current_attr.originalClass.GetMethod(current_attr.originalMethod);

                        outputStrDebug += "\n\tWith: " + current_attr;
                        outputStrDebug += "\n\t\tOriginal Method: " + current_attr.originalMethod;
                        outputStrDebug += "\n\t\tOriginal Class: " + current_attr.originalClass;
                        outputStrDebug += "\n\t\tIs Type: " + current_attr.patch_type;

                        if(current_attr.patch_type == "prefix") {
                            harmony.Patch(original, prefix: new HarmonyMethod(currentMethod));

                        } else if(current_attr.patch_type == "postfix") {
                            harmony.Patch(original, postfix: new HarmonyMethod(currentMethod));
                        }
                    }
                    outputStrDebug += "\n";
                }
            }
            modEntry.Logger.Log("Methods with attributes: " + outputStrDebug);
            
            //buildingMenu.GetMethod("LoadCategories")
        }

        [RegisterPatch("PrefabForBuilding", typeof(Building), "prefix")]
        public static bool PrefabPatchPrefix(BuildingType type, ref GameObject __result) {
            if(type == ModdedBuilding.buildingType) {
                __result = PrefabManager.Instance.silo;
                return false;
            }
            return true;
        }

        [RegisterPatch("ItemsInCategory", typeof(BuildMenu), "postfix")]
        public static void ItemsInCategoryPatch(BuildCategoryType type, ref List<object> __result) {
            if(type == ModdedBuilding.buildCategoryType) {
                __result.Add(ModdedBuilding.buildingType);
            }
        }
    }
}
