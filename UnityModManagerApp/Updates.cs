﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Net;
using System.Net.NetworkInformation;

namespace UnityModManagerNet.Installer {
    public partial class UnityModManagerForm : Form {
        readonly Dictionary<GameInfo, HashSet<UnityModManager.Repository.Release>> repositories = new Dictionary<GameInfo, HashSet<UnityModManager.Repository.Release>>();

        private void CheckModUpdates() {
            if (this.selectedGame == null) {
                return;
            }

            if (!HasNetworkConnection()) {
                return;
            }

            if (!this.repositories.ContainsKey(this.selectedGame)) {
                this.repositories.Add(this.selectedGame, new HashSet<UnityModManager.Repository.Release>());
            }

            HashSet<string> urls = new HashSet<string>();
            foreach (ModInfo mod in this.mods) {
                if (!string.IsNullOrEmpty(mod.Repository)) {
                    urls.Add(mod.Repository);
                }
            }

            if (urls.Count > 0) {
                foreach (string url in urls) {
                    try {
                        using (WebClient wc = new WebClient()) {
                            wc.Encoding = System.Text.Encoding.UTF8;
                            wc.DownloadStringCompleted += (sender, e) => { this.ModUpdates_DownloadStringCompleted(sender, e, this.selectedGame, url); };
                            wc.DownloadStringAsync(new Uri(url));
                        }
                    } catch (Exception e) {
                        Log.Print(e.Message);
                        Log.Print($"Error checking updates on '{url}'.");
                    }
                }
            }
        }

        private void ModUpdates_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e, GameInfo game, string url) {
            if (e.Error != null) {
                Log.Print(e.Error.Message);
                return;
            }

            if (!e.Cancelled && !string.IsNullOrEmpty(e.Result) && this.repositories.ContainsKey(game)) {
                try {
                    UnityModManager.Repository repository = JsonConvert.DeserializeObject<UnityModManager.Repository>(e.Result);
                    if (repository == null || repository.releases == null || repository.releases.Length == 0) {
                        return;
                    }

                    this.listMods.Invoke((MethodInvoker) delegate {
                        foreach (UnityModManager.Repository.Release v in repository.releases) {
                            this.repositories[game].Add(v);
                        }
                        if (this.selectedGame == game) {
                            this.RefreshModList();
                        }
                    });
                } catch (Exception ex) {
                    Log.Print(ex.Message);
                    Log.Print($"Error checking updates on '{url}'.");
                }
            }
        }

        private void CheckLastVersion() {
            if (string.IsNullOrEmpty(config.Repository)) {
                return;
            }

            Log.Print("Checking for updates.");

            if (!HasNetworkConnection()) {
                Log.Print("No network connection or firewall blocked.");
                return;
            }

            try {
                using (WebClient wc = new WebClient()) {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.DownloadStringCompleted += this.LastVersion_DownloadStringCompleted;
                    wc.DownloadStringAsync(new Uri(config.Repository));
                }
            } catch (Exception e) {
                Log.Print(e.Message);
                Log.Print($"Error checking update.");
            }
        }

        private void LastVersion_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e) {
            if (e.Error != null) {
                Log.Print(e.Error.Message);
                return;
            }

            if (!e.Cancelled && !string.IsNullOrEmpty(e.Result)) {
                try {
                    UnityModManager.Repository repository = JsonConvert.DeserializeObject<UnityModManager.Repository>(e.Result);
                    if (repository == null || repository.releases == null || repository.releases.Length == 0) {
                        return;
                    }

                    UnityModManager.Repository.Release release = repository.releases.FirstOrDefault(x => x.Id == nameof(UnityModManager));
                    if (release != null && !string.IsNullOrEmpty(release.Version)) {
                        Version ver = Utils.ParseVersion(release.Version);
                        if (version < ver) {
                            this.btnDownloadUpdate.Text = $"Download {release.Version}";
                            Log.Print($"Update is available.");
                        } else {
                            Log.Print($"No updates.");
                        }
                    }
                } catch (Exception ex) {
                    Log.Print(ex.Message);
                    Log.Print($"Error checking update.");
                }
            }
        }

        public static bool HasNetworkConnection() {
            try {
                using (Ping ping = new Ping()) {
                    return ping.Send("www.google.com.mx", 2000).Status == IPStatus.Success;
                }
            } catch (Exception e) {
                Log.Print(e.Message);
            }

            return false;
        }
    }
}