using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace UnityModManagerNet {
    public partial class UnityModManager {
        private static void CheckModUpdates() {
            Logger.Log("Checking for updates.");

            if (!HasNetworkConnection()) {
                Logger.Log("No network connection or firewall blocked.");
                return;
            }

            HashSet<string> urls = new HashSet<string>();

            foreach (ModEntry modEntry in modEntries) {
                if (!string.IsNullOrEmpty(modEntry.Info.Repository)) {
                    urls.Add(modEntry.Info.Repository);
                }
            }

            if (urls.Count > 0) {
                foreach (string url in urls) {
                    UI.Instance.StartCoroutine(DownloadString(url, ParseRepository));
                }
            }
        }

        private static void ParseRepository(string json, string url) {
            if (string.IsNullOrEmpty(json)) {
                return;
            }

            try {
                Repository repository = JsonUtility.FromJson<Repository>(json);
                if (repository != null && repository.releases != null && repository.releases.Length > 0) {
                    foreach (Repository.Release release in repository.releases) {
                        if (!string.IsNullOrEmpty(release.Id) && !string.IsNullOrEmpty(release.Version)) {
                            ModEntry modEntry = FindMod(release.Id);
                            if (modEntry != null) {
                                Version ver = ParseVersion(release.Version);
                                if (modEntry.Version < ver && (modEntry.NewestVersion == null || modEntry.NewestVersion < ver)) {
                                    modEntry.NewestVersion = ver;
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Logger.Log(string.Format("Error checking mod updates on '{0}'.", url));
                Logger.Log(e.Message);
            }
        }

        public static bool HasNetworkConnection() {
            try {
                using (System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping()) {
                    return ping.Send("www.google.com.mx", 2000).Status == IPStatus.Success;
                }
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }

            return false;
        }

        private static IEnumerator DownloadString(string url, UnityAction<string, string> handler) {
            UnityWebRequest www = UnityWebRequest.Get(url);

            yield return www.SendWebRequest();

            MethodInfo isError;

            Version ver = ParseVersion(Application.unityVersion);
            isError = ver.Major >= 2017 ? typeof(UnityWebRequest).GetMethod("get_isNetworkError") : typeof(UnityWebRequest).GetMethod("get_isError");

            if (isError == null || (bool) isError.Invoke(www, null)) {
                Logger.Log(www.error);
                Logger.Log(string.Format("Error downloading '{0}'.", url));
                yield break;
            }

            handler(www.downloadHandler.text, url);
        }
    }
}
