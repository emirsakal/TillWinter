using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace TillWinter.EditorTools.Build
{
    /// <summary>
    /// The only place player settings, version numbers and builds are made (build-android.bat, build-ios.bat,
    /// release-compile-check.bat). Settings functions are idempotent; build numbers only move in Bump*.
    /// Keystores and signing identities never touch the repo: the Android keystore comes from environment variables
    /// for the duration of one build, iOS signing happens in Xcode.
    /// </summary>
    public static class BuildPipeline
    {
        public const string ProductName = "Till Winter";
        public const string CompanyName = "EFS Games";
        public const string BundleId = "com.efsgames.tillwinter";
        public const string Version = "1.0.0";
        public const string ScenePath = "Assets/TillWinter/Scenes/Farm.unity";
        public const string DebugDefine = "TW_DEBUG";
        /// <summary>The brief asked for 24; Unity 6000.3's lowest supported level is 25 (Android 7.1). API 25 still takes the pre-26 haptics fallback.</summary>
        public const int AndroidMinSdk = 25;
        public const string IosMinVersion = "15.0";

        public const string EnvKeystorePath = "TW_KEYSTORE_PATH";
        public const string EnvKeystorePass = "TW_KEYSTORE_PASS";
        public const string EnvKeyAlias = "TW_KEY_ALIAS";
        public const string EnvKeyPass = "TW_KEY_PASS";

        private const string PaletteAsset = "Assets/TillWinter/Unity/Resources/Palette.asset";
        private const string IosPlugin = "Assets/Plugins/iOS/TillWinterHaptics.mm";

        // ------------------------------------------------------------------ settings (idempotent)

        public static void ApplyCommonSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true; // Personal licence
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            var palette = AssetDatabase.LoadAssetAtPath<Palette>(PaletteAsset);
            if (palette != null) PlayerSettings.SplashScreen.backgroundColor = palette.LeafDark;
        }

        private static void ApplyScripting(NamedBuildTarget t)
        {
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(t, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Medium);
            PlayerSettings.SetApplicationIdentifier(t, BundleId);
            // TW_DEBUG never lives in the project defines; development builds pass it per build (extraScriptingDefines).
            var defines = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(d => d.Length > 0 && d != DebugDefine).ToArray();
            string joined = string.Join(";", defines);
            if (joined != PlayerSettings.GetScriptingDefineSymbols(t)) PlayerSettings.SetScriptingDefineSymbols(t, joined);
        }

        public static void ApplyAndroidSettings()
        {
            ApplyCommonSettings();
            ApplyScripting(NamedBuildTarget.Android);
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            // Set last: Unity re-validates the minimum when the target SDK / architectures change, so an earlier
            // assignment could be bumped to its own default (25) on the first pass.
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)AndroidMinSdk;
            if ((int)PlayerSettings.Android.minSdkVersion != AndroidMinSdk)
                Debug.LogWarning("[Build] Android min SDK is " + (int)PlayerSettings.Android.minSdkVersion + ", expected " + AndroidMinSdk);
            // A custom keystore is only switched on for the duration of a signed build (see BuildAndroidPlayers).
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keyaliasName = "";
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
        }

        public static void ApplyIosSettings()
        {
            ApplyCommonSettings();
            ApplyScripting(NamedBuildTarget.iOS);
            PlayerSettings.iOS.targetOSVersionString = IosMinVersion;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            // Swipes from the bottom edge need a second swipe, so a harvest sweep near the home indicator is not interrupted.
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
            PlayerSettings.iOS.cameraUsageDescription = "";
            PlayerSettings.iOS.microphoneUsageDescription = "";
            PlayerSettings.iOS.locationUsageDescription = "";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int n) || n < 1) PlayerSettings.iOS.buildNumber = "1";
            ConfigureIosPlugin();
        }

        /// <summary>The Objective-C++ haptics bridge compiles only into the iOS player.</summary>
        private static void ConfigureIosPlugin()
        {
            var imp = AssetImporter.GetAtPath(IosPlugin) as PluginImporter;
            if (imp == null) return;
            bool ok = !imp.GetCompatibleWithAnyPlatform() && !imp.GetCompatibleWithEditor() && imp.GetCompatibleWithPlatform(BuildTarget.iOS)
                      && !imp.GetCompatibleWithPlatform(BuildTarget.Android);
            if (ok) return;
            imp.SetCompatibleWithAnyPlatform(false);
            imp.SetCompatibleWithEditor(false);
            imp.SetCompatibleWithPlatform(BuildTarget.Android, false);
            imp.SetCompatibleWithPlatform(BuildTarget.iOS, true);
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ versions (the only place numbers move)

        public static int BumpAndroidVersionCode()
        {
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode) + 1;
            AssetDatabase.SaveAssets();
            return PlayerSettings.Android.bundleVersionCode;
        }

        public static int BumpIosBuildNumber()
        {
            int.TryParse(PlayerSettings.iOS.buildNumber, out int n);
            n = Math.Max(1, n) + 1;
            PlayerSettings.iOS.buildNumber = n.ToString();
            AssetDatabase.SaveAssets();
            return n;
        }

        // ------------------------------------------------------------------ entry points (batchmode)

        public static void BuildAndroid() => Exit(() => BuildAndroidPlayers(HasArg("-twDev")));
        public static void BuildIos() => Exit(() => BuildIosProject(HasArg("-twDev")));
        public static void CompileReleaseCheck() => Exit(CompileRelease);

        private static void Exit(Func<bool> body)
        {
            int code = 1;
            try { code = body() ? 0 : 1; }
            catch (Exception e) { Debug.LogError("[Build] failed: " + e); }
            EditorApplication.Exit(code);
        }

        public static bool BuildAndroidPlayers(bool dev)
        {
            ApplyAndroidSettings();
            IconRenderer.EnsureIcons(HasArg("-twIcons"));
            int build = BumpAndroidVersionCode();
            WriteBuildInfo("Android", build, dev);
            string dir = OutputDir("Android", build);
            string baseName = "TillWinter-" + Version + "-" + build + (dev ? "-dev" : "");

            string ks = Env(EnvKeystorePath), ksPass = Env(EnvKeystorePass), alias = Env(EnvKeyAlias), aliasPass = Env(EnvKeyPass);
            bool signed = !string.IsNullOrEmpty(ks) && File.Exists(ks) && !string.IsNullOrEmpty(ksPass) && !string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(aliasPass);
            bool ok = true;
            try
            {
                if (signed)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = ks;
                    PlayerSettings.Android.keystorePass = ksPass;
                    PlayerSettings.Android.keyaliasName = alias;
                    PlayerSettings.Android.keyaliasPass = aliasPass;
                    ok &= BuildOne(BuildTarget.Android, Path.Combine(dir, baseName + ".aab"), dev, true);
                    ok &= BuildOne(BuildTarget.Android, Path.Combine(dir, baseName + ".apk"), dev, false);
                }
                else
                {
                    ok &= BuildOne(BuildTarget.Android, Path.Combine(dir, baseName + ".apk"), dev, false);
                    PrintKeystoreHelp();
                }
            }
            finally
            {
                // Nothing about the keystore survives the build: the project settings on disk stay keystore-free.
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
            }
            Log("Android " + Version + " (" + build + ") " + (signed ? "signed with the keystore from the environment" : "debug-signed APK for sideloading") + " -> " + dir);
            return ok;
        }

        public static bool BuildIosProject(bool dev)
        {
            ApplyIosSettings();
            IconRenderer.EnsureIcons(HasArg("-twIcons"));
            int build = BumpIosBuildNumber();
            WriteBuildInfo("iOS", build, dev);
            string dir = OutputDir("iOS", build);
            string xcode = Path.Combine(dir, "Xcode");
            bool ok = BuildOne(BuildTarget.iOS, xcode, dev, false);
            if (ok)
            {
                Log("iOS Xcode project -> " + xcode);
                Log("Next (on a Mac): open Unity-iPhone.xcodeproj, pick your team under Signing & Capabilities, Product > Archive, then Distribute App.");
            }
            return ok;
        }

        private static bool BuildOne(BuildTarget target, string path, bool dev, bool appBundle)
        {
            if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = appBundle;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(target),
                options = dev ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = dev ? new[] { DebugDefine } : new string[0],
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            // BuildReport.totalSize counts intermediates (687 MB for a 32 MB APK): report what actually landed on disk.
            long bytes = File.Exists(path) ? new FileInfo(path).Length
                : Directory.Exists(path) ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            Log(target + " " + Path.GetFileName(path) + ": " + s.result + ", " + (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB on disk, "
                + s.totalTime.TotalSeconds.ToString("0") + " s, " + s.totalErrors + " errors");
            if (s.result != BuildResult.Succeeded)
                foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Log("  error: " + m.content.Split('\n')[0]);
            return s.result == BuildResult.Succeeded;
        }

        /// <summary>Release (no TW_DEBUG) player scripts compile for Android and iOS, and the debug-only types are gone.</summary>
        private static bool CompileRelease()
        {
            bool ok = true;
            foreach (var target in new[] { BuildTarget.Android, BuildTarget.iOS })
            {
                var settings = new ScriptCompilationSettings
                {
                    target = target,
                    group = UnityEditor.BuildPipeline.GetBuildTargetGroup(target),
                    options = ScriptCompilationOptions.None,
                    extraScriptingDefines = new string[0],
                };
                string outDir = Path.Combine("Temp", "TwReleaseCompile", target.ToString());
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                PlayerBuildInterface.CompilePlayerScripts(settings, outDir);
                string dll = Path.Combine(outDir, "TillWinter.Unity.dll");
                bool compiled = File.Exists(dll) && File.Exists(Path.Combine(outDir, "TillWinter.Core.dll"));
                string leaked = compiled ? DebugTypesIn(dll) : "";
                Log(target + " release scripts: " + (compiled ? "compiled" : "FAILED") + (compiled ? (leaked.Length == 0 ? ", debug panel and probes compiled out" : ", STILL CONTAINS " + leaked) : ""));
                ok &= compiled && leaked.Length == 0;
            }
            return ok;
        }

        /// <summary>Type names live in the assembly's metadata string heap; finding them means they were compiled in.</summary>
        public static string DebugTypesIn(string dllPath)
        {
            string text = Encoding.ASCII.GetString(File.ReadAllBytes(dllPath));
            var found = new[] { "DebugPanel", "FrameAllocStart", "FrameAllocEnd" }.Where(n => text.Contains("\0" + n + "\0")).ToArray();
            return string.Join(", ", found);
        }

        // ------------------------------------------------------------------ helpers

        private static void WriteBuildInfo(string platform, int build, bool dev)
        {
            var info = BuildInfoWriter.Create(platform, build, dev);
            string path = BuildInfoWriter.Write(info);
            AssetDatabase.ImportAsset(path);
            Log("build-info: " + info.Describe());
        }

        private static string OutputDir(string platform, int build)
        {
            string dir = Path.Combine("Builds", platform, Version + "-" + build);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void PrintKeystoreHelp()
        {
            Log("No keystore in the environment: built a debug-signed APK for sideloading only (no .aab).");
            Log("For a store build, create the keystore yourself: Edit > Project Settings > Player > Android > Publishing Settings > Keystore Manager.");
            Log("Keep it outside the repo, then set " + EnvKeystorePath + ", " + EnvKeystorePass + ", " + EnvKeyAlias + ", " + EnvKeyPass + " and rerun build-android.bat.");
        }

        private static string Env(string name) => Environment.GetEnvironmentVariable(name);
        private static bool HasArg(string arg) => Environment.GetCommandLineArgs().Any(a => string.Equals(a, arg, StringComparison.OrdinalIgnoreCase));
        private static void Log(string msg) => Debug.Log("[Build] " + msg);
    }

    /// <summary>StreamingAssets/build-info.json: version, build number, git hash, UTC date.</summary>
    public static class BuildInfoWriter
    {
        public const string Dir = "Assets/StreamingAssets";

        public static BuildInfo Create(string platform, int build, bool development) => new BuildInfo
        {
            Version = BuildPipeline.Version,
            Build = build,
            GitHash = GitHash(),
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC",
            Platform = platform,
            Development = development,
        };

        public static string Write(BuildInfo info, string dir = Dir)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, BuildInfo.FileName);
            File.WriteAllText(path, JsonUtility.ToJson(info, true));
            return path;
        }

        public static string GitHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string s = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    return string.IsNullOrEmpty(s) ? "unknown" : s;
                }
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
