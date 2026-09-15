using System.Runtime.InteropServices;
using TillWinter.Core.Feel;
using UnityEngine;

namespace TillWinter.Unity
{
    public enum HapticKind { Light, Medium, Heavy, Selection }

    /// <summary>
    /// Haptics abstraction. Android: Vibrator + VibrationEffect with amplitude (API 26+, fixed-length fallback);
    /// iOS: UIImpactFeedbackGenerator / UISelectionFeedbackGenerator through Assets/Plugins/iOS/TillWinterHaptics.mm;
    /// Editor and other platforms: a console tag so the smoke test can see the moments fire.
    /// Light is rate-limited (8/s) and merged like VFX; the global flag lives in <see cref="SettingsData.HapticsEnabled"/>.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled => SettingsStore.Current.HapticsEnabled;
        /// <summary>Last kinds fired (ring buffer for tests/smoke); Editor only.</summary>
        public static int Fired { get; private set; }

        private static readonly RateLimiter LightLimiter = new RateLimiter(8, 0.5f, 2f);
        private static bool _lightPending;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TW_HapticImpact(int style, float intensity);
        [DllImport("__Internal")] private static extern void TW_HapticSelection();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk;
        private static bool _androidReady;

        private static void EnsureAndroid()
        {
            if (_androidReady) return;
            _androidReady = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] Android vibrator unavailable: " + e.Message); }
        }

        private static void AndroidVibrate(long ms, int amplitude)
        {
            EnsureAndroid();
            if (_vibrator == null) return;
            try
            {
                if (_sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var one = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", one);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] vibrate failed: " + e.Message); }
        }
#endif

        public static void Play(HapticKind kind)
        {
            if (!Enabled) return;
            if (kind == HapticKind.Light)
            {
                if (!LightLimiter.Request(Time.unscaledTimeAsDouble, out float intensity)) { _lightPending = true; return; }
                Fire(kind, intensity);
                return;
            }
            Fire(kind, 1f);
        }

        /// <summary>Once per frame: fires merged Light taps as one slightly stronger tap.</summary>
        public static void Poll()
        {
            if (!_lightPending) return;
            if (LightLimiter.Poll(Time.unscaledTimeAsDouble, out float intensity)) Fire(HapticKind.Light, intensity);
            if (LightLimiter.Pending == 0) _lightPending = false;
        }

        private static void Fire(HapticKind kind, float intensity)
        {
            Fired++;
#if UNITY_IOS && !UNITY_EDITOR
            if (kind == HapticKind.Selection) TW_HapticSelection();
            else TW_HapticImpact(kind == HapticKind.Light ? 0 : kind == HapticKind.Medium ? 1 : 2, Mathf.Clamp01(0.5f * intensity));
#elif UNITY_ANDROID && !UNITY_EDITOR
            switch (kind)
            {
                case HapticKind.Light: AndroidVibrate(12, Mathf.RoundToInt(Mathf.Clamp(70f * intensity, 40f, 160f))); break;
                case HapticKind.Medium: AndroidVibrate(25, 160); break;
                case HapticKind.Heavy: AndroidVibrate(45, 255); break;
                default: AndroidVibrate(8, 60); break;
            }
#else
            Debug.Log("[Haptics] " + kind + (intensity > 1.01f ? " x" + intensity.ToString("0.0") : ""));
#endif
        }
    }
}
