using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using UnityEngine;
using dnlib.DotNet;

namespace UnityModManagerNet {
    public partial class UnityModManager {
        private static readonly Version VER_0 = new Version();

        private static readonly Version VER_0_13 = new Version(0, 13);

        /// <summary>
        /// Contains version of UnityEngine
        /// </summary>
        public static Version UnityVersion { get; private set; }

        /// <summary>
        /// Contains version of a game, if configured [0.15.0]
        /// </summary>
        public static Version GameVersion { get; private set; } = new Version();

        /// <summary>
        /// Contains version of UMM
        /// </summary>
        public static Version Version { get; private set; } = typeof(UnityModManager).Assembly.GetName().Version;

        private static ModuleDefMD thisModuleDef = ModuleDefMD.Load(typeof(UnityModManager).Module);

        public class ModSettings {
            public virtual void Save(ModEntry modEntry) => Save(this, modEntry);

            public virtual string GetPath(ModEntry modEntry) => Path.Combine(modEntry.Path, "Settings.xml");

            public static void Save<T>(T data, ModEntry modEntry) where T : ModSettings, new() {
                string filepath = data.GetPath(modEntry);
                try {
                    using (StreamWriter writer = new StreamWriter(filepath)) {
                        XmlSerializer serializer = new XmlSerializer(typeof(T));
                        serializer.Serialize(writer, data);
                    }
                } catch (Exception e) {
                    modEntry.Logger.Error($"Can't save {filepath}.");
                    Debug.LogException(e);
                }
            }

            public static T Load<T>(ModEntry modEntry) where T : ModSettings, new() {
                T t = new T();
                string filepath = t.GetPath(modEntry);
                if (File.Exists(filepath)) {
                    try {
                        using (FileStream stream = File.OpenRead(filepath)) {
                            XmlSerializer serializer = new XmlSerializer(typeof(T));
                            T result = (T) serializer.Deserialize(stream);
                            return result;
                        }
                    } catch (Exception e) {
                        modEntry.Logger.Error($"Can't read {filepath}.");
                        Debug.LogException(e);
                    }
                }

                return t;
            }
        }

        public class ModInfo : IEquatable<ModInfo> {
            public string Id;

            public string DisplayName;

            public string Author;

            public string Version;

            public string ManagerVersion;

            public string GameVersion;

            public string[] Requirements;

            public string AssemblyName;

            public string EntryMethod;

            public string HomePage;

            public string Repository;

            public static implicit operator bool(ModInfo exists) {
                return exists != null;
            }

            public bool Equals(ModInfo other) => this.Id.Equals(other.Id);

            public override bool Equals(object obj) {
                if (obj is null) {
                    return false;
                }
                return obj is ModInfo modInfo && this.Equals(modInfo);
            }

            public override int GetHashCode() => this.Id.GetHashCode();
        }

        public partial class ModEntry {
            public readonly ModInfo Info;

            /// <summary>
            /// Path to mod folder
            /// </summary>
            public readonly string Path;

            Assembly mAssembly = null;
            public Assembly Assembly => this.mAssembly;

            /// <summary>
            /// Version of a mod
            /// </summary>
            public readonly Version Version = null;

            /// <summary>
            /// Required UMM version
            /// </summary>
            public readonly Version ManagerVersion = null;

            /// <summary>
            /// Required game version [0.15.0]
            /// </summary>
            public readonly Version GameVersion = null;

            /// <summary>
            /// Not used
            /// </summary>
            public Version NewestVersion;

            /// <summary>
            /// Required mods
            /// </summary>
            public readonly Dictionary<string, Version> Requirements = new Dictionary<string, Version>();

            /// <summary>
            /// Displayed in UMM UI. Add <color></color> tag to change colors. Can be used when custom verification game version [0.15.0]
            /// </summary>
            public string CustomRequirements = string.Empty;

            public readonly ModLogger Logger = null;

            /// <summary>
            /// Not used
            /// </summary>
            public bool HasUpdate = false;

