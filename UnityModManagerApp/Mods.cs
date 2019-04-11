using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Ionic.Zip;

namespace UnityModManagerNet.Installer {
    public partial class UnityModManagerForm : Form {
        readonly List<ModInfo> mods = new List<ModInfo>();

        private void InitPageMods() {
            this.splitContainerModsInstall.Panel2.AllowDrop = true;
            this.splitContainerModsInstall.Panel2.DragEnter += new DragEventHandler(this.Mods_DragEnter);
            this.splitContainerModsInstall.Panel2.DragDrop += new DragEventHandler(this.Mods_DragDrop);
        }

        private void BtnModInstall_Click(object sender, EventArgs e) {
            DialogResult result = this.modInstallFileDialog.ShowDialog();
            if (result == DialogResult.OK) {
                if (this.modInstallFileDialog.FileNames.Length == 0) {
                    return;
                }

                this.SaveAndInstallZipFiles(this.modInstallFileDialog.FileNames);
                this.ReloadMods();
                this.RefreshModList();
            }
        }

        private void Mods_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void Mods_DragDrop(object sender, DragEventArgs e) {
            string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) {
                return;
            }

            //Drag and drop files on OS X are in the format /.file/id=6571367.2773272
            if (Environment.OSVersion.Platform == PlatformID.Unix && files[0].StartsWith("/.file")) {
                files = files.Select(f => Utils.ResolveOSXFileUrl(f)).ToArray();
            }
            this.SaveAndInstallZipFiles(files);
            this.ReloadMods();
            this.RefreshModList();
        }

        private void SaveAndInstallZipFiles(string[] files) {
            string programModsPath = Path.Combine(Application.StartupPath, this.selectedGame.Folder);
            List<ModInfo> newMods = new List<ModInfo>();
            foreach (string filepath in files) {
                try {
                    if (Path.GetExtension(filepath) == ".zip") {
                        using (ZipFile zip = ZipFile.Read(filepath)) {
                            this.InstallMod(zip, false);
                            ModInfo modInfo = this.ReadModInfoFromZip(zip);
                            if (modInfo) {
                                newMods.Add(modInfo);
                                string dir = Path.Combine(programModsPath, modInfo.Id);
                                if (!Directory.Exists(dir)) {
                                    Directory.CreateDirectory(dir);
                                }
                                string target = Path.Combine(dir, Path.GetFileName(filepath));
                                if (filepath != target) {
                                    File.Copy(filepath, target, true);
                                }
                            }
                        }
                    } else {
                        Log.Print($"Only zip files are possible.");
                    }
                } catch (Exception ex) {
                    Log.Print(ex.Message);
                    Log.Print($"Error when installing file '{Path.GetFileName(filepath)}'.");
                }
            }

            // delete old zip files if count > 2
            if (newMods.Count > 0) {
                foreach (ModInfo modInfo in newMods) {
                    List<ModInfo> tempList = new List<ModInfo>();
                    foreach (string filepath in Directory.GetFiles(Path.Combine(programModsPath, modInfo.Id), "*.zip", SearchOption.AllDirectories)) {
                        ModInfo mod = this.ReadModInfoFromZip(filepath);
                        if (mod && !mod.EqualsVersion(modInfo)) {
                            tempList.Add(mod);
                        }
                    }
                    tempList = tempList.OrderBy(x => x.ParsedVersion).ToList();
                    while (tempList.Count > 2) {
                        ModInfo item = tempList.First();
                        try {
                            tempList.Remove(item);
                            File.Delete(item.Path);
                        } catch (Exception ex) {
                            Log.Print(ex.Message);
                            Log.Print($"Can't delete old temp file '{item.Path}'.");
                            break;
                        }
                    }
                }
            }
        }

        private void UninstallMod(string name) {
            string modsPath = Path.Combine(gamePath, this.selectedGame.ModsDirectory);
            string modPath = Path.Combine(modsPath, name);
            if (this.selectedGame == null) {
                Log.Print("Select a game.");
                return;
            }
          
            if (!Directory.Exists(modsPath)) {
                Log.Print("Install the UnityModManager.");
                return;
            }

            if (Directory.Exists(modPath)) {
                try {
                    Directory.Delete(modPath, true);
                    Log.Print($"Deleting '{name}' - SUCCESS.");
                } catch (Exception ex) {
                    Log.Print(ex.Message);
                    Log.Print($"Error when uninstalling '{name}'.");
                }
            } else {
                Log.Print($"Directory '{modPath}' - not found.");
            }

            this.ReloadMods();
            this.RefreshModList();
        }

        private void InstallMod(string filepath) {
            if (!File.Exists(filepath)) {
                Log.Print($"File not found '{Path.GetFileName(filepath)}'.");
            }
            try {
                using (ZipFile zip = ZipFile.Read(filepath)) {
                    this.InstallMod(zip);
                }
            } catch (Exception e) {
                Log.Print(e.Message);
                Log.Print($"Error when installing '{Path.GetFileName(filepath)}'.");
            }
        }

