using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Harmony;

namespace UnityModManagerNet
{
    public partial class UnityModManager
    {
        public class UI : MonoBehaviour
        {
            internal static bool Load()
            {
                try
                {
                    new GameObject(typeof(UI).FullName, typeof(UI));

                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return false;
            }

            private static UI mInstance = null;

            public static UI Instance
            {
                get { return mInstance; }
            }

            public static GUIStyle window = null;
            public static GUIStyle h1 = null;
            public static GUIStyle h2 = null;
            public static GUIStyle bold = null;
            /// <summary>
            /// [0.13.1]
            /// </summary>
            public static GUIStyle button = null;
            private static GUIStyle settings = null;
            private static GUIStyle status = null;
            private static GUIStyle www = null;
            private static GUIStyle updates = null;

            private bool mFirstLaunched = false;
            private bool mInit = false;

            private bool mOpened = false;
            public bool Opened { get { return this.mOpened; } }

            private Rect mWindowRect = new Rect(0, 0, 0, 0);
            private Vector2 mWindowSize = Vector2.zero;
            private Vector2 mExpectedWindowSize = Vector2.zero;
            private Resolution mCurrentResolution;

            private float mUIScale = 1f;
            private float mExpectedUIScale = 1f;
            private bool mUIScaleChanged;

            public int globalFontSize = 13;

            private void Awake()
            {
                mInstance = this;
                DontDestroyOnLoad(this);
                this.mWindowSize = this.ClampWindowSize(new Vector2(Params.WindowWidth, Params.WindowHeight));
                this.mExpectedWindowSize = this.mWindowSize;
                this.mUIScale = Mathf.Clamp(Params.UIScale, 0.5f, 2f);
                this.mExpectedUIScale = this.mUIScale;
                Textures.Init();
                HarmonyInstance harmony = HarmonyInstance.Create("UnityModManager.UI");
                MethodInfo original = typeof(Screen).GetMethod("set_lockCursor");
                MethodInfo prefix = typeof(Screen_lockCursor_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(original, new HarmonyMethod(prefix));
            }

            private void Start()
            {
                this.CalculateWindowPos();
                if (string.IsNullOrEmpty(Config.UIStartingPoint))
                {
                    this.FirstLaunch();
                }
                if (Params.CheckUpdates == 1)
                {
                    CheckModUpdates();
                }
            }

            private void OnDestroy()
            {
                SaveSettingsAndParams();
                Logger.WriteBuffers();
            }

            private void Update()
            {
                float deltaTime = Time.deltaTime;
                foreach (ModEntry mod in modEntries)
                {
                    if (mod.Active && mod.OnUpdate != null)
                    {
                        try
                        {
                            mod.OnUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.Error("OnUpdate: " + e.GetType().Name + " - " + e.Message);
                            Debug.LogException(e);
                        }
                    }
                }

                bool toggle = false;
                
                switch (Params.ShortcutKeyId)
                {
                    default:
                        if (Input.GetKeyUp(KeyCode.F10) && (Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl)))
                        {
                            toggle = true;
                        }
                        break;
                    case 1:
                        if (Input.GetKeyUp(KeyCode.ScrollLock))
                        {
                            toggle = true;
                        }
                        break;
                    case 2:
                        if (Input.GetKeyUp(KeyCode.KeypadMultiply))
                        {
                            toggle = true;
                        }
                        break;
                    case 3:
                        if (Input.GetKeyUp(KeyCode.BackQuote))
                        {
                            toggle = true;
                        }
                        break;
                }

                if (toggle)
                {
                    this.ToggleWindow();
                }

                if (this.mOpened && Input.GetKey(KeyCode.Escape))
                {
                    this.ToggleWindow();
                }
            }

            private void FixedUpdate()
            {
                float deltaTime = Time.fixedDeltaTime;
                foreach (ModEntry mod in modEntries)
                {
                    if (mod.Active && mod.OnFixedUpdate != null)
                    {
                        try
                        {
                            mod.OnFixedUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.Error("OnFixedUpdate: " + e.GetType().Name + " - " + e.Message);
                            Debug.LogException(e);
                        }
                    }
                }
            }

            private void LateUpdate()
            {
                float deltaTime = Time.deltaTime;
                foreach (ModEntry mod in modEntries)
                {
                    if (mod.Active && mod.OnLateUpdate != null)
                    {
                        try
                        {
                            mod.OnLateUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.Error("OnLateUpdate: " + e.GetType().Name + " - " + e.Message);
                            Debug.LogException(e);
                        }
                    }
                }

                Logger.Watcher(deltaTime);
            }

            private void PrepareGUI()
            {
                window = new GUIStyle {
                    name = "umm window"
                };
                

                h1 = new GUIStyle {
                    name = "umm h1",
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                h1.normal.textColor = Color.white;

                h2 = new GUIStyle {
                    name = "umm h2",
                    fontStyle = FontStyle.Bold
                };

                bold = new GUIStyle(GUI.skin.label) {
                    name = "umm bold",
                    fontStyle = FontStyle.Bold
                };

                button = new GUIStyle(GUI.skin.button) {
                    name = "umm button"
                };

                settings = new GUIStyle {
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true
                };

                status = new GUIStyle {
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true
                };

                www = new GUIStyle {
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true
                };

                updates = new GUIStyle {
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true
                };

                bold.normal.textColor = Color.white;

                h2.normal.textColor = new Color(0.6f, 0.91f, 1f);
                h2.normal.textColor = new Color(0.6f, 0.91f, 1f);

                window.normal.background = Textures.Window;
                window.normal.background.wrapMode = TextureWrapMode.Repeat;
            }

            private void ScaleGUI()
            {
                GUI.skin.font = Font.CreateDynamicFontFromOSFont(new[] { "Arial" }, Scale(this.globalFontSize));
                GUI.skin.button.padding = new RectOffset(Scale(10), Scale(10), Scale(3), Scale(3));
                //GUI.skin.button.margin = RectOffset(Scale(4), Scale(2));

                GUI.skin.horizontalSlider.fixedHeight = Scale(12);
                GUI.skin.horizontalSlider.border = RectOffset(3, 0);
                GUI.skin.horizontalSlider.padding = RectOffset(Scale(-1), 0);
                GUI.skin.horizontalSlider.margin = RectOffset(Scale(4), Scale(8));

                GUI.skin.horizontalSliderThumb.fixedHeight = Scale(12);
                GUI.skin.horizontalSliderThumb.border = RectOffset(4, 0);
                GUI.skin.horizontalSliderThumb.padding = RectOffset(Scale(7), 0);
                GUI.skin.horizontalSliderThumb.margin = RectOffset(0);

                window.padding = RectOffset(Scale(5));
                h1.fontSize = Scale(16);
                h1.margin = RectOffset(Scale(0), Scale(5));
                h2.fontSize = Scale(13);
                h2.margin = RectOffset(Scale(0), Scale(3));
                button.fontSize = Scale(13);
                button.padding = RectOffset(Scale(30), Scale(5));

                int iconHeight = 28;
                settings.fixedWidth = Scale(24);
                settings.fixedHeight = Scale(iconHeight);
                status.fixedWidth = Scale(12);
                status.fixedHeight = Scale(iconHeight);
                www.fixedWidth = Scale(24);
                www.fixedHeight = Scale(iconHeight);
                updates.fixedWidth = Scale(26);
                updates.fixedHeight = Scale(iconHeight);

                this.mColumns.Clear();
                foreach (Column column in this.mOriginColumns)
                {
                    this.mColumns.Add(new Column { name = column.name, width = this.Scale(column.width), expand = column.expand, skip = column.skip });
                }
            }

            private void OnGUI()
            {
                if (!this.mInit)
                {
                    this.mInit = true;
                    this.PrepareGUI();
                    this.ScaleGUI();
                }

                if (this.mOpened)
                {
                    if (this.mCurrentResolution.width != Screen.currentResolution.width || this.mCurrentResolution.height != Screen.currentResolution.height)
                    {
                        this.mCurrentResolution = Screen.currentResolution;
                        this.CalculateWindowPos();
                    }
                    if (this.mUIScaleChanged)
                    {
                        this.mUIScaleChanged = false;
                        this.ScaleGUI();
                    }
                    Color backgroundColor = GUI.backgroundColor;
                    Color color = GUI.color;
                    GUI.backgroundColor = Color.white;
                    GUI.color = Color.white;
                    this.mWindowRect = GUILayout.Window(0, this.mWindowRect, this.WindowFunction, "", window, GUILayout.Height(this.mWindowSize.y));
                    GUI.backgroundColor = backgroundColor;
                    GUI.color = color;
                }
            }

            public int tabId = 0;
            public string[] tabs = { "Mods", "Logs", "Settings" };

            class Column
            {
                public string name;
                public float width;
                public bool expand = false;
                public bool skip = false;
            }

            private readonly List<Column> mOriginColumns = new List<Column>
            {
                new Column {name = "Name", width = 200, expand = true},
                new Column {name = "Version", width = 60},
                new Column {name = "Requirements", width = 150, expand = true},
                new Column {name = "On/Off", width = 50},
                new Column {name = "Status", width = 50}
            };
            private List<Column> mColumns = new List<Column>();

            private Vector2[] mScrollPosition = new Vector2[0];

            private int mShowModSettings = -1;

            public static int Scale(int value)
            {
                if (!Instance) {
                    return value;
                }

                return (int)(value * Instance.mUIScale);
            }

            private float Scale(float value)
            {
                if (!Instance) {
                    return value;
                }

                return value * this.mUIScale;
            }

            private void CalculateWindowPos()
            {
                this.mWindowSize = this.ClampWindowSize(this.mWindowSize);
                this.mWindowRect = new Rect((Screen.width - this.mWindowSize.x) / 2f, (Screen.height - this.mWindowSize.y) / 2f, 0, 0);
            }

            private Vector2 ClampWindowSize(Vector2 orig) => new Vector2(Mathf.Clamp(orig.x, Mathf.Min(960, Screen.width), Screen.width), Mathf.Clamp(orig.y, Mathf.Min(720, Screen.height), Screen.height));

            private void WindowFunction(int windowId)
            {
                if (Input.GetKey(KeyCode.LeftControl)) {
                    GUI.DragWindow(this.mWindowRect);
                }

                UnityAction buttons = () => { };

                GUILayout.Label("Mod Manager " + Version, h1);

                GUILayout.Space(3);
                int tab = this.tabId;
                tab = GUILayout.Toolbar(tab, this.tabs, button, GUILayout.ExpandWidth(false));
                if (tab != this.tabId)
                {
                    this.tabId = tab;
                }

                GUILayout.Space(5);

                if (this.mScrollPosition.Length != this.tabs.Length) {
                    this.mScrollPosition = new Vector2[this.tabs.Length];
                }

                this.DrawTab(this.tabId, ref buttons);

                GUILayout.FlexibleSpace();
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Close", button, GUILayout.ExpandWidth(false)))
                {
                    this.ToggleWindow();
                }

                if (GUILayout.Button("Save", button, GUILayout.ExpandWidth(false)))
                {
                    SaveSettingsAndParams();
                }

                buttons();
                GUILayout.EndHorizontal();
            }

            private void DrawTab(int tabId, ref UnityAction buttons)
            {
                GUILayoutOption minWidth = GUILayout.MinWidth(this.mWindowSize.x);

                switch (this.tabs[tabId])
                {
                    case "Mods":
                        {
                        this.mScrollPosition[tabId] = GUILayout.BeginScrollView(this.mScrollPosition[tabId], minWidth, GUILayout.ExpandHeight(false));

                        float amountWidth = this.mColumns.Where(x => !x.skip).Sum(x => x.width);
                        float expandWidth = this.mColumns.Where(x => x.expand && !x.skip).Sum(x => x.width);

                        List<ModEntry> mods = modEntries;
                        GUILayoutOption[] colWidth = this.mColumns.Select(x =>
                                x.expand
                                    ? GUILayout.Width(x.width / expandWidth * (this.mWindowSize.x - 60 + expandWidth - amountWidth))
                                    : GUILayout.Width(x.width)).ToArray();

                            GUILayout.BeginVertical("box");

                            GUILayout.BeginHorizontal("box");
                            for (int i = 0; i < this.mColumns.Count; i++)
                            {
                                if (this.mColumns[i].skip) {
                                continue;
                            }

                            GUILayout.Label(this.mColumns[i].name, colWidth[i]);
                            }
                            
                            GUILayout.EndHorizontal();

                            for (int i = 0, c = mods.Count; i < c; i++)
                            {
                                int col = -1;
                                GUILayout.BeginVertical("box");
                                GUILayout.BeginHorizontal();

                                GUILayout.BeginHorizontal(colWidth[++col]);
                                if (mods[i].OnGUI != null || mods[i].CanReload)
                                {
                                    if (GUILayout.Button(mods[i].Info.DisplayName, GUI.skin.label, GUILayout.ExpandWidth(true)))
                                    {
                                    this.mShowModSettings = (this.mShowModSettings == i) ? -1 : i;
                                    }

                                    if (GUILayout.Button(this.mShowModSettings == i ? Textures.SettingsActive : Textures.SettingsNormal, settings))
                                    {
                                    this.mShowModSettings = (this.mShowModSettings == i) ? -1 : i;
                                    }
                                }
                                else
                                {
                                    GUILayout.Label(mods[i].Info.DisplayName);
                                }

                                if (!string.IsNullOrEmpty(mods[i].Info.HomePage))
                                {
                                    GUILayout.Space(10);
                                    if (GUILayout.Button(Textures.WWW, www))
                                    {
                                        Application.OpenURL(mods[i].Info.HomePage);
                                    }
                                }

                                if (mods[i].NewestVersion != null)
                                {
                                    GUILayout.Space(10);
                                    GUILayout.Box(Textures.Updates, updates);
                                }

                                GUILayout.Space(20);

                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal(colWidth[++col]);
                                GUILayout.Label(mods[i].Info.Version, GUILayout.ExpandWidth(false));
                                //                            if (string.IsNullOrEmpty(mods[i].Info.Repository))
                                //                            {
                                //                                GUI.color = new Color32(255, 81, 83, 255);
                                //                                GUILayout.Label("*");
                                //                                GUI.color = Color.white;
                                //                            }
                                GUILayout.EndHorizontal();

                                if (mods[i].ManagerVersion > GetVersion())
                                {
                                    GUILayout.Label("<color=\"#CD5C5C\">Manager-" + mods[i].Info.ManagerVersion + "</color>", colWidth[++col]);
                                }
                                else if (GameVersion != VER_0 && mods[i].GameVersion > GameVersion)
                                {
                                    GUILayout.Label("<color=\"#CD5C5C\">Game-" + mods[i].Info.GameVersion + "</color>", colWidth[++col]);
                                }
                                else if (mods[i].Requirements.Count > 0)
                                {
                                    foreach (KeyValuePair<string, Version> item in mods[i].Requirements)
                                    {
                                    string id = item.Key;
                                    ModEntry mod = FindMod(id);
                                        GUILayout.Label(((mod == null || item.Value != null && item.Value > mod.Version || !mod.Active) && mods[i].Active) ? "<color=\"#CD5C5C\">" + id + "</color>" : id, colWidth[++col]);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(mods[i].CustomRequirements))
                                {
                                    GUILayout.Label(mods[i].CustomRequirements, colWidth[++col]);
                                }
                                else
                                {
                                    GUILayout.Label("-", colWidth[++col]);
                                }

                            bool action = mods[i].Enabled;
                                action = GUILayout.Toggle(action, "", colWidth[++col]);
                                if (action != mods[i].Enabled)
                                {
                                    mods[i].Enabled = action;
                                    if (mods[i].Toggleable) {
                                    mods[i].Active = action;
                                } else if (action && !mods[i].Loaded) {
                                    mods[i].Active = action;
                                }
                            }

                                if (mods[i].Active)
                                {
                                    GUILayout.Box(mods[i].Enabled ? Textures.StatusActive : Textures.StatusNeedRestart, status);
                                }
                                else
                                {
                                    GUILayout.Box(!mods[i].Enabled ? Textures.StatusInactive : Textures.StatusNeedRestart, status);
                                }

                                GUILayout.EndHorizontal();

                                if (this.mShowModSettings == i)
                                {
                                    if (mods[i].CanReload)
                                    {
                                        GUILayout.Label("Debug", h2);
                                        if (GUILayout.Button("Reload", button, GUILayout.ExpandWidth(false)))
                                        {
                                            mods[i].Reload();
                                        }
                                        GUILayout.Space(5);
                                    }
                                    if (mods[i].Active && mods[i].OnGUI != null)
                                    {
                                        GUILayout.Label("Options", h2);
                                        try
                                        {
                                            mods[i].OnGUI(mods[i]);
                                        }
                                        catch (Exception e)
                                        {
                                        this.mShowModSettings = -1;
                                            mods[i].Logger.Error("OnGUI: " + e.GetType().Name + " - " + e.Message);
                                            Debug.LogException(e);
                                        }
                                    }
                                }

                                GUILayout.EndVertical();
                            }

                            GUILayout.EndVertical();

                            GUILayout.EndScrollView();

                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.SettingsNormal, settings);
                            GUILayout.Space(3);
                            GUILayout.Label("Options", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.WWW, www);
                            GUILayout.Space(3);
                            GUILayout.Label("Home page", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.Updates, updates);
                            GUILayout.Space(3);
                            GUILayout.Label("Available update", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.StatusActive, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Active", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.StatusInactive, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Inactive", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.StatusNeedRestart, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Need restart", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Label("[CTRL + LClick]", bold, GUILayout.ExpandWidth(false));
                            GUILayout.Space(3);
                            GUILayout.Label("Drag window", GUILayout.ExpandWidth(false));
                            //                        GUILayout.Space(10);
                            //                        GUI.color = new Color32(255, 81, 83, 255);
                            //                        GUILayout.Label("*", bold, GUILayout.ExpandWidth(false));
                            //                        GUI.color = Color.white;
                            //                        GUILayout.Space(3);
                            //                        GUILayout.Label("Not support updates", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            if (GUI.changed)
                            {
                            }

                            break;
                        }

                    case "Logs":
                        {
                        this.mScrollPosition[tabId] = GUILayout.BeginScrollView(this.mScrollPosition[tabId], minWidth);

                            GUILayout.BeginVertical("box");

                            for (int c = Logger.history.Count, i = Mathf.Max(0, c - Logger.historyCapacity); i < c; i++)
                            {
                                GUILayout.Label(Logger.history[i]);
                            }

                            GUILayout.EndVertical();
                            GUILayout.EndScrollView();

                            buttons += delegate
                            {
                                if (GUILayout.Button("Clear", button, GUILayout.ExpandWidth(false)))
                                {
                                    Logger.Clear();
                                }
                                if (GUILayout.Button("Open detailed log", button, GUILayout.ExpandWidth(false)))
                                {
                                    OpenUnityFileLog();
                                }
                            };

                            break;
                        }

                    case "Settings":
                        {
                        this.mScrollPosition[tabId] = GUILayout.BeginScrollView(this.mScrollPosition[tabId], minWidth);

                            GUILayout.BeginVertical("box");

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Hotkey", GUILayout.ExpandWidth(false));
                            Params.ShortcutKeyId = GUILayout.Toolbar(Params.ShortcutKeyId, this.mHotkeyNames, GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Check updates", GUILayout.ExpandWidth(false));
                            Params.CheckUpdates = GUILayout.Toolbar(Params.CheckUpdates, this.mCheckUpdateStrings,
                                GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Show this window on startup", GUILayout.ExpandWidth(false));
                            Params.ShowOnStart = GUILayout.Toolbar(Params.ShowOnStart, this.mShowOnStartStrings,
                                GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginVertical("box");
                            GUILayout.Label("Window size", bold, GUILayout.ExpandWidth(false));
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Width ", GUILayout.ExpandWidth(false));
                        this.mExpectedWindowSize.x = GUILayout.HorizontalSlider(this.mExpectedWindowSize.x, Mathf.Min(Screen.width, 960), Screen.width, GUILayout.Width(200));
                            GUILayout.Label(" " + this.mExpectedWindowSize.x.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Height", GUILayout.ExpandWidth(false));
                        this.mExpectedWindowSize.y = GUILayout.HorizontalSlider(this.mExpectedWindowSize.y, Mathf.Min(Screen.height, 720), Screen.height, GUILayout.Width(200));
                            GUILayout.Label(" " + this.mExpectedWindowSize.y.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
                            {
                            this.mWindowSize.x = Mathf.Floor(this.mExpectedWindowSize.x) % 2 > 0 ? Mathf.Ceil(this.mExpectedWindowSize.x) : Mathf.Floor(this.mExpectedWindowSize.x);
                            this.mWindowSize.y = Mathf.Floor(this.mExpectedWindowSize.y) % 2 > 0 ? Mathf.Ceil(this.mExpectedWindowSize.y) : Mathf.Floor(this.mExpectedWindowSize.y);
                            this.CalculateWindowPos();
                                Params.WindowWidth = this.mWindowSize.x;
                                Params.WindowHeight = this.mWindowSize.y;
                            }
                            GUILayout.EndVertical();

                            GUILayout.Space(5);

                            GUILayout.BeginVertical("box");
                            GUILayout.Label("UI", bold, GUILayout.ExpandWidth(false));
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Scale", GUILayout.ExpandWidth(false), GUILayout.ExpandWidth(false));
                        this.mExpectedUIScale = GUILayout.HorizontalSlider(this.mExpectedUIScale, 0.5f, 2f, GUILayout.Width(200));
                            GUILayout.Label(" " + this.mExpectedUIScale.ToString("f2"), GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
                            {
                                if (this.mUIScale != this.mExpectedUIScale)
                                {
                                this.mUIScaleChanged = true;
                                this.mUIScale = this.mExpectedUIScale;
                                    Params.UIScale = this.mUIScale;
                                }
                            }
                            GUILayout.EndVertical();

                            GUILayout.EndVertical();
                            GUILayout.EndScrollView();

                            break;
                        }
                }
            }

            private readonly string[] mCheckUpdateStrings = { "Never", "Automatic" };
            
            private readonly string[] mShowOnStartStrings = { "No", "Yes" };

            private readonly string[] mHotkeyNames = { "CTRL+F10", "ScrollLock", "Num *", "~" };

            internal bool GameCursorLocked { get; set; }

            public void FirstLaunch()
            {
                if (this.mFirstLaunched || UnityModManager.Params.ShowOnStart == 0 && modEntries.All(x => !x.ErrorOnLoading)) {
                    return;
                }

                this.ToggleWindow(true);
            }

            public void ToggleWindow() => this.ToggleWindow(!this.mOpened);

            public void ToggleWindow(bool open)
            {
                if (open == this.mOpened) {
                    return;
                }

                if (open) {
                    this.mFirstLaunched = true;
                }

                try
                {
                    this.mOpened = open;
                    this.BlockGameUI(open);
                    //if (!open)
                    //    SaveSettingsAndParams();
                    if (open)
                    {
                        this.GameCursorLocked = Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
                        if (this.GameCursorLocked)
                        {
                            Cursor.visible = true;
                            Cursor.lockState = CursorLockMode.None;
                        }
                    }
                    else
                    {
                        if (this.GameCursorLocked)
                        {
                            Cursor.visible = false;
                            Cursor.lockState = CursorLockMode.Locked;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("ToggleWindow: " + e.GetType().Name + " - " + e.Message);
                    Debug.LogException(e);
                }
            }

            private GameObject mCanvas = null;

            private void BlockGameUI(bool value)
            {
                if (value)
                {
                    this.mCanvas = new GameObject("", typeof(Canvas), typeof(GraphicRaycaster));
                    this.mCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    this.mCanvas.GetComponent<Canvas>().sortingOrder = short.MaxValue;
                    DontDestroyOnLoad(this.mCanvas);
                    GameObject panel = new GameObject("", typeof(Image));
                    panel.transform.SetParent(this.mCanvas.transform);
                    panel.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
                    panel.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
                    panel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                    panel.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                }
                else
                {
                    if (this.mCanvas) {
                        Destroy(this.mCanvas);
                    }
                }
            }

            private static RectOffset RectOffset(int value) => new RectOffset(value, value, value, value);

            private static RectOffset RectOffset(int x, int y) => new RectOffset(x, x, y, y);
        }

        //        [HarmonyPatch(typeof(Screen), "lockCursor", MethodType.Setter)]
        static class Screen_lockCursor_Patch
        {
            static bool Prefix(bool value)
            {
                if (UI.Instance != null && UI.Instance.Opened)
                {
                    UI.Instance.GameCursorLocked = value;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    return false;
                }

                return true;
            }
        }

    }
}