            //public ModSettings Settings = null;

            /// <summary>
            /// Show button to reload the mod [0.14.0]
            /// </summary>
            public bool CanReload { get; private set; }

            /// <summary>
            /// Called to unload old data for reloading mod [0.14.0]
            /// </summary>
            public Func<ModEntry, bool> OnUnload = null;

            /// <summary>
            /// Called to activate / deactivate the mod
            /// </summary>
            public Func<ModEntry, bool, bool> OnToggle = null;

            /// <summary>
            /// Called by MonoBehaviour.OnGUI
            /// </summary>
            public Action<ModEntry> OnGUI = null;

            /// <summary>
            /// Called when the game closes
            /// </summary>
            public Action<ModEntry> OnSaveGUI = null;

            /// <summary>
            /// Called by MonoBehaviour.Update [0.13.0]
            /// </summary>
            public Action<ModEntry, float> OnUpdate = null;

            /// <summary>
            /// Called by MonoBehaviour.LateUpdate [0.13.0]
            /// </summary>
            public Action<ModEntry, float> OnLateUpdate = null;

            /// <summary>
            /// Called by MonoBehaviour.FixedUpdate [0.13.0]
            /// </summary>
            public Action<ModEntry, float> OnFixedUpdate = null;

            Dictionary<long, MethodInfo> mCache = new Dictionary<long, MethodInfo>();

            bool mStarted = false;
            public bool Started => this.mStarted;

            bool mErrorOnLoading = false;
            public bool ErrorOnLoading => this.mErrorOnLoading;

            /// <summary>
            /// UI checkbox
            /// </summary>
            public bool Enabled = true;
            //public bool Enabled => Enabled;

            /// <summary>
            /// If OnToggle exists
            /// </summary>
            public bool Toggleable => this.OnToggle != null;

            /// <summary>
            /// If Assembly is loaded [0.13.1]
            /// </summary>
            public bool Loaded => this.Assembly != null;

            bool mFirstLoading = true;

            bool mActive = false;
            public bool Active {
                get => this.mActive;
                set {
                    if (value && !this.Loaded) {
                        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        this.Load();
                        this.Logger.NativeLog($"Loading time {(stopwatch.ElapsedMilliseconds / 1000f):f2} s.");
                        return;
                    }

                    if (!this.mStarted || this.mErrorOnLoading) {
                        return;
                    }

                    try {
                        if (value) {
                            if (this.mActive) {
                                return;
                            }

                            if (this.OnToggle == null || this.OnToggle(this, true)) {
                                this.mActive = true;
                                this.Logger.Log($"Active.");
                            } else {
                                this.Logger.Log($"Unsuccessfully.");
                            }
                        } else {
                            if (!this.mActive) {
                                return;
                            }

                            if (this.OnToggle != null && this.OnToggle(this, false)) {
                                this.mActive = false;
                                this.Logger.Log($"Inactive.");
                            }
                        }
                    } catch (Exception e) {
                        this.Logger.Error("OnToggle: " + e.GetType().Name + " - " + e.Message);
                        Debug.LogException(e);
                    }
                }
            }

            public ModEntry(ModInfo info, string path) {
                this.Info = info;
                this.Path = path;
                this.Logger = new ModLogger(this.Info.Id);
                this.Version = ParseVersion(info.Version);
                this.ManagerVersion = !string.IsNullOrEmpty(info.ManagerVersion) ? ParseVersion(info.ManagerVersion) : new Version();
                this.GameVersion = !string.IsNullOrEmpty(info.GameVersion) ? ParseVersion(info.GameVersion) : new Version();

                if (info.Requirements != null && info.Requirements.Length > 0) {
                    Regex regex = new Regex(@"(.*)-(\d\.\d\.\d).*");
                    foreach (string id in info.Requirements) {
                        Match match = regex.Match(id);
                        if (match.Success) {
                            this.Requirements.Add(match.Groups[1].Value, ParseVersion(match.Groups[2].Value));
                            continue;
                        }
                        if (!this.Requirements.ContainsKey(id)) {
                            this.Requirements.Add(id, null);
                        }
                    }
                }
            }

