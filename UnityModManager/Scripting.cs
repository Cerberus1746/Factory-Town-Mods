using Jurassic;

namespace UnityModManagerNet {
    public partial class UnityModManager {
        private ScriptEngine engine;

        public void Load() {
            this.engine = new ScriptEngine();
            Logger.Log(this.engine.Evaluate<string>("1.5 + 2.3").ToString());
        }
    }
}