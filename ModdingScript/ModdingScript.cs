using UnityEngine;
using UnityModManagerNet;
using Jurassic;

namespace ModdingScript
{
    public class ModdingScript : MonoBehaviour
    {
        readonly ScriptEngine engine = new ScriptEngine();

        public void Main(UnityModManager.ModEntry modEntry) => modEntry.Logger.Log(this.engine.Evaluate<string>("1.5 + 2.3").ToString());
    }


    static class Main
    {
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModdingScript ratioCalculator = new ModdingScript();
            ratioCalculator.Main(modEntry);

            return true;
        }
    }
}
