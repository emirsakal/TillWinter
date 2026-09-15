#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace TillWinter.EditorTools.Build
{
    /// <summary>
    /// Info.plist after Unity generates the Xcode project: full screen required (no iPad split view), portrait only,
    /// no ATS exceptions, no camera/microphone/location usage strings, no export-compliance prompt (no custom crypto).
    /// Signing stays in Xcode with the developer.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("UIRequiresFullScreen", true);
            var orientations = root.CreateArray("UISupportedInterfaceOrientations");
            orientations.AddString("UIInterfaceOrientationPortrait");
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            foreach (var key in new[] { "NSAppTransportSecurity", "NSCameraUsageDescription", "NSMicrophoneUsageDescription", "NSLocationWhenInUseUsageDescription", "NSLocationAlwaysAndWhenInUseUsageDescription" })
                if (root.values.ContainsKey(key)) root.values.Remove(key);
            plist.WriteToFile(plistPath);
            Debug.Log("[Build] Info.plist: UIRequiresFullScreen, portrait only, no ATS exceptions, no usage strings");
        }
    }
}
#endif
