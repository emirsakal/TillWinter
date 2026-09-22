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
    /// Signing stays in Xcode with the developer. Also writes the app's PrivacyInfo.xcprivacy: no tracking, no data
    /// collected, and the two required-reason APIs a save file and a settings file touch (UserDefaults, file
    /// timestamps). Unity's own manifest covers the engine's use; App Store Connect wants the app's as well.
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
            WritePrivacyManifest(path);
        }

        private const string PrivacyFile = "PrivacyInfo.xcprivacy";

        private static void WritePrivacyManifest(string path)
        {
            File.WriteAllText(Path.Combine(path, PrivacyFile), PrivacyManifest);
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string guid = proj.FindFileGuidByProjectPath(PrivacyFile) ?? proj.AddFile(PrivacyFile, PrivacyFile);
            proj.AddFileToBuild(proj.GetUnityMainTargetGuid(), guid);
            proj.WriteToFile(projPath);
            Debug.Log("[Build] " + PrivacyFile + ": no tracking, no collected data, UserDefaults CA92.1 + file timestamp C617.1");
        }

        private const string PrivacyManifest =
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
"<plist version=\"1.0\">\n<dict>\n" +
"  <key>NSPrivacyTracking</key><false/>\n" +
"  <key>NSPrivacyTrackingDomains</key><array/>\n" +
"  <key>NSPrivacyCollectedDataTypes</key><array/>\n" +
"  <key>NSPrivacyAccessedAPITypes</key>\n  <array>\n" +
"    <dict>\n      <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryUserDefaults</string>\n" +
"      <key>NSPrivacyAccessedAPITypeReasons</key><array><string>CA92.1</string></array>\n    </dict>\n" +
"    <dict>\n      <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryFileTimestamp</string>\n" +
"      <key>NSPrivacyAccessedAPITypeReasons</key><array><string>C617.1</string></array>\n    </dict>\n" +
"  </array>\n</dict>\n</plist>\n";
    }
}
#endif
