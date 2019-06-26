using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using FullSerializer;
using HarmonyLib;

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
        public Harmony harmony;

        private void InitializeEnums() {
            buildingType = (BuildingType) verboseName.GetHashCode();
            buildCategoryType = (BuildCategoryType) verboseCategory.GetHashCode();
        }

        public void ExecuteHarmony() {
            InitializeEnums();

            Type apiType = this.GetType();
            string outputStrDebug = "Methods inside Type: " + apiType + "\n";
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

            MethodInfo[] methodList = apiType.GetMethods(flags);

            foreach(MethodInfo currentMethod in methodList) {
                outputStrDebug += "Current Method: " + currentMethod + "\n";
                object[] attrs = currentMethod.GetCustomAttributes(typeof(RegisterPatch), true);

                if(attrs.Length > 0) {
                    outputStrDebug += "Anottated: " + currentMethod;

                    foreach(RegisterPatch current_attr in attrs) {
                        MethodInfo originalMethod;
                        if(current_attr.parameters == null) {
                            originalMethod = current_attr.originalClass.GetMethod(current_attr.originalMethod, flags);
                        } else {
                            originalMethod = current_attr.originalClass.GetMethod(current_attr.originalMethod, flags, null, current_attr.parameters, null);
                        }
                        if(originalMethod == null) {
                            throw new NullReferenceException($"{current_attr.originalMethod} not found in {current_attr.originalClass}");
                        }

                        outputStrDebug += "\n\tWith: " + current_attr;
                        outputStrDebug += "\n\t\tOriginal Method: " + current_attr.originalMethod;
                        outputStrDebug += "\n\t\tOriginal Class: " + current_attr.originalClass;
                        outputStrDebug += "\n\t\tIs Type: " + current_attr.patch_type + "\n";
                        try {
                            if(current_attr.patch_type == "prefix") {
                                harmony.Patch(originalMethod, prefix: new HarmonyMethod(currentMethod));

                            } else if(current_attr.patch_type == "postfix") {
                                harmony.Patch(originalMethod, postfix: new HarmonyMethod(currentMethod));
                            } else if(current_attr.patch_type == "transpiler") {
                                harmony.Patch(originalMethod, transpiler: new HarmonyMethod(currentMethod));
                            }
                        } catch(PlatformNotSupportedException ex) {
                            throw new PlatformNotSupportedException($"Exception while loading {currentMethod}");
                        }
                        outputStrDebug += "Registered Patch: " + currentMethod + "\n\n";
                    }
                }
            }
        }

        /*[RegisterPatch("TryDeserialize", typeof(fsSerializer), "postfix", new Type[] {typeof(fsData), typeof(Type), typeof(object)})]
        public static void TryDeserializePatch(fsData data, Type storageType, object result) {
        }*/

        [RegisterPatch("LoadBuildObjectDictionary", typeof(BuildMenu), "prefix")]
        public static bool LoadBuildObjectDictionaryPatch(ref BuildMenu __instance) {
            __instance.buildCategories.Add(ModdedBuilding.buildCategoryType);
            return true;
        }

        [RegisterPatch("LoadCategories", typeof(BuildMenu), "postfix")]
        public static void LoadCategoriesPatch(ref BuildMenu __instance) {
            BuildCategoryType customCategory = ModdedBuilding.buildCategoryType;

            MethodInfo AddCategory = AccessTools.Method(typeof(BuildMenu), "AddCategory");
            AddCategory.Invoke(__instance, new object[] { customCategory });
        }

        [RegisterPatch("ItemsInCategory", typeof(BuildMenu), "postfix")]
        public static void ItemsInCategoryPatch(BuildCategoryType type, ref List<object> __result) {
            if(type == ModdedBuilding.buildCategoryType) {
                __result.Add(ModdedBuilding.buildingType);
            }
        }

        [RegisterPatch("PrefabForBuilding", typeof(Building), "prefix")]
        public static bool PrefabForBuildingPatch(BuildingType type, ref GameObject __result) {
            if(type == ModdedBuilding.buildingType) {
                __result = PrefabManager.Instance.silo;
                return false;
            }
            return true;
        }

        [RegisterPatch("HasDynamicInventory", typeof(Building), "prefix")]
        public static bool HasDynamicInventoryPatch(BuildingType type, ref bool __result) {
            if(type == ModdedBuilding.buildingType) {
                __result = false;
                return false;
            }
            return true;
        }

        [RegisterPatch("LoadFixedItemSlots", typeof(Building), "postfix")]
        public static void InitializeFixedItemSlotsPatch(ref Building __instance) {
            if(__instance.type == ModdedBuilding.buildingType) {
                __instance.inventory.CreateStorageSlots(1, ModdedBuilding.invSize);
            }
        }

        
        [RegisterPatch("RestoreBlockObjects", typeof(BlockData), "prefix")]
        public static bool RestoreBlockObjectsPatch(ref BlockData __instance) {
            return true;
        }

        [RegisterPatch("DoSerialize", typeof(GameSaveFileJson.CustomBlockDataConverter), "postfix")]
        public static void CustomBlockDataConverterDoSerializePatch(BlockData model, Dictionary<string, fsData> serialized) {
            if(model.building.type == ModdedBuilding.buildingType) {
                Dictionary<string, fsData> dataDict = serialized["b"].AsDictionary;
                dataDict["type"] = new fsData(ModdedBuilding.buildingType.ToString());
                serialized["b"] = new fsData(dataDict);
            }
        }

        [RegisterPatch("ReadMasterLocalizationFile", typeof(LocalizationManager), "prefix")]
        public static void ReadMasterLocalizationFilePatch(ref LocalizationManager __instance) {
            string buildingName = TextDisplay.LabelForBuilding(ModdedBuilding.buildingType);
            string categoryName = TextDisplay.LabelForBuildCategoryType(ModdedBuilding.buildCategoryType);

            Dictionary<string, string> englishStrs = (Dictionary<string, string>) AccessTools.Field(typeof(LocalizationManager), "englishStrings").GetValue(__instance);
            Dictionary<string, string> locStrs = (Dictionary<string, string>) AccessTools.Field(typeof(LocalizationManager), "localizedStrings").GetValue(__instance);

            englishStrs.Add(buildingName, ModdedBuilding.verboseName);
            locStrs.Add(buildingName, ModdedBuilding.verboseName);

            englishStrs.Add(categoryName, ModdedBuilding.verboseCategory);
            locStrs.Add(categoryName, ModdedBuilding.verboseCategory);
        }

        [RegisterPatch("LoadObjectDefs", typeof(Data), "postfix")]
        public static void LoadObjectDefsPatch(ref Data __instance) {
            __instance.buildingDefinitions.Add(ModdedBuilding.buildingType, ModdedBuilding.buildingDef);
        }
    }
}