            public bool Load() {
                if (this.Loaded) {
                    return !this.mErrorOnLoading;
                }

                this.mErrorOnLoading = false;

                this.Logger.Log($"Version '{this.Info.Version}'. Loading.");
                if (string.IsNullOrEmpty(this.Info.AssemblyName)) {
                    this.mErrorOnLoading = true;
                    this.Logger.Error($"{nameof(this.Info.AssemblyName)} is null.");
                }

                if (string.IsNullOrEmpty(this.Info.EntryMethod)) {
                    this.mErrorOnLoading = true;
                    this.Logger.Error($"{nameof(this.Info.EntryMethod)} is null.");
                }

                if (!string.IsNullOrEmpty(this.Info.ManagerVersion)) {
                    if (this.ManagerVersion > GetVersion()) {
                        this.mErrorOnLoading = true;
                        this.Logger.Error($"Mod Manager must be version '{this.Info.ManagerVersion}' or higher.");
                    }
                }

                if (!string.IsNullOrEmpty(this.Info.GameVersion)) {
                    if (UnityModManager.GameVersion != VER_0 && this.GameVersion > UnityModManager.GameVersion) {
                        this.mErrorOnLoading = true;
                        this.Logger.Error($"Game must be version '{this.Info.GameVersion}' or higher.");
                    }
                }

                if (this.Requirements.Count > 0) {
                    foreach (KeyValuePair<string, Version> item in this.Requirements) {
                        string id = item.Key;
                        ModEntry mod = FindMod(id);
                        if (mod == null) {
                            this.mErrorOnLoading = true;
                            this.Logger.Error($"Required mod '{id}' missing.");
                            continue;
                        } else if (item.Value != null && item.Value > mod.Version) {
                            this.mErrorOnLoading = true;
                            this.Logger.Error($"Required mod '{id}' must be version '{item.Value}' or higher.");
                            continue;
                        }

                        if (!mod.Active) {
                            mod.Enabled = true;
                            mod.Active = true;
                            if (!mod.Active) {
                                this.Logger.Log($"Required mod '{id}' inactive.");
                            }
                        }
                    }
                }

                if (this.mErrorOnLoading) {
                    return false;
                }

                string assemblyPath = System.IO.Path.Combine(this.Path, this.Info.AssemblyName);

                if (File.Exists(assemblyPath)) {
                    try {
                        string assemblyCachePath = assemblyPath;
                        bool cacheExists = false;

                        if (this.mFirstLoading) {
                            FileInfo fi = new FileInfo(assemblyPath);
                            ushort hash = (ushort) ((long) fi.LastWriteTimeUtc.GetHashCode() + UnityModManager.Version.GetHashCode() + this.ManagerVersion.GetHashCode()).GetHashCode();
                            assemblyCachePath = assemblyPath + $".{hash}.cache";
                            cacheExists = File.Exists(assemblyCachePath);

                            if (!cacheExists) {
                                foreach (string filepath in Directory.GetFiles(this.Path, "*.cache")) {
                                    try {
                                        File.Delete(filepath);
                                    } catch (Exception) {
                                    }
                                }
                            }
                        }

                        if (this.ManagerVersion >= VER_0_13) {
                            if (this.mFirstLoading) {
                                if (!cacheExists) {
                                    File.Copy(assemblyPath, assemblyCachePath, true);
                                }
                                this.mAssembly = Assembly.LoadFile(assemblyCachePath);

                                foreach (Type type in this.mAssembly.GetTypes()) {
                                    if (type.GetCustomAttributes(typeof(EnableReloadingAttribute), true).Any()) {
                                        this.CanReload = true;
                                        break;
                                    }
                                }
                            } else {
                                this.mAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
                            }
                        } else {
                            //var asmDef = AssemblyDefinition.ReadAssembly(assemblyPath);
                            //var modDef = asmDef.MainModule;
                            //if (modDef.TryGetTypeReference("UnityModManagerNet.UnityModManager", out var typeRef))
                            //{
                            //    var managerAsmRef = new AssemblyNameReference("UnityModManager", version);
                            //    if (typeRef.Scope is AssemblyNameReference asmNameRef)
                            //    {
                            //        typeRef.Scope = managerAsmRef;
                            //        modDef.AssemblyReferences.Add(managerAsmRef);
                            //        asmDef.Write(assemblyCachePath);
                            //    }
                            //}
                            if (!cacheExists) {
                                ModuleDefMD modDef = ModuleDefMD.Load(File.ReadAllBytes(assemblyPath));
                                foreach (TypeRef item in modDef.GetTypeRefs()) {
                                    if (item.FullName == "UnityModManagerNet.UnityModManager") {
                                        item.ResolutionScope = new AssemblyRefUser(thisModuleDef.Assembly);
                                    }
                                }
                                modDef.Write(assemblyCachePath);
                            }
                            this.mAssembly = Assembly.LoadFile(assemblyCachePath);
                        }

                        this.mFirstLoading = false;
                    } catch (Exception exception) {
                        this.mErrorOnLoading = true;
                        this.Logger.Error($"Error loading file '{assemblyPath}'.");
                        Debug.LogException(exception);
                        return false;
                    }

                    try {
                        object[] param = new object[] { this };
                        Type[] types = new Type[] { typeof(ModEntry) };
                        if (this.FindMethod(this.Info.EntryMethod, types, false) == null) {
                            param = null;
                            types = null;
                        }

                        if (!this.Invoke(this.Info.EntryMethod, out object result, param, types) || result != null && (bool) result == false) {
                            this.mErrorOnLoading = true;
                            this.Logger.Log($"Not loaded.");
                        }
                    } catch (Exception e) {
                        this.mErrorOnLoading = true;
                        this.Logger.Log(e.ToString());
                        return false;
                    }

                    this.mStarted = true;

                    if (!this.mErrorOnLoading) {
                        this.Active = true;
                        return true;
                    }
                } else {
                    this.mErrorOnLoading = true;
                    this.Logger.Error($"File '{assemblyPath}' not found.");
                }

                return false;
            }

