using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HappinessSetter {
    public class HappinessSetter : MonoBehaviour {
        public static int currentHappiness;
        public static bool isSetterActive;
        public static float originalValue;

        // Token: 0x04000004 RID: 4
        private int modifier = 1;
        public void OnUpdate(UnityModManager.ModEntry modEntry, float dt) {
            bool keyDown = Input.GetKeyDown(117);
            bool keyDown2 = Input.GetKeyDown(61);
            bool keyDown3 = Input.GetKeyDown(45);
            if(HappinessSetter.isSetterActive) {
                if(Input.GetKey(304) || Input.GetKey(303)) {
                    this.modifier = 100;
                } else if(Input.GetKey(306) || Input.GetKey(305)) {
                    this.modifier = 10;
                } else {
                    this.modifier = 1;
                }
                if(keyDown) {
                    Menu.Instance.playerMessagePanel.ShowImmediateMessage("Setter is NOT active");
                    GameManager.Instance.happinessProductionModifier = HappinessSetter.originalValue;
                    HappinessSetter.isSetterActive = false;
                } else if(keyDown2) {
                    Menu.Instance.playerMessagePanel.ShowImmediateMessage("Happiness Added");
                    HappinessSetter.currentHappiness += this.modifier;
                } else if(keyDown3) {
                    Menu.Instance.playerMessagePanel.ShowImmediateMessage("Happiness Reduced");
                    HappinessSetter.currentHappiness -= this.modifier;
                }
            } else if(!HappinessSetter.isSetterActive && keyDown) {
                Menu.Instance.playerMessagePanel.ShowImmediateMessage("Setter is active");
                GameManager.Instance.happinessProductionModifier = HappinessSetter.originalValue;
                HappinessSetter.isSetterActive = true;
            }
            if(keyDown || keyDown2 || keyDown3) {
                GameManager.Instance.isHappinessStale = true;
                GameManager.Instance.CalcHappinessMetadata();
            }
        }
    }

    [HarmonyPatch(typeof(GameManager))]
    [HarmonyPatch("CalculateHappinessModifier")]
    internal class HappinessProductionModifier_Patch {
        // Token: 0x06000004 RID: 4 RVA: 0x000021A9 File Offset: 0x000003A9
        private static void Prefix(ref GameManager __instance) {
            if(HappinessSetter.isSetterActive) {
                __instance.happinessProvided = HappinessSetter.currentHappiness;
            }
        }
    }

    internal static class Main {
        private static bool Load(UnityModManager.ModEntry modEntry) {
            HarmonyInstance.Create(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
            HappinessSetter @object = new HappinessSetter();
            modEntry.OnUpdate = new Action<UnityModManager.ModEntry, float>(@object.OnUpdate);
            return true;
        }
    }
}