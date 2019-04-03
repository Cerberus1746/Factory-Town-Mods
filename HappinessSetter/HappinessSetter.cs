using UnityEngine;
using UnityModManagerNet;
using Harmony;
using System.Reflection;

namespace HappinessSetter
{
    public class HappinessSetter : MonoBehaviour
    {
        public static float currentHappiness = 0.0f;
        public void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (Input.GetKeyDown(KeyCode.Equals))
            {

                HappinessSetter.currentHappiness += 0.1f;
                GameManager.Instance.CalculateHappiness();
            }
            else if (Input.GetKeyDown(KeyCode.Minus))
            {
                HappinessSetter.currentHappiness -= 0.1f;
                GameManager.Instance.CalculateHappiness();
            }
            
        }
    }

    [HarmonyPatch(typeof(GameManager))]
    [HarmonyPatch("CalculateHappinessModifier")]
    class HappinessProductionModifier_Patch
    {
        static void Postfix(ref GameManager __instance)
        {
            __instance.happinessProductionModifier = HappinessSetter.currentHappiness;
        }
    }

    static class Main
    {
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HappinessSetter ratioCalculator = new HappinessSetter();

            modEntry.OnUpdate = ratioCalculator.OnUpdate;
            return true;
        }
    }
}
