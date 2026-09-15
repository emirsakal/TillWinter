using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TillWinter.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace TillWinter.Tests.Unity
{
    /// <summary>Feel pass guards: every VfxId and SfxId has a catalogue entry, no generated audio fallback, licences cover every clip, mixer groups exist.</summary>
    public class FeelTests
    {
        [Test]
        public void EveryVfxId_HasAPooledParticlePrefab()
        {
            var catalog = Resources.Load<VfxCatalog>("VfxCatalog");
            Assert.IsNotNull(catalog, "Resources/VfxCatalog (run feel-setup.bat)");
            foreach (VfxId id in Enum.GetValues(typeof(VfxId)))
            {
                var e = catalog.Get(id);
                Assert.IsNotNull(e, id + " entry");
                Assert.IsNotNull(e.Prefab, id + " prefab");
                Assert.IsNotNull(e.Prefab.GetComponent<ParticleSystem>(), id + " particle system");
                Assert.That(e.MaxPerSecond, Is.GreaterThan(0), id + " limiter");
                var r = e.Prefab.GetComponent<ParticleSystemRenderer>();
                Assert.IsTrue(r.sharedMaterial != null && r.sharedMaterial.shader.name == "TillWinter/TW_Toon", id + " uses the toon material");
            }
            Assert.AreEqual(12, catalog.Get(VfxId.Harvest).MaxPerSecond, "harvest pop budget");
            Assert.AreEqual(10, catalog.Get(VfxId.WaterSplash).MaxPerSecond, "water splash budget");
        }

        [Test]
        public void EverySfxId_HasClips_AndTheyAllLoad()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                Assert.IsTrue(SfxTable.Rows.ContainsKey(id), id + " row");
                Assert.That(SfxTable.Rows[id].Clips.Length, Is.GreaterThan(0), id + " clips");
            }
            var missing = SfxTable.Missing();
            Assert.IsEmpty(missing, "clips missing from Resources/Kenney: " + string.Join(", ", missing));
            Assert.AreEqual(3, SfxTable.Rows[SfxId.HarvestPop].Clips.Length, "three harvest pop variants");
            Assert.AreEqual(20, SfxTable.Rows[SfxId.CoinArrive].MaxPerSecond, "coin budget");
        }

        [Test]
        public void NoGeneratedFallback_Remains()
        {
            var gen = typeof(AudioManager).GetMethod("Gen", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.IsNull(gen, "AudioManager.Gen (procedural clips) must be gone");
            var src = File.ReadAllText("Assets/TillWinter/Unity/AudioManager.cs");
            Assert.IsFalse(src.Contains("AudioClip.Create"), "no AudioClip.Create in AudioManager");
        }

        [Test]
        public void EveryAudioClipFile_HasALicenceEntry()
        {
            string index = File.ReadAllText("Assets/Audio/LICENSES.md");
            var unlisted = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string name = Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid));
                if (!index.Contains("`" + name + "`")) unlisted.Add(name);
            }
            Assert.IsEmpty(unlisted, "clips without a licence entry: " + string.Join(", ", unlisted));
            foreach (var lic in new[] { "License-ImpactSounds.txt", "License-InterfaceSounds.txt", "License-RpgAudio.txt", "License-UIAudio.txt" })
                Assert.IsTrue(File.Exists("Assets/Audio/Kenney/" + lic), lic);
        }

        [Test]
        public void Mixer_HasMasterSfxAmbience_WithExposedVolumes()
        {
            var mixer = Resources.Load<AudioMixer>(AudioManager.MixerName);
            Assert.IsNotNull(mixer, "Resources/" + AudioManager.MixerName + " (run feel-setup.bat)");
            Assert.That(mixer.FindMatchingGroups("Master").Length, Is.GreaterThan(0));
            Assert.That(mixer.FindMatchingGroups("SFX").Length, Is.GreaterThan(0));
            Assert.That(mixer.FindMatchingGroups("Ambience").Length, Is.GreaterThan(0));
            foreach (var p in new[] { "MasterVolume", "SfxVolume", "AmbienceVolume" })
                Assert.IsTrue(mixer.GetFloat(p, out _), p + " exposed");
        }

        [Test]
        public void Settings_RoundTrip_ThroughJson()
        {
            var data = new SettingsData { HapticsEnabled = false, SfxVolume = 0.4f };
            string path = Path.Combine(Path.GetTempPath(), "tw-settings-test.json");
            File.WriteAllText(path, JsonUtility.ToJson(data));
            var back = SettingsStore.Load(path);
            Assert.IsNotNull(back);
            Assert.IsFalse(back.HapticsEnabled);
            Assert.AreEqual(0.4f, back.SfxVolume, 1e-5f);
            File.Delete(path);
        }
    }
}
