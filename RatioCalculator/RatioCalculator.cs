using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace RatioCalculatorMod {
    class RatioCalculator : MonoBehaviour {
        public void OnUpdate(UnityModManager.ModEntry modEntry, float dt) {
            if (Input.GetKeyDown(KeyCode.F7)) {
                if (CustomCursor.Instance.selectedTarget.isAssigned) {
                    Building currentBuilding = CustomCursor.Instance.selectedTarget.building;
                    Dictionary<string, float> outputList = new Dictionary<string, float>();
                    Dictionary<string, float> inputList = new Dictionary<string, float>();

                    float modifier = currentBuilding.productionModifierFinal + 1;
                    StringBuilder outputString = new StringBuilder($"Production Modifier: {modifier.ToString()}\n");

                    foreach (RecipeInstance currentRecipe in currentBuilding.currentlyAssignedRecipes) {
                        float currentCycle = 1 / (currentRecipe.baselineProductionTime / modifier);

                        foreach (ItemCount currentOutput in currentRecipe.outputs) {
                            string itemName = currentOutput.serializedItemType;
                            if (outputList.ContainsKey(itemName)) {
                                outputList[itemName] += currentCycle;

                            } else {
                                outputList.Add(itemName, currentCycle);
                            }
                        }

                        foreach (ItemCount currentInput in currentRecipe.inputs) {
                            string itemName = currentInput.serializedItemType;
                            float inputsPerSecond = currentInput.count * currentCycle;
                            if (inputList.ContainsKey(itemName)) {
                                inputList[itemName] += inputsPerSecond;

                            } else {
                                inputList.Add(itemName, inputsPerSecond);
                            }
                        }
                    }
                    outputString.Append("Outputs:\n");
                    foreach (KeyValuePair<string, float> currentOutput in outputList) {
                        outputString.Append($" per second {currentOutput.Value}\n");
                    }
                    outputString.Append("\n");
                    outputString.Append("Inputs:\n");
                    foreach (KeyValuePair<string, float> currentInput in inputList) {
                        outputString.Append($"{currentInput.Key} per second {currentInput.Value}\n");
                    }

                    Menu.Instance.playerMessagePanel.ShowImmediateMessage(outputString.ToString());
                }
            }
        }
    }

    static class Main {
        static bool Load(UnityModManager.ModEntry modEntry) {
            RatioCalculator ratioCalculator = new RatioCalculator();

            modEntry.OnUpdate = ratioCalculator.OnUpdate;
            return true;
        }
    }
}