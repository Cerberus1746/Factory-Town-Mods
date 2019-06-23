using UnityModManagerNet;
using ModdingAPI;
using Harmony;

namespace HugeSilo {
    public class BuildingHugeSilo: ModdedBuilding {
        public BuildingHugeSilo(UnityModManager.ModEntry modEntry) {
            this.invSize = int.MaxValue;
            this.verboseName = "HugeSilo";
            this.verboseCategory = "Modded";
            this.buildingDef = new BuildingDef {
                isStorage = true,
                usesSharedInventory = false,
                footprintOverride = 1,
                hasItemFilter = true
            };

            this.modEntry = modEntry;
            this.harmony = HarmonyInstance.Create(modEntry.Info.Id); ;
            modEntry.Logger.Log("Initializing Huge Silo Class.");
            this.ExecuteHarmony();
        }
    }
    static class Main {
        public static BuildingHugeSilo hugeSilo;
        public static bool Load(UnityModManager.ModEntry modEntry) {
            modEntry.Logger.Log("Initializing Huge Silo Mod.");
            Main.hugeSilo = new BuildingHugeSilo(modEntry);

            return true;
        }
    }
}
