using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityModManagerNet;
using System;
using System.IO;

namespace RatioCalculatorMod
{
    class RatioCalculator : MonoBehaviour
    {
        string assetPath = "";

        public void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            /*if (assetPath == "")
            {
                this.assetPath = "D:\\Program Files (x86)\\Steam\\steamapps\\common\\Factory Town\\Factory Town_Data\\StreamingAssets\\AssetBundles\\ui";
                var loadedAssetBundle = AssetBundle.LoadFromFile(assetPath);
                if (loadedAssetBundle == null)
                {
                    modEntry.Logger.Log("Failed to load AssetBundles! " + assetPath);
                } else {
                    modEntry.Logger.Log("Succesfully loaded AssetBundles!");
                    GameObject asset = loadedAssetBundle.LoadAsset<GameObject>("ModUI");
                    Instantiate(asset);
                }
            }*/
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (CustomCursor.Instance.selectedTarget.isAssigned)
                {
                    Building currentBuilding = CustomCursor.Instance.selectedTarget.building;
                    Dictionary<string, float> outputList = new Dictionary<string, float>();
                    Dictionary<string, float> inputList = new Dictionary<string, float>();

                    float modifier = currentBuilding.productionModifierFinal + 1;
                    string outputString = "Production Modifier: " + modifier.ToString() + "\n";

                    foreach (RecipeInstance currentRecipe in currentBuilding.currentlyAssignedRecipes)
                    {
                        float currentCycle = 1 / (currentRecipe.baselineProductionTime / modifier);

                        foreach (ItemCount currentOutput in currentRecipe.outputs)
                        {
                            string itemName = currentOutput.serializedItemType;
                            if (outputList.ContainsKey(itemName))
                            {
                                outputList[itemName] += currentCycle;

                            } else
                            {
                                outputList.Add(itemName, currentCycle);
                            }
                        }

                        foreach (ItemCount currentInput in currentRecipe.inputs)
                        {
                            string itemName = currentInput.serializedItemType;
                            float inputsPerSecond = currentInput.count * currentCycle;
                            if (inputList.ContainsKey(itemName))
                            {
                                inputList[itemName] += inputsPerSecond;

                            } else
                            {
                                inputList.Add(itemName, inputsPerSecond);
                            }
                        }
                    }
                    outputString += "Outputs:\n";
                    foreach (KeyValuePair<string, float> currentOutput in outputList)
                    {
                        outputString += currentOutput.Key + " per second " + currentOutput.Value + "\n";
                    }
                    outputString += "\n";
                    outputString += "Inputs:\n";
                    foreach (KeyValuePair<string, float> currentInput in inputList)
                    {
                        outputString += currentInput.Key + " per second " + currentInput.Value + "\n";
                    }

                    Menu.Instance.playerMessagePanel.ShowImmediateMessage(outputString);
                }
            }
        }
    }

    static class Main
    {
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            RatioCalculator ratioCalculator = new RatioCalculator();

            modEntry.OnUpdate = ratioCalculator.OnUpdate;
            return true;
        }
    }
}