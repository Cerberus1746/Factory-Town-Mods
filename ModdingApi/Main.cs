using BepInEx;

namespace ModdingApi {
    [BepInPlugin("org.bepinex.plugins.exampleplugin", "Example Plug-In", "1.0.0.0")]
    public class ExamplePlugin : BaseUnityPlugin {
        // Awake is called once when both the game and the plug-in are loaded
        void Awake() {
            UnityEngine.Debug.Log("Hello, world!");
        }
    }
}