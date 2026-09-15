using System.IO;
using NUnit.Framework;
using TillWinter.Unity;
using UnityEngine;

namespace TillWinter.Tests.Unity
{
    /// <summary>settings.json round trip, language resolution, and reduce motion gating shake and flash (S9).</summary>
    public class SettingsTests
    {
        private SettingsData _previous;

        [SetUp] public void Remember() => _previous = SettingsStore.Current;
        [TearDown] public void Restore() => SettingsStore.Override(_previous);

        [Test]
        public void Settings_RoundTrip_ThroughTheFile()
        {
            var d = new SettingsData { Language = "tr", ReduceMotion = true, HapticsEnabled = false, QualityTier = 0, SfxVolume = 0.3f, AmbienceVolume = 0.6f };
            string path = Path.Combine(Path.GetTempPath(), "tw-settings-test.json");
            File.WriteAllText(path, JsonUtility.ToJson(d));
            try
            {
                var back = SettingsStore.Load(path);
                Assert.IsNotNull(back);
                Assert.AreEqual("tr", back.Language);
                Assert.IsTrue(back.ReduceMotion);
                Assert.IsFalse(back.HapticsEnabled);
                Assert.AreEqual(0, back.QualityTier);
                Assert.AreEqual(0.3f, back.SfxVolume, 1e-5f);
                Assert.AreEqual(0.6f, back.AmbienceVolume, 1e-5f);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void OldOrUnknownLanguage_FallsBackToTheSystemLanguage()
        {
            string path = Path.Combine(Path.GetTempPath(), "tw-settings-test-old.json");
            File.WriteAllText(path, "{\"Version\":1,\"HapticsEnabled\":true,\"QualityTier\":-1,\"Language\":\"de\"}");
            try
            {
                var back = SettingsStore.Load(path);
                Assert.AreEqual("", back.Language);
                Assert.IsFalse(back.ReduceMotion, "a pre-S9 file has motion on");
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Language_FollowsTheSystem_UnlessChosen()
        {
            Assert.AreEqual(GameLanguage.Turkish, GameLanguage.Resolve("", SystemLanguage.Turkish));
            Assert.AreEqual(GameLanguage.English, GameLanguage.Resolve("", SystemLanguage.German));
            Assert.AreEqual(GameLanguage.English, GameLanguage.Resolve("en", SystemLanguage.Turkish));
            Assert.AreEqual(GameLanguage.Turkish, GameLanguage.Resolve("tr", SystemLanguage.English));
        }

        [Test]
        public void ReduceMotion_DisablesCameraShake_AndScreenFlash()
        {
            var rigGo = new GameObject("rig");
            var hudGo = new GameObject("hud");
            try
            {
                var rig = rigGo.AddComponent<CameraRig>();
                var hud = hudGo.AddComponent<HudView>();

                SettingsStore.Override(new SettingsData { ReduceMotion = true });
                rig.Shake(0.1f, 5f);
                hud.Flash(0.1f, 5f);
                Assert.IsFalse(rig.Shaking, "no shake with reduce motion");
                Assert.IsFalse(hud.Flashing, "no flash with reduce motion");

                SettingsStore.Override(new SettingsData { ReduceMotion = false });
                rig.Shake(0.1f, 5f);
                hud.Flash(0.1f, 5f);
                Assert.IsTrue(rig.Shaking);
                Assert.IsTrue(hud.Flashing);
            }
            finally
            {
                Object.DestroyImmediate(rigGo);
                Object.DestroyImmediate(hudGo);
            }
        }
    }
}
