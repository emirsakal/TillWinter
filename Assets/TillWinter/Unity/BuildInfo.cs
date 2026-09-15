using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TillWinter.Unity
{
    /// <summary>
    /// Version, build number, git hash and date of the running player. Written into StreamingAssets/build-info.json
    /// by the build pipeline; absent in the Editor. Shown in the debug panel (credits screen comes later).
    /// </summary>
    [Serializable]
    public sealed class BuildInfo
    {
        public const string FileName = "build-info.json";

        public string Version = "";
        public int Build;
        public string GitHash = "";
        public string Date = "";
        public string Platform = "";
        public bool Development;

        public static BuildInfo Current { get; private set; }

        public string Describe() => "v" + Version + " (" + Build + ") " + GitHash + "  " + Date + (Development ? "  dev" : "");

        public static string Summary => Current != null ? Current.Describe() : "editor build (no build-info.json)";

        /// <summary>StreamingAssets is inside the APK on Android (needs a web request); a plain file elsewhere.</summary>
        public static IEnumerator Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            string json = null;
            if (path.Contains("://"))
            {
                using (var req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
                }
            }
            else if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            if (string.IsNullOrEmpty(json)) yield break;
            try { Current = JsonUtility.FromJson<BuildInfo>(json); }
            catch (Exception) { Current = null; }
        }
    }
}