            internal void Reload() {
                if (!this.mStarted || !this.CanReload) {
                    return;
                }

                try {
                    string assemblyPath = System.IO.Path.Combine(this.Path, this.Info.AssemblyName);
                    Assembly reflAssembly = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(assemblyPath));
                    if (reflAssembly.GetName().Version == this.Assembly.GetName().Version) {
                        this.Logger.Log("Reload is not needed. The version is exactly the same as the previous one.");
                        return;
                    }
                } catch (Exception e) {
                    this.Logger.Error(e.ToString());
                    return;
                }

                if (this.OnSaveGUI != null) {
                    this.OnSaveGUI.Invoke(this);
                }

                this.Logger.Log("Reloading...");

                if (this.Toggleable) {
                    this.Active = false;
                } else {
                    this.mActive = false;
                }

                try {
                    if (!this.Active && (this.OnUnload == null || this.OnUnload.Invoke(this))) {
                        this.mCache.Clear();
                        typeof(Harmony.Traverse).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new Harmony.AccessCache());
                        typeof(Harmony.Traverse).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new Harmony.AccessCache());

                        Assembly oldAssembly = this.Assembly;
                        this.mAssembly = null;
                        this.mStarted = false;
                        this.mErrorOnLoading = false;

                        this.OnToggle = null;
                        this.OnGUI = null;
                        this.OnSaveGUI = null;
                        this.OnUnload = null;
                        this.OnUpdate = null;
                        this.OnFixedUpdate = null;
                        this.OnLateUpdate = null;
                        this.CustomRequirements = null;

