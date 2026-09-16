using System;
using System.IO;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Player settings (not game state): feel and audio flags. Saved separately from the game save so the save schema is untouched.</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public int Version = 1;
        public bool HapticsEnabled = true;
        /// <summary>-1 = auto-select on first launch, then remembered (0 Low, 1 Default).</summary>
        public int QualityTier = -1;
        public float MasterVolume = 1f;
        public float SfxVolume = 1f;
        public float AmbienceVolume = 1f;
        /// <summary>"" follows the system language (Turkish -> tr, anything else -> en); "en" / "tr" once chosen in Settings.</summary>
        public string Language = "";
        /// <summary>Disables camera shake and the screen flash only (S9).</summary>
        public bool ReduceMotion;
        /// <summary>Larger text everywhere (the UiType scale multiplier).</summary>
        public bool LargeText;
    }

    /// <summary>The two shipped languages. Strings and the number style switch together.</summary>
    public static class GameLanguage
    {
        public const string English = "en", Turkish = "tr";
        public static string Current { get; private set; } = English;

        /// <summary>An explicit choice wins; otherwise Turkish devices get Turkish and everything else English.</summary>
        public static string Resolve(string setting, SystemLanguage system)
        {
            if (setting == English || setting == Turkish) return setting;
            return system == SystemLanguage.Turkish ? Turkish : English;
        }

        public static void Apply(string lang, TextAsset en, TextAsset tr)
        {
            Current = lang == Turkish && tr != null ? Turkish : English;
            Strings.Load(Current == Turkish ? tr : en);
            Core.NumberFormat.Style = Current == Turkish ? Core.NumberStyle.Turkish : Core.NumberStyle.English;
        }
    }

    /// <summary>settings.json next to the save file; same atomic write and never-throw rules as SaveController.</summary>
    public static class SettingsStore
    {
        public const string FileName = "settings.json";
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        private static SettingsData _current;

        /// <summary>Camera shake and screen flash check this (reduce motion).</summary>
        public static bool MotionAllowed => !Current.ReduceMotion;

        public static SettingsData Current
        {
            get
            {
                if (_current == null) _current = Load(Path) ?? new SettingsData();
                return _current;
            }
        }

        public static void Save()
        {
            try
            {
                string path = Path;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Current, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TillWinter] Could not write settings: " + e.Message);
            }
        }

        public static SettingsData Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<SettingsData>(json);
                if (data == null || data.Version < 1) return null;
                data.MasterVolume = Mathf.Clamp01(data.MasterVolume);
                data.SfxVolume = Mathf.Clamp01(data.SfxVolume);
                data.AmbienceVolume = Mathf.Clamp01(data.AmbienceVolume);
                if (data.Language != GameLanguage.English && data.Language != GameLanguage.Turkish) data.Language = "";
                UiType.Scale = data.LargeText ? UiType.LargeScale : 1f;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TillWinter] Could not read settings: " + e.Message);
                return null;
            }
        }

        /// <summary>Tests and the debug panel: swap the in-memory settings without touching disk.</summary>
        public static void Override(SettingsData data) => _current = data;
    }
}
