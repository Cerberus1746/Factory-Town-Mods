using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityModManagerNet;
using System;
using System.IO;

namespace RatioCalculatorMod {
    

    class AssetManager : MonoBehaviour {
        string assetPath = "";

        public AssetManager(UnityModManager.ModEntry modEntry) {
            if (this.assetPath == "") {
                this.assetPath = "D:\\Program Files (x86)\\Steam\\steamapps\\common\\Factory Town\\Factory Town_Data\\StreamingAssets\\AssetBundles\\ui";
                var loadedAssetBundle = AssetBundle.LoadFromFile(assetPath);
                if (loadedAssetBundle == null) {
                    modEntry.Logger.Error("Failed to load AssetBundles! " + assetPath);
                } else {
                    modEntry.Logger.Log("Succesfully loaded AssetBundles!");
                    GameObject asset = loadedAssetBundle.LoadAsset<GameObject>("ModUI");
                    Instantiate(asset);
                }
            }
        }
    }

    static class Main {
        static bool Load(UnityModManager.ModEntry modEntry) {
            //AssetManager ratioCalculator = new AssetManager(modEntry);
            
            return true;
        }
    }
}