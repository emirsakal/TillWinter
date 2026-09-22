using System.IO;
using System.Xml;
using NUnit.Framework;
using TillWinter.EditorTools.Build;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using TwBuild = TillWinter.EditorTools.Build.BuildPipeline;

namespace TillWinter.Tests.Unity
{
    /// <summary>Build pipeline guards: link.xml keeps Core, settings are idempotent, build-info round-trips, quality thresholds.</summary>
    public class BuildTests
    {
        [Test]
        public void LinkXml_PreservesCore()
        {
            var doc = new XmlDocument();
            doc.Load("Assets/link.xml");
            var node = doc.SelectSingleNode("/linker/assembly[@fullname='TillWinter.Core']");
            Assert.IsNotNull(node, "TillWinter.Core listed in link.xml");
            Assert.AreEqual("all", node.Attributes["preserve"]?.Value);
        }

        [Test]
        public void ApplySettings_IsIdempotent_AndNeverBumpsNumbers()
        {
            TwBuild.ApplyAndroidSettings();
            TwBuild.ApplyIosSettings();
            string first = Snapshot();
            int code = PlayerSettings.Android.bundleVersionCode;
            string ios = PlayerSettings.iOS.buildNumber;
            TwBuild.ApplyAndroidSettings();
            TwBuild.ApplyIosSettings();
            Assert.AreEqual(first, Snapshot());
            Assert.AreEqual(code, PlayerSettings.Android.bundleVersionCode, "only Bump* moves the version code");
            Assert.AreEqual(ios, PlayerSettings.iOS.buildNumber);

            Assert.AreEqual(TwBuild.BundleId, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            Assert.AreEqual(TwBuild.BundleId, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS));
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            Assert.AreEqual(ManagedStrippingLevel.Medium, PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
            Assert.AreEqual(TwBuild.AndroidMinSdk, (int)PlayerSettings.Android.minSdkVersion, "lowest level Unity 6000.3 supports");
            Assert.AreEqual(TwBuild.AndroidTargetSdk, (int)PlayerSettings.Android.targetSdkVersion, "target level pinned, never Auto");
            Assert.That(TwBuild.AndroidTargetSdk, Is.GreaterThanOrEqualTo(35), "Play's floor for new apps");
            Assert.IsFalse(PlayerSettings.Android.forceInternetPermission, "no INTERNET permission: the game is offline");
            Assert.IsTrue(PlayerSettings.Android.renderOutsideSafeArea, "edge to edge; the HUD keeps to Screen.safeArea itself");
            Assert.IsFalse(PlayerSettings.SplashScreen.show, "the studio mark opens the app, not the engine splash");
            Assert.IsFalse(PlayerSettings.muteOtherAudioSources, "the player's own music keeps playing");
            Assert.AreEqual("15.0", PlayerSettings.iOS.targetOSVersionString);
            Assert.IsTrue(PlayerSettings.iOS.requiresFullScreen);
            Assert.IsTrue(PlayerSettings.gcIncremental);
            Assert.IsFalse(PlayerSettings.Android.useCustomKeystore, "no keystore in project settings");
            Assert.IsEmpty(PlayerSettings.Android.keystoreName, "keystore path never stored in ProjectSettings");
            Assert.IsEmpty(PlayerSettings.Android.keyaliasName, "key alias never stored in ProjectSettings");
            StringAssert.DoesNotContain(TwBuild.DebugDefine, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android));
            StringAssert.DoesNotContain(TwBuild.DebugDefine, PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS));
        }

        private static string Snapshot() => string.Join("|",
            PlayerSettings.productName, PlayerSettings.companyName, PlayerSettings.bundleVersion,
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS),
            PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS),
            PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android), PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.iOS),
            PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Android), PlayerSettings.Android.minSdkVersion, PlayerSettings.Android.targetSdkVersion,
            PlayerSettings.Android.targetArchitectures, string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)),
            string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS)), PlayerSettings.iOS.targetOSVersionString, PlayerSettings.iOS.targetDevice,
            PlayerSettings.iOS.requiresFullScreen, PlayerSettings.iOS.deferSystemGesturesMode, PlayerSettings.gcIncremental,
            PlayerSettings.defaultInterfaceOrientation, PlayerSettings.SplashScreen.backgroundColor,
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android), PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS));

        [Test]
        public void BuildInfo_WriterRoundTrips()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tw-buildinfo-test");
            var info = new BuildInfo { Version = "0.9.0", Build = 42, GitHash = "abc1234", Date = "2026-09-15 10:00 UTC", Platform = "Android", Development = false };
            string path = BuildInfoWriter.Write(info, dir);
            Assert.AreEqual(BuildInfo.FileName, Path.GetFileName(path));
            var back = JsonUtility.FromJson<BuildInfo>(File.ReadAllText(path));
            Assert.AreEqual("0.9.0", back.Version);
            Assert.AreEqual(42, back.Build);
            Assert.AreEqual("abc1234", back.GitHash);
            Assert.AreEqual("Android", back.Platform);
            Assert.IsFalse(back.Development);
            Directory.Delete(dir, true);
        }

        [Test]
        public void BuildInfo_Create_UsesThePipelineVersion_AndAGitHash()
        {
            var info = BuildInfoWriter.Create("iOS", 7, true);
            Assert.AreEqual(TwBuild.Version, info.Version);
            Assert.AreEqual(7, info.Build);
            Assert.IsTrue(info.Development);
            Assert.IsFalse(string.IsNullOrEmpty(info.GitHash));
        }

        [Test]
        public void QualityAutoSelect_UsesMemoryCoresAndGpuThresholds()
        {
            // iPhone 12: 4 GB, 6 cores, shared GPU memory (iOS reports no dedicated VRAM worth checking).
            Assert.AreEqual(QualityTier.Default, QualityTiers.AutoSelect(3800, 0, 6, 50, true, true, out _));
            // Older Android: 2 GB RAM.
            Assert.AreEqual(QualityTier.Low, QualityTiers.AutoSelect(2048, 1024, 8, 45, true, false, out string reason));
            StringAssert.Contains("RAM", reason);
            Assert.AreEqual(QualityTier.Low, QualityTiers.AutoSelect(4096, 512, 8, 45, true, false, out _), "small GPU memory");
            Assert.AreEqual(QualityTier.Low, QualityTiers.AutoSelect(4096, 2048, 4, 45, true, false, out _), "four cores");
            Assert.AreEqual(QualityTier.Default, QualityTiers.AutoSelect(6144, 2048, 8, 50, true, false, out _));
            Assert.AreEqual(QualityTier.Default, QualityTiers.AutoSelect(1024, 128, 2, 20, false, false, out _), "desktop is never auto-lowered");
            Assert.AreEqual(QualityTier.Default, QualityTiers.AutoSelect(0, 0, 0, 0, true, false, out _), "unknown values never count against a device");
        }
    }
}