                        if (this.Load()) {
                            Type[] allTypes = oldAssembly.GetTypes();
                            foreach (Type type in allTypes) {
                                Type t = this.Assembly.GetType(type.FullName);
                                if (t != null) {
                                    foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                                        if (field.GetCustomAttributes(typeof(SaveOnReloadAttribute), true).Any()) {
                                            FieldInfo f = t.GetField(field.Name);
                                            if (f != null) {
                                                this.Logger.Log($"Copying field '{field.DeclaringType.Name}.{field.Name}'");
                                                try {
                                                    if (field.FieldType != f.FieldType) {
                                                        if (field.FieldType.IsEnum && f.FieldType.IsEnum) {
                                                            f.SetValue(null, Convert.ToInt32(field.GetValue(null)));
                                                        } else if (field.FieldType.IsClass && f.FieldType.IsClass) {
                                                            //f.SetValue(null, Convert.ChangeType(field.GetValue(null), f.FieldType));
                                                        } else if (field.FieldType.IsValueType && f.FieldType.IsValueType) {
                                                            //f.SetValue(null, Convert.ChangeType(field.GetValue(null), f.FieldType));
                                                        }
                                                    } else {
                                                        f.SetValue(null, field.GetValue(null));
                                                    }
                                                } catch (Exception ex) {
                                                    this.Logger.Error(ex.ToString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        return;
                    } else if (this.Active) {
                        this.Logger.Log("Must be deactivated.");
                    }
                } catch (Exception e) {
                    this.Logger.Error(e.ToString());
                }

                this.Logger.Log("Reloading canceled.");
            }

            public bool Invoke(string namespaceClassnameMethodname, out object result, object[] param = null, Type[] types = null) {
                result = null;
                try {
                    MethodInfo methodInfo = this.FindMethod(namespaceClassnameMethodname, types);
                    if (methodInfo != null) {
                        result = methodInfo.Invoke(null, param);
                        return true;
                    }
                } catch (Exception exception) {
                    this.Logger.Error($"Error trying to call '{namespaceClassnameMethodname}'.");
                    this.Logger.Error($"{exception.GetType().Name} - {exception.Message}");
                    Debug.LogException(exception);
                }

                return false;
            }

            MethodInfo FindMethod(string namespaceClassnameMethodname, Type[] types, bool showLog = true) {
                long key = namespaceClassnameMethodname.GetHashCode();
                if (types != null) {
                    foreach (Type val in types) {
                        key += val.GetHashCode();
                    }
                }

                if (!this.mCache.TryGetValue(key, out MethodInfo methodInfo)) {
                    if (this.mAssembly != null) {
                        string classString = null;
                        string methodString = null;
                        int pos = namespaceClassnameMethodname.LastIndexOf('.');
                        if (pos != -1) {
                            classString = namespaceClassnameMethodname.Substring(0, pos);
                            methodString = namespaceClassnameMethodname.Substring(pos + 1);
                        } else {
                            if (showLog) {
                                this.Logger.Error($"Function name error '{namespaceClassnameMethodname}'.");
                            }

                            goto Exit;
                        }
                        Type type = this.mAssembly.GetType(classString);
                        if (type != null) {
                            if (types == null) {
                                types = new Type[0];
                            }

                            methodInfo = type.GetMethod(methodString, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types, new ParameterModifier[0]);
                            if (methodInfo == null) {
                                if (showLog) {
                                    if (types.Length > 0) {
                                        this.Logger.Log($"Method '{namespaceClassnameMethodname}[{string.Join(", ", types.Select(x => x.Name).ToArray())}]' not found.");
                                    } else {
                                        this.Logger.Log($"Method '{namespaceClassnameMethodname}' not found.");
                                    }
                                }
                            }
                        } else {
                            if (showLog) {
                                this.Logger.Error($"Class '{classString}' not found.");
                            }
                        }
                    } else {
                        if (showLog) {
                            UnityModManager.Logger.Error($"Can't find method '{namespaceClassnameMethodname}'. Mod '{this.Info.Id}' is not loaded.");
                        }
                    }

                Exit:

                    this.mCache[key] = methodInfo;
                }

                return methodInfo;
            }
        }

        public static readonly List<ModEntry> modEntries = new List<ModEntry>();
        public static string ModsPath { get; private set; }

        internal static Param Params { get; set; } = new Param();
        internal static GameInfo Config { get; set; } = new GameInfo();

        internal static bool started;
        internal static bool initialized;

        public static void Main() => AppDomain.CurrentDomain.AssemblyLoad += OnLoad;

        static void OnLoad(object sender, AssemblyLoadEventArgs args) {
            if (args.LoadedAssembly.FullName == "Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") {
                AppDomain.CurrentDomain.AssemblyLoad -= OnLoad;
                Injector.Run(true);
            }
        }

        public static bool Initialize() {
            if (initialized) {
                return true;
            }

            initialized = true;

            Logger.Clear();

            Logger.Log($"Initialize. Version '{Version}'.");

            UnityVersion = ParseVersion(Application.unityVersion);

            Config = GameInfo.Load();
            if (Config == null) {
                return false;
            }

            Params = Param.Load();

            ModsPath = Path.Combine(Environment.CurrentDirectory, Config.ModsDirectory);

            if (!Directory.Exists(ModsPath)) {
                Directory.CreateDirectory(ModsPath);
            }

            //SceneManager.sceneLoaded += SceneManager_sceneLoaded; // Incompatible with Unity5

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            return true;
        }

        //private static void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        //{
        //    Logger.NativeLog($"Scene loaded: {scene.name} ({mode.ToString()})");
        //}

        static Assembly AddOddAssembly(ResolveEventArgs args) {
            string dllName = args.Name.Split(","[0])[0];

            string rootPath = Path.GetDirectoryName(typeof(UnityModManager).Assembly.Location);
            string filepath = Path.Combine(rootPath, $"{dllName}.dll");
            Logger.Log($"Attempting to add '{dllName}' from path: \n{filepath}");
            if (File.Exists(filepath)) {
                try {
                    Logger.Log($"Succesfully added: '{args.Name}'.\n");
                    return Assembly.LoadFile(filepath);
                } catch (Exception e) {
                    Logger.Error(e.ToString());
                }
            } else {
                Logger.Error($"File not Found! {filepath}");
            }
            return null;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null) {
                Logger.Log($"Succesfully added: '{args.Name}'.\n");
                return assembly;
            }

            return AddOddAssembly(args);
        }

        public static void Start() {
            try {
                _Start();
            } catch (Exception e) {
                Debug.LogException(e);
                OpenUnityFileLog();
            }
        }

        private static void _Start() {
            if (!Initialize()) {
                Logger.Log($"Cancel start due to an error.");
                OpenUnityFileLog();
                return;
            }
            if (started) {
                Logger.Log($"Cancel start. Already started.");
                return;
            }

            started = true;

            if (!string.IsNullOrEmpty(Config.GameVersionPoint)) {
                try {
                    Logger.Log($"Start parsing game version.");

                    if (Injector.TryParseEntryPoint(Config.GameVersionPoint, out string assembly, out string className, out string methodName, out _)) {
                        Assembly asm = Assembly.Load(assembly);
                        if (asm == null) {
                            Logger.Error($"File '{assembly}' not found.");
                            goto Next;
                        }
                        Type foundClass = asm.GetType(className);
                        if (foundClass == null) {
                            Logger.Error($"Class '{className}' not found.");
                            goto Next;
                        }
                        MethodInfo foundMethod = foundClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (foundMethod == null) {
                            FieldInfo foundField = foundClass.GetField(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            if (foundField != null) {
                                GameVersion = ParseVersion(foundField.GetValue(null).ToString());
                                Logger.Log($"Game version detected as '{GameVersion}'.");
                                goto Next;
                            }

                            UnityModManager.Logger.Error($"Method '{methodName}' not found.");
                            goto Next;
                        }

                        GameVersion = ParseVersion(foundMethod.Invoke(null, null).ToString());
                        Logger.Log($"Game version detected as '{GameVersion}'.");
                    }
                } catch (Exception e) {
                    Debug.LogException(e);
                    OpenUnityFileLog();
                }
            }

        Next:

            if (Directory.Exists(ModsPath)) {
                Logger.Log($"Parsing mods.");

                Dictionary<string, ModEntry> mods = new Dictionary<string, ModEntry>();

                int countMods = 0;

                foreach (string dir in Directory.GetDirectories(ModsPath)) {
                    string jsonPath = Path.Combine(dir, Config.ModInfo);
                    if (!File.Exists(Path.Combine(dir, Config.ModInfo))) {
                        jsonPath = Path.Combine(dir, Config.ModInfo.ToLower());
                    }
                    if (File.Exists(jsonPath)) {
                        countMods++;
                        Logger.Log($"Reading file '{jsonPath}'.");
                        try {
                            ModInfo modInfo = JsonUtility.FromJson<ModInfo>(File.ReadAllText(jsonPath));
                            if (string.IsNullOrEmpty(modInfo.Id)) {
                                Logger.Error($"Id is null.");
                                continue;
                            }
                            if (mods.ContainsKey(modInfo.Id)) {
                                Logger.Error($"Id '{modInfo.Id}' already uses another mod.");
                                continue;
                            }
                            if (string.IsNullOrEmpty(modInfo.AssemblyName)) {
                                modInfo.AssemblyName = modInfo.Id + ".dll";
                            }

                            ModEntry modEntry = new ModEntry(modInfo, dir + Path.DirectorySeparatorChar);
                            mods.Add(modInfo.Id, modEntry);
                        } catch (Exception exception) {
                            Logger.Error($"Error parsing file '{jsonPath}'.");
                            Debug.LogException(exception);
                        }
                    } else {
                        //Logger.Log($"File not found '{jsonPath}'.");
                    }
                }

                if (mods.Count > 0) {
                    Logger.Log($"Sorting mods.");
                    TopoSort(mods);

                    Params.ReadModParams();

                    Logger.Log($"Loading mods.");
                    foreach (ModEntry mod in modEntries) {
                        if (!mod.Enabled) {
                            mod.Logger.Log("To skip (disabled).");
                        } else {
                            mod.Active = true;
                        }
                    }
                }

                Logger.Log($"Finish. Found {countMods} mods. Successful loaded {modEntries.Count(x => !x.ErrorOnLoading)} mods.".ToUpper());
                Console.WriteLine();
                Console.WriteLine();
            }

            if (!UI.Load()) {
                Logger.Error($"Can't load UI.");
            }
        }

        private static void DFS(string id, Dictionary<string, ModEntry> mods) {
            if (modEntries.Any(m => m.Info.Id == id)) {
                return;
            }
            foreach (string req in mods[id].Requirements.Keys) {
                DFS(req, mods);
            }
            modEntries.Add(mods[id]);
        }

        private static void TopoSort(Dictionary<string, ModEntry> mods) {
            foreach (string id in mods.Keys) {
                DFS(id, mods);
            }
        }

        public static ModEntry FindMod(string id) => modEntries.FirstOrDefault(x => x.Info.Id == id);

        public static Version GetVersion() => Version;

        public static void SaveSettingsAndParams() {
            Params.Save();
            foreach (ModEntry mod in modEntries) {
                if (mod.Active && mod.OnSaveGUI != null) {
                    try {
                        mod.OnSaveGUI(mod);
                    } catch (Exception e) {
                        mod.Logger.Error("OnSaveGUI: " + e.GetType().Name + " - " + e.Message);
                        Debug.LogException(e);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Copies a value from an old assembly to a new one [0.14.0]
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SaveOnReloadAttribute : Attribute {
    }

    /// <summary>
    /// Allows reloading [0.14.1]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EnableReloadingAttribute : Attribute {
    }
}
