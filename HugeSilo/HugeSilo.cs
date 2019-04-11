using System;
using System.Reflection;
using System.Collections.Generic;

using UnityModManagerNet;
using Harmony;
using UnityEngine;

namespace HugeSilo {
    public class BuildingHugeSilo {
        public static readonly int invSize = int.MaxValue;
        public static readonly string verboseName = "Huge Silo";
        public static readonly string verboseCategory = "Modded Items";
        public static readonly BuildingType buildingType = (BuildingType) verboseName.GetHashCode();
        public static readonly BuildingType originalVanillaBuilding = BuildingType.Silo;
        public static readonly BuildCategoryType buildCategoryType = (BuildCategoryType) verboseCategory.GetHashCode();
        public static readonly BuildingDef buildingDef = new BuildingDef {
            isStorage = true,
            usesSharedInventory = false,
            footprintOverride = 1,
            hasItemFilter = true
        };

        public static UnityModManager.ModEntry modEntry;

        public BuildingHugeSilo(UnityModManager.ModEntry modEntry) {
            BuildingHugeSilo.modEntry = modEntry;

            HarmonyInstance harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }


    [HarmonyPatch(typeof(Building))]
    [HarmonyPatch("PrefabForBuilding")]
    [HarmonyPatch(new Type[] { typeof(BuildingType) })]
    static class PrefabForBuilding_Patch {
        public static bool Prefix(BuildingType type, ref GameObject __result) {
            if(type == BuildingHugeSilo.buildingType) {
                __result = PrefabManager.Instance.silo;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Building))]
    [HarmonyPatch("HasDynamicInventory")]
    [HarmonyPatch(new Type[] { typeof(BuildingType) })]
    static class HasDynamicInventory_Patch {
        public static bool Prefix(BuildingType type, ref bool __result) {
            if(type == BuildingHugeSilo.buildingType) {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Building))]
    [HarmonyPatch("InitializeFixedItemSlots")]
    static class InitializeFixedItemSlots_Patch {
        public static void Postfix(ref Building __instance) {
            if(__instance.type == BuildingHugeSilo.buildingType) {
                __instance.inventory.CreateStorageSlots(1, BuildingHugeSilo.invSize);
            }
        }
    }

    [HarmonyPatch(typeof(LocalizationManager))]
    [HarmonyPatch("ReadMasterLocalizationFile")]
    static class ReadMasterLocalizationFile_Patch {
        public static void Postfix(ref LocalizationManager __instance) {
            string buildingName = TextDisplay.LabelForBuilding(BuildingHugeSilo.buildingType);
            string categoryName = TextDisplay.LabelForBuildCategoryType(BuildingHugeSilo.buildCategoryType);

            Dictionary<string, string> englishStrs = (Dictionary<string, string>) AccessTools.Field(typeof(LocalizationManager), "englishStrings").GetValue(__instance);
            Dictionary<string, string> locStrs = (Dictionary<string, string>) AccessTools.Field(typeof(LocalizationManager), "localizedStrings").GetValue(__instance);

            englishStrs.Add(buildingName, BuildingHugeSilo.verboseName);
            locStrs.Add(buildingName, BuildingHugeSilo.verboseName);

            englishStrs.Add(categoryName, BuildingHugeSilo.verboseCategory);
            locStrs.Add(categoryName, BuildingHugeSilo.verboseCategory);
        }
    }

    [HarmonyPatch(typeof(BuildMenu))]
    [HarmonyPatch("LoadCategories")]
    static class AddCategory_Patch {
        public static void Postfix(ref BuildMenu __instance) {
            BuildCategoryType customCategory = BuildingHugeSilo.buildCategoryType;

            MethodInfo AddCategory = AccessTools.Method(typeof(BuildMenu), "AddCategory");
            AddCategory.Invoke(__instance, new object[] { customCategory });
        }
    }

    [HarmonyPatch(typeof(BuildMenu))]
    [HarmonyPatch("ItemsInCategory")]
    [HarmonyPatch(new Type[] { typeof(BuildCategoryType) })]
    static class ItemsInCategory_Patch {
        public static void Postfix(BuildCategoryType type, ref List<object> __result) {
            if(type == BuildingHugeSilo.buildCategoryType) {
                __result.Add(BuildingHugeSilo.buildingType);
            }
        }
    }

    [HarmonyPatch(typeof(Data))]
    [HarmonyPatch("LoadObjectDefs")]
    static class LoadObjectDefs_Patch {
        public static void Postfix(ref Data __instance) {
            __instance.buildingDefinitions.Add(BuildingHugeSilo.buildingType, BuildingHugeSilo.buildingDef);
        }
    }

    static class Main {
        public static bool Load(UnityModManager.ModEntry modEntry) {
            BuildingHugeSilo hugeSilo = new BuildingHugeSilo(modEntry);
            return true;
        }
    }
}
