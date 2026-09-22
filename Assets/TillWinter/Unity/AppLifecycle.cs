using System;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Device lifecycle: 60 fps in-year and 30 fps while the Winter screen is open (nothing moves but the tree),
    /// audio paused in the background, resume after an interruption (offline simulation only for absences of at
    /// least FarmConfig.OfflineMinSeconds — Core enforces it), and the cold-start measurement.
    /// The save on pause is SaveController's job; the year timer is naturally paused because nothing ticks in the background.
    /// </summary>
    public sealed class AppLifecycle : MonoBehaviour
    {
        public const int YearFps = 60;
        public const int WinterFps = 30;

        /// <summary>Seconds from process start to the first frame the game rendered (-1 until then).</summary>
        public static float ColdStartSeconds { get; private set; } = -1f;
        /// <summary>Seconds spent inside GameBootstrap.Awake (catalogue loads, pools, UI build).</summary>
        public static float BootstrapSeconds { get; private set; }

        private static float _bootStart;

        private GameController _game;
        private AudioManager _audio;
        private AwayCard _away;
        private WinterScreen _winter;
        private DateTime? _pausedAt;
        private bool _firstFrame = true;

        public static void MarkBootStart() => _bootStart = Time.realtimeSinceStartup;
        public static void MarkBootEnd() => BootstrapSeconds = Time.realtimeSinceStartup - _bootStart;

        public void Init(GameController game, AudioManager audio, AwayCard away, WinterScreen winter)
        {
            _game = game;
            _audio = audio;
            _away = away;
            _winter = winter;
            StartCoroutine(BuildInfo.Load());
            Application.lowMemory += OnLowMemory;
            Reminders.Cancel(); // launched: whatever was booked for the time away is moot
            if (game != null && game.Sim != null) game.Sim.Retired += _ => ReviewPrompt.MaybeAsk(game.State);
        }

        private void OnDestroy() => Application.lowMemory -= OnLowMemory;

        /// <summary>The system is about to kill something: save first, then hand back what the pools no longer hold.</summary>
        private static void OnLowMemory()
        {
            SaveController.Instance?.SaveNow();
            Resources.UnloadUnusedAssets();
            Debug.Log("[TillWinter] Low memory: saved and unloaded unused assets");
        }

        private void Update()
        {
            if (_firstFrame)
            {
                _firstFrame = false;
                ColdStartSeconds = Time.realtimeSinceStartup;
                Debug.Log("[TillWinter] Cold start " + ColdStartSeconds.ToString("0.00") + " s (bootstrap " + BootstrapSeconds.ToString("0.00") + " s)");
            }
            int fps = _winter != null && _winter.IsOpen ? WinterFps : YearFps;
            if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
        }

        /// <summary>
        /// Opt-in (Settings → Reminders): one notification when the offline cap runs out, and only if something on
        /// the farm works on its own — a field with nothing passive has nothing to report.
        /// </summary>
        private void BookReminder()
        {
            if (!SettingsStore.Current.Reminders || _game == null || _game.Sim == null) return;
            var st = _game.State;
            if (st.Phase != Phase.Year || st.IsDaily) return;
            var stats = st.Stats;
            bool passive = stats.IrrigationFactor > 0f || stats.SunFactor > 0f || stats.ApprenticeCount > 0 || stats.TractorLevel > 0;
            if (!passive) return;
            Reminders.Schedule(_game.Sim.Config.OfflineCapSeconds, Strings.Get("reminder.title"), Strings.Get("reminder.body"));
        }

        /// <summary>Desktop and the editor never pause: losing focus should still quiet the game.</summary>
        private void OnApplicationFocus(bool focused)
        {
            if (Application.isMobilePlatform) return; // mobile uses OnApplicationPause, which also simulates the time away
            AudioListener.pause = !focused;
            if (!focused) SettingsStore.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _pausedAt = DateTime.UtcNow;
                AudioListener.pause = true;
                SettingsStore.Save(); // a volume dragged on the settings sheet survives the app being killed in the background
                BookReminder();
                return;
            }
            AudioListener.pause = false;
            Reminders.Cancel();
            if (!_pausedAt.HasValue || _game == null || _game.Sim == null) return;
            double elapsed = Math.Max(0, (DateTime.UtcNow - _pausedAt.Value).TotalSeconds);
            _pausedAt = null;
            var report = _game.Sim.SimulateOffline(elapsed); // zero report below OfflineMinSeconds (a call, an app switch)
            if (report.SecondsSimulated > 0) Debug.Log("[TillWinter] Resumed after " + elapsed.ToString("0") + " s: +" + report.CoinsEarned.ToString("0") + " coins offline");
            if (report.CoinsEarned > 0 && _away != null) _away.Show(report);
        }
    }
}
