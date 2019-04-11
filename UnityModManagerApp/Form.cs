using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace UnityModManagerNet.Installer
{
    [Serializable]
    public partial class UnityModManagerForm : Form
    {
        const string REG_PATH = @"HKEY_CURRENT_USER\Software\UnityModManager";

        public UnityModManagerForm()
        {
            this.InitializeComponent();
            this.Init();
            this.InitPageMods();
        }

        static readonly string[] libraryFiles = new string[]
        {
            "0Harmony.dll",
            "dnlib.dll",
            "Jurassic.dll",

            //"System.Xml.dll",
            nameof(UnityModManager) + ".dll",
            nameof(UnityModManager) + ".xml"
        };

        static string[] libraryPaths;

        public static UnityModManagerForm instance = null;

        static Config config = null;
        static Param param = null;
        static Version version = null;

        static string gamePath = null;
        static string managedPath = null;
        static string managerPath = null;
        static string entryAssemblyPath = null;
        static string injectedEntryAssemblyPath = null;
        static string managerAssemblyPath = null;
        static string entryPoint = null;
        static string injectedEntryPoint = null;

        static string gameExePath = null;

        static string doorstopFilename = "winhttp.dll";
        static string doorstopConfigFilename = "doorstop_config.ini";
        static string doorstopPath = null;
        static string doorstopConfigPath = null;

        static ModuleDefMD assemblyDef = null;
        static ModuleDefMD injectedAssemblyDef = null;
        static ModuleDefMD managerDef = null;

        //static string machineConfigPath = null;
        //static XDocument machineDoc = null;

        GameInfo selectedGame => (GameInfo) this.gameList.SelectedItem;
        Param.GameParam selectedGameParams = null;
        ModInfo selectedMod => this.listMods.SelectedItems.Count > 0 ? this.mods.Find(x => x.DisplayName == this.listMods.SelectedItems[0].Text) : null;

        private void Init()
        {
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            instance = this;

            Log.Init();

            if (!Utils.IsUnixPlatform())
            {
                foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type registry = asm.GetType("Microsoft.Win32.Registry");
                    if (registry != null)
                    {
                        System.Reflection.MethodInfo getValue = registry.GetMethod("GetValue", new Type[] { typeof(string), typeof(string), typeof(object) });
                        if (getValue != null)
                        {
                            string exePath = getValue.Invoke(null, new object[] { REG_PATH, "ExePath", string.Empty }) as string;
                            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                            {
                                System.Reflection.MethodInfo setValue = registry.GetMethod("SetValue", new Type[] { typeof(string), typeof(string), typeof(object) });
                                if (setValue != null)
                                {
                                    setValue.Invoke(null, new object[] { REG_PATH, "ExePath", Path.Combine(Application.StartupPath, "UnityModManager.exe") });
                                    setValue.Invoke(null, new object[] { REG_PATH, "Path", Application.StartupPath });
                                }
                            }
                        }
                        break;
                    }
                }
            }

            for (InstallType i = (InstallType)0; i < InstallType.Count; i++)
            {
                RadioButton btn = new RadioButton();
                btn.Name = i.ToString();
                btn.Text = i.ToString();
                btn.Dock = DockStyle.Left;
                btn.AutoSize = true;
                btn.Click += this.installType_Click;
                this.installTypeGroup.Controls.Add(btn);
            }

            version = typeof(UnityModManager).Assembly.GetName().Version;
            this.currentVersion.Text = version.ToString();

            config = Config.Load();
            param = Param.Load();

            if (config != null && config.GameInfo != null && config.GameInfo.Length > 0)
            {
                config.GameInfo = config.GameInfo.OrderBy(x => x.Name).ToArray();
                this.gameList.Items.AddRange(config.GameInfo);

                GameInfo selected = null;
                if (!string.IsNullOrEmpty(param.LastSelectedGame))
                {
                    selected = config.GameInfo.FirstOrDefault(x => x.Name == param.LastSelectedGame);
                }
                selected = selected ?? config.GameInfo.First();
                this.gameList.SelectedItem = selected;
                this.selectedGameParams = param.GetGameParam(selected);
            }
            else
            {
                this.InactiveForm();
                Log.Print($"Error parsing file '{Config.filename}'.");
                return;
            }

            this.CheckLastVersion();
        }

        private void installType_Click(object sender, EventArgs e)
        {
            RadioButton btn = (sender as RadioButton);
            if (!btn.Checked) {
                return;
            }

            this.selectedGameParams.InstallType = (InstallType)Enum.Parse(typeof(InstallType), btn.Name);

            this.RefreshForm();
        }

        private void UnityModLoaderForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Properties.Settings.Default.Save();
            param.Sync(config.GameInfo);
            param.Save();
        }

        private void InactiveForm()
        {
            this.btnInstall.Enabled = false;
            this.btnRemove.Enabled = false;
            this.btnRestore.Enabled = false;
            this.tabControl.TabPages[1].Enabled = false;
            this.installedVersion.Text = "-";

            foreach (object ctrl in this.installTypeGroup.Controls)
            {
                if (ctrl is RadioButton btn)
                {
                    btn.Enabled = false;
                }
            }
        }

        private bool IsValid(GameInfo gameInfo)
        {
            if (this.selectedGame == null)
            {
                Log.Print("Select a game.");
                return false;
            }

            List<string> ignoreFields = new List<string>
            {
                nameof(GameInfo.GameExe),
                nameof(GameInfo.StartingPoint),
                nameof(GameInfo.UIStartingPoint),
                nameof(GameInfo.OldPatchTarget),
                nameof(GameInfo.GameVersionPoint)
            };

            string prefix = (!string.IsNullOrEmpty(gameInfo.Name) ? $"[{gameInfo.Name}]" : "[?]");
            bool hasError = false;
            foreach (System.Reflection.FieldInfo field in typeof(GameInfo).GetFields())
            {
                if (!field.IsStatic && field.IsPublic && !ignoreFields.Exists(x => x == field.Name))
                {
                    object value = field.GetValue(gameInfo);
                    if (value == null || value.ToString() == "")
                    {
                        hasError = true;
                        Log.Print($"{prefix} Field '{field.Name}' is empty.");
                    }
                }
            }

            if (hasError)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(gameInfo.EntryPoint)) {
                if (!Utils.TryParseEntryPoint(gameInfo.EntryPoint, out _))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(gameInfo.StartingPoint)) {
                if (!Utils.TryParseEntryPoint(gameInfo.StartingPoint, out _))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(gameInfo.UIStartingPoint)) {
                if (!Utils.TryParseEntryPoint(gameInfo.UIStartingPoint, out _))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(gameInfo.OldPatchTarget)) {
                if (!Utils.TryParseEntryPoint(gameInfo.OldPatchTarget, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshForm()
        {
            if (!this.IsValid(this.selectedGame))
            {
                this.InactiveForm();
                return;
            }

            this.btnInstall.Text = "Install";
            this.btnRestore.Enabled = false;

            gamePath = "";
            if (string.IsNullOrEmpty(this.selectedGameParams.Path) || !Directory.Exists(this.selectedGameParams.Path))
            {
                string result = this.FindGameFolder(this.selectedGame.Folder);
                if (string.IsNullOrEmpty(result))
                {
                    this.InactiveForm();
                    this.btnOpenFolder.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
                    this.btnOpenFolder.Text = "Select Game Folder";
                    this.folderBrowserDialog.SelectedPath = null;
                    Log.Print($"Game folder '{this.selectedGame.Folder}' not found.");
                    return;
                }
                Log.Print($"Game folder detected as '{result}'.");
                this.selectedGameParams.Path = result;
            }

            Utils.TryParseEntryPoint(this.selectedGame.EntryPoint, out string assemblyName);

            gamePath = this.selectedGameParams.Path;
            this.btnOpenFolder.ForeColor = System.Drawing.Color.Black;
            this.btnOpenFolder.Text = new DirectoryInfo(gamePath).Name;
            this.folderBrowserDialog.SelectedPath = gamePath;
            managedPath = this.FindManagedFolder(gamePath);
            managerPath = Path.Combine(managedPath, nameof(UnityModManager));
            entryAssemblyPath = Path.Combine(managedPath, assemblyName);
            injectedEntryAssemblyPath = entryAssemblyPath;
            managerAssemblyPath = Path.Combine(managerPath, typeof(UnityModManager).Module.Name);
            entryPoint = this.selectedGame.EntryPoint;
            injectedEntryPoint = this.selectedGame.EntryPoint;
            assemblyDef = null;
            injectedAssemblyDef = null;
            managerDef = null;

            gameExePath = !string.IsNullOrEmpty(this.selectedGame.GameExe) ? Path.Combine(gamePath, this.selectedGame.GameExe) : string.Empty;

            doorstopPath = Path.Combine(gamePath, doorstopFilename);
            doorstopConfigPath = Path.Combine(gamePath, doorstopConfigFilename);

            libraryPaths = new string[libraryFiles.Length];
            for (int i = 0; i < libraryFiles.Length; i++)
            {
                libraryPaths[i] = Path.Combine(managerPath, libraryFiles[i]);
            }

            DirectoryInfo parent = new DirectoryInfo(Application.StartupPath).Parent;
            for(int i = 0; i < 3; i++)
            {
                if (parent == null) {
                    break;
                }

                if (parent.FullName == gamePath)
                {
                    this.InactiveForm();
                    Log.Print("UMM Installer should not be located in the game folder.");
                    return;
                }
                parent = parent.Parent;
            }

            //machineConfigPath = string.Empty;
            //machineDoc = null;

            //if (!string.IsNullOrEmpty(selectedGame.MachineConfig))
            //{
            //    machineConfigPath = Path.Combine(gamePath, selectedGame.MachineConfig);
            //    try
            //    {
            //        machineDoc = XDocument.Load(machineConfigPath);
            //    }
            //    catch (Exception e)
            //    {
            //        InactiveForm();
            //        Log.Print(e.ToString());
            //        return;
            //    }
            //}

            try
            {
                assemblyDef = ModuleDefMD.Load(File.ReadAllBytes(entryAssemblyPath));
            }
            catch (Exception e)
            {
                this.InactiveForm();
                Log.Print(e.ToString());
                return;
            }

            bool useOldPatchTarget = false;
            GameInfo.filepathInGame = Path.Combine(managerPath, "Config.xml");
            if (File.Exists(GameInfo.filepathInGame))
            {
                GameInfo gameConfig = GameInfo.ImportFromGame();
                if(gameConfig == null || !Utils.TryParseEntryPoint(gameConfig.EntryPoint, out assemblyName))
                {
                    this.InactiveForm();
                    return;
                }
                injectedEntryPoint = gameConfig.EntryPoint;
                injectedEntryAssemblyPath = Path.Combine(managedPath, assemblyName);
            }
            else if (!string.IsNullOrEmpty(this.selectedGame.OldPatchTarget))
            {
                if (!Utils.TryParseEntryPoint(this.selectedGame.OldPatchTarget, out assemblyName))
                {
                    this.InactiveForm();
                    return;
                }
                useOldPatchTarget = true;
                injectedEntryPoint = this.selectedGame.OldPatchTarget;
                injectedEntryAssemblyPath = Path.Combine(managedPath, assemblyName);
            }

            try
            {
                injectedAssemblyDef = injectedEntryAssemblyPath == entryAssemblyPath ? assemblyDef : ModuleDefMD.Load(File.ReadAllBytes(injectedEntryAssemblyPath));
                if (File.Exists(managerAssemblyPath)) {
                    managerDef = ModuleDefMD.Load(File.ReadAllBytes(managerAssemblyPath));
                }
            }
            catch (Exception e)
            {
                this.InactiveForm();
                Log.Print(e.ToString());
                return;
            }

            List<InstallType> disabledMethods = new List<InstallType>();
            List<InstallType> unavailableMethods = new List<InstallType>();

            Type managerType = typeof(UnityModManager);
            Type starterType = typeof(Injection.UnityModManagerStarter);

            Rescan:
            TypeDef v0_12_Installed = injectedAssemblyDef.Types.FirstOrDefault(x => x.Name == managerType.Name);
            TypeDef newWayInstalled = injectedAssemblyDef.Types.FirstOrDefault(x => x.Name == starterType.Name);
            bool hasInjectedAssembly = v0_12_Installed != null || newWayInstalled != null;

            if (useOldPatchTarget && !hasInjectedAssembly)
            {
                useOldPatchTarget = false;
                injectedEntryPoint = this.selectedGame.EntryPoint;
                injectedEntryAssemblyPath = entryAssemblyPath;
                injectedAssemblyDef = assemblyDef;
                goto Rescan;
            }

            //if (machineDoc == null)
            //{
            //    unavailableMethods.Add(InstallType.Config);
            //    selectedGameParams.InstallType = InstallType.Assembly;
            //}
            //else if (hasInjectedAssembly)
            //{
            //    disabledMethods.Add(InstallType.Config);
            //    selectedGameParams.InstallType = InstallType.Assembly;
            //}
            //else if (machineDoc.Descendants("cryptoClass").Any(x => x.HasAttributes && x.FirstAttribute.Name.LocalName == "ummRngWrapper"))
            //{
            //    disabledMethods.Add(InstallType.Assembly);
            //    selectedGameParams.InstallType = InstallType.Config;
            //}

            if (Utils.IsUnixPlatform() || !File.Exists(gameExePath))
            {
                unavailableMethods.Add(InstallType.DoorstopProxy);
                this.selectedGameParams.InstallType = InstallType.Assembly;
            }
            else if (File.Exists(doorstopPath))
            {
                disabledMethods.Add(InstallType.Assembly);
                this.selectedGameParams.InstallType = InstallType.DoorstopProxy;
            }
            
            if (hasInjectedAssembly)
            {
                disabledMethods.Add(InstallType.DoorstopProxy);
                this.selectedGameParams.InstallType = InstallType.Assembly;
            }

            foreach (object ctrl in this.installTypeGroup.Controls)
            {
                if (ctrl is RadioButton btn)
                {
                    if (unavailableMethods.Exists(x => x.ToString() == btn.Name))
                    {
                        btn.Visible = false;
                        btn.Enabled = false;
                        continue;
                    }
                    if (disabledMethods.Exists(x => x.ToString() == btn.Name))
                    {
                        btn.Visible = true;
                        btn.Enabled = false;
                        continue;
                    }

                    btn.Visible = true;
                    btn.Enabled = true;
                    btn.Checked = btn.Name == this.selectedGameParams.InstallType.ToString();
                }
            }

            this.installTypeGroup.PerformLayout();

            //if (selectedGameParams.InstallType == InstallType.Config)
            //{
            //    btnRestore.Enabled = IsDirty(machineDoc) && File.Exists($"{machineConfigPath}.original_");
            //}

            if (this.selectedGameParams.InstallType == InstallType.Assembly)
            {
                this.btnRestore.Enabled = IsDirty(injectedAssemblyDef) && File.Exists($"{injectedEntryAssemblyPath}.original_");
            }

            this.tabControl.TabPages[1].Enabled = true;

            managerDef = managerDef ?? injectedAssemblyDef;

            TypeDef managerInstalled = managerDef.Types.FirstOrDefault(x => x.Name == managerType.Name);
            if (managerInstalled != null && (hasInjectedAssembly || this.selectedGameParams.InstallType == InstallType.DoorstopProxy))
            {
                this.btnInstall.Text = "Update";
                this.btnInstall.Enabled = false;
                this.btnRemove.Enabled = true;

                Version version2;
                if (v0_12_Installed != null)
                {
                    string versionString = managerInstalled.Fields.First(x => x.Name == nameof(UnityModManager.Version)).Constant.Value.ToString();
                    version2 = Utils.ParseVersion(versionString);
                }
                else
                {
                    version2 = managerDef.Assembly.Version;
                }

                this.installedVersion.Text = version2.ToString();
                if (version > version2 && v0_12_Installed == null)
                {
                    this.btnInstall.Enabled = true;
                }
            }
            else
            {
                this.installedVersion.Text = "-";
                this.btnInstall.Enabled = true;
                this.btnRemove.Enabled = false;
            }
        }

        //private void btnRunGame_SizeChanged(object sender, EventArgs e)
        //{
        //    var btn = sender as Button;
        //    btn.Location = new System.Drawing.Point((int)(btn.Parent.Size.Width / 2f - btn.Size.Width / 2f), btn.Location.Y);
        //}

        //private void btnRunGame_Click(object sender, EventArgs e)
        //{
            //Process.Start(gameExePath);
        //}

        private string FindGameFolder(string str)
        {
            string[] disks = new string[] { @"C:\", @"D:\", @"E:\", @"F:\" };
            string[] roots = new string[] { "Games", "Program files", "Program files (x86)", "" };
            string[] folders = new string[] { @"Steam\SteamApps\common", @"GoG Galaxy\Games", "" };
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                disks = new string[] { Environment.GetEnvironmentVariable("HOME") };
                roots = new string[] { "Library/Application Support", ".steam" };
                folders = new string[] { "Steam/SteamApps/common", "steam/steamapps/common" };
            }
            foreach (string disk in disks)
            {
                foreach (string root in roots)
                {
                    foreach (string folder in folders)
                    {
                        string path = Path.Combine(disk, root);
                        path = Path.Combine(path, folder);
                        path = Path.Combine(path, str);
                        if (Directory.Exists(path))
                        {
                            return path;
                        }
                    }
                }
            }
            return null;
        }

        private string FindManagedFolder(string str)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string appName = $"{Path.GetFileName(str)}.app";
                if (!Directory.Exists(Path.Combine(str, appName)))
                {
                    appName = Directory.GetDirectories(str).FirstOrDefault(dir => dir.EndsWith(".app"));
                }
                string path = Path.Combine(str, $"{appName}/Contents/Resources/Data/Managed");
                if (Directory.Exists(path))
                {
                    return path;
                }
            }
            Regex regex = new Regex(".*_Data$");
            DirectoryInfo directory = new DirectoryInfo(str);
            foreach (DirectoryInfo dir in directory.GetDirectories())
            {
                Match match = regex.Match(dir.Name);
                if (match.Success)
                {
                    string path = Path.Combine(str, $"{dir.Name}{Path.DirectorySeparatorChar}Managed");
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
            return str;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (!this.TestWritePermissions())
            {
                return;
            }
            //if (selectedGameParams.InstallType == InstallType.Config)
            //{
            //    InjectConfig(Actions.Remove, machineDoc);
            //}
            if (this.selectedGameParams.InstallType == InstallType.DoorstopProxy)
            {
                this.InstallDoorstop(Actions.Remove);
            }
            else
            {
                this.InjectAssembly(Actions.Remove, injectedAssemblyDef);
            }

            this.RefreshForm();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            if (!this.TestWritePermissions())
            {
                return;
            }
            string modsPath = Path.Combine(gamePath, this.selectedGame.ModsDirectory);
            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
            }

            //if (selectedGameParams.InstallType == InstallType.Config)
            //{
            //    InjectConfig(Actions.Install, machineDoc);
            //}
            if (this.selectedGameParams.InstallType == InstallType.DoorstopProxy)
            {
                this.InstallDoorstop(Actions.Install);
            }
            else
            {
                this.InjectAssembly(Actions.Install, assemblyDef);
            }

            this.RefreshForm();
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            //if (selectedGameParams.InstallType == InstallType.Config)
            //{
            //    var originalConfigPath = $"{machineConfigPath}.original_";
            //    RestoreOriginal(machineConfigPath, originalConfigPath);
            //}
            //else
            //{
                
            //}

            if (this.selectedGameParams.InstallType == InstallType.Assembly)
            {
                string injectedEntryAssemblyPath = Path.Combine(managedPath, injectedAssemblyDef.Name);
                string originalAssemblyPath = $"{injectedEntryAssemblyPath}.original_";
                RestoreOriginal(injectedEntryAssemblyPath, originalAssemblyPath);
            }

            this.RefreshForm();
        }

        private void btnDownloadUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.btnDownloadUpdate.Text == "Home Page")
                {
                    if (!string.IsNullOrEmpty(config.HomePage)) {
                        Process.Start(config.HomePage);
                    }
                }
                else
                {
                    Process.Start("Downloader.exe");
                }
            }
            catch(Exception ex)
            {
                Log.Print(ex.ToString());
            }
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            DialogResult result = this.folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                this.selectedGameParams.Path = this.folderBrowserDialog.SelectedPath;
                this.RefreshForm();
            }
        }

        private void gameList_Changed(object sender, EventArgs e)
        {
            GameInfo selected = (GameInfo)((ComboBox)sender).SelectedItem;
            if (selected != null)
            {
                Log.Print($"Game changed to '{selected.Name}'.");
                param.LastSelectedGame = selected.Name;
                this.selectedGameParams = param.GetGameParam(selected);
            }

            this.RefreshForm();
        }

        enum Actions
        {
            Install,
            Remove
        };

        private bool InstallDoorstop(Actions action, bool write = true)
        {
            string gameConfigPath = GameInfo.filepathInGame;

            bool success = false;
            switch (action)
            {
                case Actions.Install:
                    try
                    {
                        Log.Print("=======================================");

                        if (!Directory.Exists(managerPath)) {
                            Directory.CreateDirectory(managerPath);
                        }

                        Utils.MakeBackup(doorstopPath);
                        Utils.MakeBackup(doorstopConfigPath);
                        Utils.MakeBackup(libraryPaths);

                        if (!this.InstallDoorstop(Actions.Remove, false))
                        {
                            Log.Print("Installation failed. Can't uninstall the previous version.");
                            goto EXIT;
                        }

                        Log.Print($"Copying files to game...");
                        bool? arch = Utils.UnmanagedDllIs64Bit(gameExePath);
                        string filename = arch == true ? "winhttp_x64.dll" : "winhttp_x86.dll";
                        Log.Print($"  '{filename}'");
                        File.Copy(filename, doorstopPath, true);
                        Log.Print($"  '{doorstopConfigFilename}'");
                        File.WriteAllText(doorstopConfigPath, "[UnityDoorstop]" + Environment.NewLine + "enabled = true" + Environment.NewLine + "targetAssembly = " + managerAssemblyPath);

                        DoactionLibraries(Actions.Install);
                        this.DoactionGameConfig(Actions.Install);
                        Log.Print("Installation was successful.");

                        success = true;
                    }
                    catch (Exception e)
                    {
                        Log.Print(e.ToString());
                        Utils.RestoreBackup(doorstopPath);
                        Utils.RestoreBackup(doorstopConfigPath);
                        Utils.RestoreBackup(libraryPaths);
                        Utils.RestoreBackup(gameConfigPath);
                        Log.Print("Installation failed.");
                    }
                    break;

                case Actions.Remove:
                    try
                    {
                        if (write)
                        {
                            Log.Print("=======================================");
                        }

                        Utils.MakeBackup(gameConfigPath);
                        if (write)
                        {
                            
                            Utils.MakeBackup(doorstopPath);
                            Utils.MakeBackup(doorstopConfigPath);
                            Utils.MakeBackup(libraryPaths);
                        }

                        Log.Print($"Deleting files from game...");
                        Log.Print($"  '{doorstopFilename}'");
                        File.Delete(doorstopPath);
                        Log.Print($"  '{doorstopConfigFilename}'");
                        File.Delete(doorstopConfigPath);

                        if (write)
                        {
                            DoactionLibraries(Actions.Remove);
                            this.DoactionGameConfig(Actions.Remove);
                            Log.Print("Removal was successful.");
                        }

                        success = true;
                    }
                    catch (Exception e)
                    {
                        Log.Print(e.ToString());
                        if (write)
                        {
                            Utils.RestoreBackup(doorstopPath);
                            Utils.RestoreBackup(doorstopConfigPath);
                            Utils.RestoreBackup(libraryPaths);
                            Utils.RestoreBackup(gameConfigPath);
                            Log.Print("Removal failed.");
                        }
                    }
                    break;
            }

            EXIT:

            if (write)
            {
                try
                {
                    Utils.DeleteBackup(doorstopPath);
                    Utils.DeleteBackup(doorstopConfigPath);
                    Utils.DeleteBackup(libraryPaths);
                    Utils.DeleteBackup(gameConfigPath);
                }
                catch (Exception)
                {
                }
            }

            return success;
        }

        //private bool InjectConfig(Actions action, XDocument doc = null, bool write = true)
        //{
        //    var originalMachineConfigPath = $"{machineConfigPath}.original_";
        //    var gameConfigPath = GameInfo.filepathInGame;

        //    var success = false;

        //    switch (action)
        //    {
        //        case Actions.Install:
        //            try
        //            {
        //                if (!Directory.Exists(managerPath))
        //                    Directory.CreateDirectory(managerPath);

        //                Utils.MakeBackup(machineConfigPath);
        //                Utils.MakeBackup(libraryPaths);

        //                if (!IsDirty(doc))
        //                {
        //                    File.Copy(machineConfigPath, originalMachineConfigPath, true);
        //                }
        //                MakeDirty(doc);

        //                if (!InjectConfig(Actions.Remove, doc, false))
        //                {
        //                    Log.Print("Installation failed. Can't uninstall the previous version.");
        //                    goto EXIT;
        //                }

        //                Log.Print($"Applying patch to '{Path.GetFileName(machineConfigPath)}'...");

        //                foreach (var mapping in doc.Descendants("cryptoNameMapping"))
        //                {
        //                    foreach(var cryptoClasses in mapping.Elements("cryptoClasses"))
        //                    {
        //                        if (!cryptoClasses.Elements("cryptoClass").Any(x => x.FirstAttribute.Name.LocalName == "ummRngWrapper"))
        //                        {
        //                            cryptoClasses.Add(new XElement("cryptoClass", new XAttribute("ummRngWrapper", "UnityModManagerNet.RngWrapper, UnityModManager, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")));
        //                        }
        //                    }
        //                    if (!mapping.Elements("nameEntry").Any(x => x.LastAttribute.Value == "ummRngWrapper"))
        //                    {
        //                        //mapping.Add(new XElement("nameEntry", new XAttribute("name", "RandomNumberGenerator"), new XAttribute("class", "ummRngWrapper")));
        //                        mapping.Add(new XElement("nameEntry", new XAttribute("name", "System.Security.Cryptography.RandomNumberGenerator"), new XAttribute("class", "ummRngWrapper")));
        //                    }
        //                    break;
        //                }

        //                doc.Save(machineConfigPath);
        //                DoactionLibraries(Actions.Install);
        //                DoactionGameConfig(Actions.Install);
        //                Log.Print("Installation was successful.");

        //                success = true;
        //            }
        //            catch (Exception e)
        //            {
        //                Log.Print(e.ToString());
        //                Utils.RestoreBackup(machineConfigPath);
        //                Utils.RestoreBackup(libraryPaths);
        //                Utils.RestoreBackup(gameConfigPath);
        //                Log.Print("Installation failed.");
        //            }

        //            break;

        //        case Actions.Remove:
        //            try
        //            {
        //                Utils.MakeBackup(gameConfigPath);
        //                if (write)
        //                {
        //                    Utils.MakeBackup(machineConfigPath);
        //                    Utils.MakeBackup(libraryPaths);
        //                }

        //                Log.Print("Removing patch...");

        //                MakeDirty(doc);

        //                foreach (var mapping in doc.Descendants("cryptoNameMapping"))
        //                {
        //                    foreach (var cryptoClasses in mapping.Elements("cryptoClasses"))
        //                    {
        //                        foreach (var cryptoClass in cryptoClasses.Elements("cryptoClass"))
        //                        {
        //                            if (cryptoClass.FirstAttribute.Name.LocalName == "ummRngWrapper")
        //                            {
        //                                cryptoClass.Remove();
        //                            }
        //                        }
        //                    }
        //                    foreach (var nameEntry in mapping.Elements("nameEntry"))
        //                    {
        //                        if (nameEntry.LastAttribute.Value == "ummRngWrapper")
        //                        {
        //                            nameEntry.Remove();
        //                        }
        //                    }
        //                    break;
        //                }

        //                if (write)
        //                {
        //                    doc.Save(machineConfigPath);
        //                    DoactionLibraries(Actions.Remove);
        //                    DoactionGameConfig(Actions.Remove);
        //                    Log.Print("Removal was successful.");
        //                }

        //                success = true;
        //            }
        //            catch (Exception e)
        //            {
        //                Log.Print(e.ToString());
        //                if (write)
        //                {
        //                    Utils.RestoreBackup(machineConfigPath);
        //                    Utils.RestoreBackup(libraryPaths);
        //                    Utils.RestoreBackup(gameConfigPath);
        //                    Log.Print("Removal failed.");
        //                }
        //            }

        //            break;
        //    }

        //    EXIT:

        //    if (write)
        //    {
        //        try
        //        {
        //            Utils.DeleteBackup(machineConfigPath);
        //            Utils.DeleteBackup(libraryPaths);
        //            Utils.DeleteBackup(gameConfigPath);
        //        }
        //        catch (Exception)
        //        {
        //        }
        //    }

        //    return success;
        //}

        private bool InjectAssembly(Actions action, ModuleDefMD assemblyDef, bool write = true)
        {
            Type managerType = typeof(UnityModManager);
            Type starterType = typeof(Injection.UnityModManagerStarter);
            string gameConfigPath = GameInfo.filepathInGame;

            string assemblyPath = Path.Combine(managedPath, assemblyDef.Name);
            string originalAssemblyPath = $"{assemblyPath}.original_";

            bool success = false;

            switch (action)
            {
                case Actions.Install:
                    {
                        try
                        {
                            Log.Print("=======================================");

                            if (!Directory.Exists(managerPath)) {
                            Directory.CreateDirectory(managerPath);
                        }

                        Utils.MakeBackup(assemblyPath);
                            Utils.MakeBackup(libraryPaths);

                            if (!IsDirty(assemblyDef))
                            {
                                File.Copy(assemblyPath, originalAssemblyPath, true);
                                MakeDirty(assemblyDef);
                            }

                            if (!this.InjectAssembly(Actions.Remove, injectedAssemblyDef, assemblyDef != injectedAssemblyDef))
                            {
                                Log.Print("Installation failed. Can't uninstall the previous version.");
                                goto EXIT;
                            }

                            Log.Print($"Applying patch to '{Path.GetFileName(assemblyPath)}'...");

                            if (!Utils.TryGetEntryPoint(assemblyDef, entryPoint, out MethodDef methodDef, out string insertionPlace, true))
                            {
                                goto EXIT;
                            }

                        ModuleDefMD starterDef = ModuleDefMD.Load(starterType.Module);
                        TypeDef starter = starterDef.Types.First(x => x.Name == starterType.Name);
                            starterDef.Types.Remove(starter);
                            assemblyDef.Types.Add(starter);

                        Instruction instr = OpCodes.Call.ToInstruction(starter.Methods.First(x => x.Name == nameof(Injection.UnityModManagerStarter.Start)));
                            if (insertionPlace == "before")
                            {
                                methodDef.Body.Instructions.Insert(0, instr);
                            }
                            else
                            {
                                methodDef.Body.Instructions.Insert(methodDef.Body.Instructions.Count - 1, instr);
                            }

                            assemblyDef.Write(assemblyPath);
                            DoactionLibraries(Actions.Install);
                        this.DoactionGameConfig(Actions.Install);

                            Log.Print("Installation was successful.");

                            success = true;
                        }
                        catch (Exception e)
                        {
                            Log.Print(e.ToString());
                            Utils.RestoreBackup(assemblyPath);
                            Utils.RestoreBackup(libraryPaths);
                            Utils.RestoreBackup(gameConfigPath);
                            Log.Print("Installation failed.");
                        }
                    }
                    break;

                case Actions.Remove:
                    {
                        try
                        {
                            if (write)
                            {
                                Log.Print("=======================================");
                            }

                            Utils.MakeBackup(gameConfigPath);

                        TypeDef v0_12_Installed = assemblyDef.Types.FirstOrDefault(x => x.Name == managerType.Name);
                        TypeDef newWayInstalled = assemblyDef.Types.FirstOrDefault(x => x.Name == starterType.Name);

                            if (v0_12_Installed != null || newWayInstalled != null)
                            {
                                if (write)
                                {
                                    Utils.MakeBackup(assemblyPath);
                                    Utils.MakeBackup(libraryPaths);
                                }

                                Log.Print("Removing patch...");

                                Instruction instr = null;

                                if (newWayInstalled != null)
                                {
                                    instr = OpCodes.Call.ToInstruction(newWayInstalled.Methods.First(x => x.Name == nameof(Injection.UnityModManagerStarter.Start)));
                                }
                                else if (v0_12_Installed != null)
                                {
                                    instr = OpCodes.Call.ToInstruction(v0_12_Installed.Methods.First(x => x.Name == nameof(UnityModManager.Start)));
                                }

                                if (!string.IsNullOrEmpty(injectedEntryPoint))
                                {
                                    if (!Utils.TryGetEntryPoint(assemblyDef, injectedEntryPoint, out MethodDef methodDef, out _, true))
                                    {
                                        goto EXIT;
                                    }

                                    for (int i = 0; i < methodDef.Body.Instructions.Count; i++)
                                    {
                                        if (methodDef.Body.Instructions[i].OpCode == instr.OpCode && methodDef.Body.Instructions[i].Operand == instr.Operand)
                                        {
                                            methodDef.Body.Instructions.RemoveAt(i);
                                            break;
                                        }
                                    }
                                }

                                if (newWayInstalled != null) {
                                assemblyDef.Types.Remove(newWayInstalled);
                            } else if (v0_12_Installed != null) {
                                assemblyDef.Types.Remove(v0_12_Installed);
                            }

                            if (!IsDirty(assemblyDef))
                                {
                                    MakeDirty(assemblyDef);
                                }

                                if (write)
                                {
                                    assemblyDef.Write(assemblyPath);
                                    DoactionLibraries(Actions.Remove);
                                this.DoactionGameConfig(Actions.Remove);
                                    Log.Print("Removal was successful.");
                                }
                            }

                            success = true;
                        }
                        catch (Exception e)
                        {
                            Log.Print(e.ToString());
                            if (write)
                            {
                                Utils.RestoreBackup(assemblyPath);
                                Utils.RestoreBackup(libraryPaths);
                                Utils.RestoreBackup(gameConfigPath);
                                Log.Print("Removal failed.");
                            }
                        }
                    }
                    break;
            }

            EXIT:

            if (write)
            {
                try
                {
                    Utils.DeleteBackup(assemblyPath);
                    Utils.DeleteBackup(libraryPaths);
                    Utils.DeleteBackup(gameConfigPath);
                }
                catch (Exception)
                {
                }
            }

            return success;
        }

        //private static bool IsDirty(XDocument doc)
        //{
        //    return doc.Root.Element("mscorlib").Attribute(nameof(UnityModManager)) != null;
        //}

        //private static void MakeDirty(XDocument doc)
        //{
        //    doc.Root.Element("mscorlib").SetAttributeValue(nameof(UnityModManager), UnityModManager.version);
        //}

        private static bool IsDirty(ModuleDefMD assembly) => assembly.Types.FirstOrDefault(x => x.FullName == typeof(Marks.IsDirty).FullName || x.Name == typeof(UnityModManager).Name) != null;

        private static void MakeDirty(ModuleDefMD assembly)
        {
            ModuleDefMD moduleDef = ModuleDefMD.Load(typeof(Marks.IsDirty).Module);
            TypeDef typeDef = moduleDef.Types.FirstOrDefault(x => x.FullName == typeof(Marks.IsDirty).FullName);
            moduleDef.Types.Remove(typeDef);
            assembly.Types.Add(typeDef);
        }

        private bool TestWritePermissions()
        {
            bool success = true;

            success = Utils.IsDirectoryWritable(managedPath) && success;
            success = Utils.IsFileWritable(managerAssemblyPath) && success;
            success = Utils.IsFileWritable(GameInfo.filepathInGame) && success;

            foreach (string file in libraryPaths)
            {
                success = Utils.IsFileWritable(file) && success;
            }

            //if (machineDoc != null)
            //{
            //    success = Utils.IsFileWritable(machineConfigPath) && success;
            //}
            if (this.selectedGameParams.InstallType == InstallType.DoorstopProxy)
            {
                success = Utils.IsFileWritable(doorstopPath) && success;
            }
            else
            {
                success = Utils.IsFileWritable(entryAssemblyPath) && success;
                if (injectedEntryAssemblyPath != entryAssemblyPath) {
                    success = Utils.IsFileWritable(injectedEntryAssemblyPath) && success;
                }
            }

            return success;
        }

        private static bool RestoreOriginal(string file, string backup)
        {
            try
            {
                File.Copy(backup, file, true);
                Log.Print("Original files restored.");
                File.Delete(backup);
                return true;
            }
            catch (Exception e)
            {
                Log.Print(e.Message);
            }

            return false;
        }

        private static void DoactionLibraries(Actions action)
        {
            if (action == Actions.Install)
            {
                Log.Print($"Copying files to game...");
            }
            else
            {
                Log.Print($"Deleting files from game...");
            }

            for (int i = 0; i < libraryPaths.Length; i++)
            {
                string filename = libraryFiles[i];
                string path = libraryPaths[i];
                if (action == Actions.Install)
                {
                    if (File.Exists(path))
                    {
                        FileInfo source = new FileInfo(filename);
                        FileInfo dest = new FileInfo(path);
                        if (dest.LastWriteTimeUtc == source.LastWriteTimeUtc) {
                            continue;
                        }

                        //File.Copy(path, $"{path}.old_", true);
                    }

                    Log.Print($"  {filename}");
                    File.Copy(filename, path, true);
                }
                else
                {
                    if (File.Exists(path))
                    {
                        Log.Print($"  {filename}");
                        File.Delete(path);
                    }
                }
            }
        }

        private void DoactionGameConfig(Actions action)
        {
            if (action == Actions.Install)
            {
                Log.Print($"Creating configs...");
                Log.Print($"  Config.xml");

                this.selectedGame.ExportToGame();
            }
            else
            {
                Log.Print($"Deleting configs...");
                if (File.Exists(GameInfo.filepathInGame))
                {
                    Log.Print($"  Config.xml");
                    File.Delete(GameInfo.filepathInGame);
                }
            }
        }

        private void folderBrowserDialog_HelpRequest(object sender, EventArgs e)
        {
        }

        private void tabs_Changed(object sender, EventArgs e)
        {
            switch (this.tabControl.SelectedIndex)
            {
                case 1: // Mods
                    this.ReloadMods();
                    this.RefreshModList();
                    if (this.selectedGame != null && !this.repositories.ContainsKey(this.selectedGame)) {
                        this.CheckModUpdates();
                    }

                    break;
            }
        }
    }
}