        private void InstallMod(ZipFile zip, bool reloadMods = true) {
            string modsPath = Path.Combine(gamePath, this.selectedGame.ModsDirectory);

            if (this.selectedGame == null) {
                Log.Print("Select a game.");
                return;
            }

            if (!Directory.Exists(modsPath)) {
                Log.Print("Install the UnityModManager.");
                return;
            }

            try {
                foreach (ZipEntry e in zip.EntriesSorted) {
                    if (e.IsDirectory) {
                        continue;
                    }

                    string filepath = Path.Combine(modsPath, e.FileName);
                    if (File.Exists(filepath)) {
                        File.Delete(filepath);
                    }
                }
                foreach (ZipEntry entry in zip.EntriesSorted) {
                    if (entry.IsDirectory) {
                        Directory.CreateDirectory(Path.Combine(modsPath, entry.FileName));
                    } else {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(modsPath, entry.FileName)));
                        using (FileStream fs = new FileStream(Path.Combine(modsPath, entry.FileName), FileMode.Create, FileAccess.Write)) {
                            entry.Extract(fs);
                        }
                    }
                }
                Log.Print($"Unpacking '{Path.GetFileName(zip.Name)}' - SUCCESS.");
            } catch (Exception ex) {
                Log.Print(ex.Message);
                Log.Print(ex.StackTrace);
                Log.Print($"Error when unpacking '{Path.GetFileName(zip.Name)}'.");
            }

            if (reloadMods) {
                this.ReloadMods();
                this.RefreshModList();
            }
        }

        private void ReloadMods() {
            this.mods.Clear();

            if (this.selectedGame == null) {
                return;
            }

            string modsPath = Path.Combine(gamePath, this.selectedGame.ModsDirectory);
            if (Directory.Exists(modsPath)) {
                foreach (string dir in Directory.GetDirectories(modsPath)) {
                    string jsonPath = Path.Combine(dir, this.selectedGame.ModInfo);
                    if (!File.Exists(jsonPath)) {
                        jsonPath = Path.Combine(dir, this.selectedGame.ModInfo.ToLower());
                    }

                    if (File.Exists(jsonPath)) {
                        try {
                            ModInfo modInfo = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(jsonPath));
                            if (modInfo && modInfo.IsValid()) {
                                modInfo.Path = dir;
                                modInfo.Status = ModStatus.Installed;
                                this.mods.Add(modInfo);
                            }
                        } catch (Exception e) {
                            Log.Print(e.Message);
                            Log.Print($"Error parsing file '{jsonPath}'.");
                        }
                    }
                }
            }

            this.LoadZipMods();
        }

        private void LoadZipMods() {
            if (this.selectedGame == null) {
                return;
            }

            string dir = Path.Combine(Application.StartupPath, this.selectedGame.Folder);
            if (!Directory.Exists(dir)) {
                return;
            }

            foreach (string filepath in Directory.GetFiles(dir, "*.zip", SearchOption.AllDirectories)) {
                ModInfo modInfo = this.ReadModInfoFromZip(filepath);
                if (!modInfo) {
                    continue;
                }

                int index = this.mods.FindIndex(m => m.Id == modInfo.Id);
                if (index == -1) {
                    modInfo.Status = ModStatus.NotInstalled;
                    modInfo.AvailableVersions.Add(modInfo.ParsedVersion, filepath);
                    this.mods.Add(modInfo);
                } else {
                    if (!this.mods[index].AvailableVersions.ContainsKey(modInfo.ParsedVersion)) {
                        this.mods[index].AvailableVersions.Add(modInfo.ParsedVersion, filepath);
                    }
                }
            }
        }

        private void RefreshModList() {
            this.listMods.Items.Clear();

            if (this.selectedGame == null || this.mods.Count == 0 || this.tabControl.SelectedIndex != 1) {
                return;
            }

            this.mods.Sort((x, y) => x.DisplayName.CompareTo(y.DisplayName));

            foreach (ModInfo modInfo in this.mods) {
                string status = "";
                ListViewItem listItem = new ListViewItem(modInfo.DisplayName);

                if (modInfo.Status == ModStatus.Installed) {
                    UnityModManager.Repository.Release res = this.repositories.ContainsKey(this.selectedGame) ? this.repositories[this.selectedGame].FirstOrDefault(x => x.Id == modInfo.Id) : null;
                    Version web = !string.IsNullOrEmpty(res?.Version) ? Utils.ParseVersion(res.Version) : new Version();
                    Version local = modInfo.AvailableVersions.Keys.Max(x => x) ?? new Version();
                    Version newest = web > local ? web : local;

                    status = newest > modInfo.ParsedVersion ? $"Available {newest}" : "OK";
                } else if(modInfo.Status == ModStatus.NotInstalled) {
                    status = "";
                }

                
                if (modInfo.Status == ModStatus.NotInstalled) {
                    listItem.SubItems.Add(modInfo.AvailableVersions.Count > 0 ? modInfo.AvailableVersions.Keys.Max(x => x).ToString() : modInfo.Version);
                } else {
                    listItem.SubItems.Add(modInfo.Version);
                }
                if (!string.IsNullOrEmpty(modInfo.ManagerVersion)) {
                    listItem.SubItems.Add(modInfo.ManagerVersion);
                    if (version < Utils.ParseVersion(modInfo.ManagerVersion)) {
                        listItem.ForeColor = System.Drawing.Color.FromArgb(192, 0, 0);
                        status = "Need to update UMM";
                    }
                } else {
                    listItem.SubItems.Add("");
                }
                listItem.SubItems.Add(status);
                this.listMods.Items.Add(listItem);
            }
        }

        private ModInfo ReadModInfoFromZip(string filepath) {
            try {
                using (ZipFile zip = ZipFile.Read(filepath)) {
                    return this.ReadModInfoFromZip(zip);
                }
            } catch (Exception e) {
                Log.Print(e.Message);
                Log.Print($"Error parsing file '{Path.GetFileName(filepath)}'.");
            }

            return null;
        }



        private ModInfo ReadModInfoFromZip(ZipFile zip) {
            try {
                foreach (ZipEntry e in zip) {
                    if (e.FileName.EndsWith(this.selectedGame.ModInfo, StringComparison.InvariantCultureIgnoreCase)) {
                        using (StreamReader s = new StreamReader(e.OpenReader())) {
                            ModInfo modInfo = JsonConvert.DeserializeObject<ModInfo>(s.ReadToEnd());
                            if (modInfo.IsValid()) {
                                modInfo.Path = zip.Name;
                                return modInfo;
                            }
                        }
                        break;
                    }
                }
            } catch (Exception e) {
                Log.Print(e.Message);
                Log.Print($"Error parsing file '{Path.GetFileName(zip.Name)}'.");
            }

            return null;
        }

        private void ModcontextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e) {
            this.installToolStripMenuItem.Visible = false;
            this.uninstallToolStripMenuItem.Visible = false;
            this.updateToolStripMenuItem.Visible = false;
            this.revertToolStripMenuItem.Visible = false;
            this.wwwToolStripMenuItem1.Visible = false;

            ModInfo modInfo = this.selectedMod;
            if (!modInfo) {
                e.Cancel = true;
                return;
            }

            if (modInfo.Status == ModStatus.Installed) {
                this.uninstallToolStripMenuItem.Visible = true;
                Version newest = modInfo.AvailableVersions.Keys.Max(x => x);
                if (newest != null && newest > modInfo.ParsedVersion) {
                    this.updateToolStripMenuItem.Text = $"Update to {newest}";
                    this.updateToolStripMenuItem.Visible = true;
                }
                Version previous = modInfo.AvailableVersions.Keys.Where(x => x < modInfo.ParsedVersion).Max(x => x);
                if (previous != null) {
                    this.revertToolStripMenuItem.Text = $"Revert to {previous}";
                    this.revertToolStripMenuItem.Visible = true;
                }
            } else if (modInfo.Status == ModStatus.NotInstalled) {
                this.installToolStripMenuItem.Visible = true;
            }

            if (!string.IsNullOrEmpty(modInfo.HomePage)) {
                this.wwwToolStripMenuItem1.Visible = true;
            }
        }

        private void InstallToolStripMenuItem_Click(object sender, EventArgs e) {
            ModInfo modInfo = this.selectedMod;
            if (modInfo) {
                KeyValuePair<Version, string> newest = modInfo.AvailableVersions.OrderByDescending(x => x.Key).FirstOrDefault();
                if (!string.IsNullOrEmpty(newest.Value)) {
                    this.InstallMod(newest.Value);
                }
            }
        }

        private void UpdateToolStripMenuItem_Click(object sender, EventArgs e) => this.InstallToolStripMenuItem_Click(sender, e);

        private void UninstallToolStripMenuItem_Click(object sender, EventArgs e) {
            ModInfo modInfo = this.selectedMod;
            if (modInfo) {
                this.UninstallMod(modInfo.Id);
            }
        }

        private void RevertToolStripMenuItem_Click(object sender, EventArgs e) {
            ModInfo modInfo = this.selectedMod;
            if (modInfo) {
                KeyValuePair<Version, string> previous = modInfo.AvailableVersions.Where(x => x.Key < modInfo.ParsedVersion).OrderByDescending(x => x.Key).FirstOrDefault();
                if (!string.IsNullOrEmpty(previous.Value)) {
                    this.InstallMod(previous.Value);
                }
            }
        }

        private void WwwToolStripMenuItem1_Click(object sender, EventArgs e) {
            ModInfo modInfo = this.selectedMod;
            if (modInfo) {
                System.Diagnostics.Process.Start(modInfo.HomePage);
            }
        }
    }
}
