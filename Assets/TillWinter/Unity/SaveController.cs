using System;
using System.Collections;
using System.IO;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Owns the save file (GDD §9): atomic write, one .bak, corrupt files renamed, never throws to the player.
    /// Triggers: Winter start, every purchase, Retire, StartNewGeneration, Next Year, pause/focus loss/quit,
    /// and an autosave every 30 s during the year.
    /// </summary>
    public sealed class SaveController : MonoBehaviour
    {
        public const string FileName = "tillwinter.json";
        public float AutosaveSeconds = 30f;

        public static SaveController Instance { get; private set; }
        public string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);
        public string LastResult { get; private set; } = "";

        private GameController _game;
        private bool _hooked;

        private void Awake()
        {
            Instance = this;
        }

        public void Attach(GameController game)
        {
            _game = game;
            var sim = game.Sim;
            sim.WinterStarted += SaveNow;
            sim.Purchased += _ => SaveNow();
            sim.Retired += _ => SaveNow();
            sim.GenerationStarted += SaveNow;
            sim.YearStarted += SaveNow;
            _hooked = true;
            StartCoroutine(Autosave());
        }

        private IEnumerator Autosave()
        {
            var wait = new WaitForSecondsRealtime(AutosaveSeconds);
            while (true)
            {
                yield return wait;
                if (_game != null && _game.State.Phase == Phase.Year) SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();

        public void SaveNow()
        {
#if UNITY_EDITOR || TW_DEBUG
            FrameAlloc.IgnoreThisFrame(); // JsonUtility allocates by design; saves are events, not per-frame work
#endif
            if (!_hooked || _game == null || _game.Sim == null) return;
            try
            {
                var data = _game.Sim.ToSave();
                data.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Write(Path, JsonUtility.ToJson(data));
                LastResult = "saved " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception e)
            {
                LastResult = "save failed: " + e.Message;
                Debug.LogWarning("[TillWinter] Save failed: " + e.Message);
            }
        }

        /// <summary>Atomic: write .tmp, rotate current to .bak, move .tmp into place.</summary>
        private static void Write(string path, string json)
        {
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
        }

        /// <summary>Loads the save, falling back to .bak; corrupt files are renamed and null is returned (start fresh).</summary>
        public static SaveData Load(string path)
        {
            var data = TryRead(path);
            if (data != null) return data;
            var bak = TryRead(path + ".bak");
            if (bak != null)
            {
                Debug.LogWarning("[TillWinter] Main save unreadable, using .bak");
                return bak;
            }
            if (File.Exists(path))
            {
                try
                {
                    File.Move(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                    Debug.LogWarning("[TillWinter] Save file corrupt; renamed and starting fresh");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[TillWinter] Could not rename corrupt save: " + e.Message);
                }
            }
            return null;
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.SchemaVersion < 1 || data.Plots == null || data.Plots.Length == 0) return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TillWinter] Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        /// <summary>Reset save: stop every save trigger so nothing writes the old farm back before the reload.</summary>
        public void Detach() => _hooked = false;

        public void DeleteSave()
        {
            foreach (var p in new[] { Path, Path + ".bak", Path + ".tmp" })
            {
                try { if (File.Exists(p)) File.Delete(p); }
                catch (Exception e) { Debug.LogWarning("[TillWinter] Could not delete " + p + ": " + e.Message); }
            }
            LastResult = "save deleted";
        }
    }
}
