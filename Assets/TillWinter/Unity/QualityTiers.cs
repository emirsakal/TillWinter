using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    public enum QualityTier { Low, Default }

    /// <summary>
    /// Two tiers: Low (no shadows, no post-processing) and Default (soft shadows, colour-adjust volume).
    /// Auto-selected once on a phone from RAM / GPU memory / cores / shader level and remembered in settings.json;
    /// the debug panel toggles (and saves) it. Desktop and the Editor always run Default and never write the choice.
    /// </summary>
    public static class QualityTiers
    {
        public const int MinRamMb = 3072;
        public const int MinGpuMb = 1024;
        public const int MinCores = 6;
        public const int MinShaderLevel = 35;

        public static QualityTier Current { get; private set; } = QualityTier.Default;
        /// <summary>Why the current tier was chosen (debug panel).</summary>
        public static string Reason { get; private set; } = "";

        private static Light _sun;
        private static Camera _cam;

        public static void Init(Light sun, Camera cam)
        {
            _sun = sun;
            _cam = cam;
            var settings = SettingsStore.Current;
            if (Application.isMobilePlatform && settings.QualityTier >= 0)
            {
                Reason = "remembered";
                Apply((QualityTier)Mathf.Clamp(settings.QualityTier, 0, 1), false);
                return;
            }
            var tier = AutoSelect(SystemInfo.systemMemorySize, SystemInfo.graphicsMemorySize, SystemInfo.processorCount, SystemInfo.graphicsShaderLevel,
                Application.isMobilePlatform, Application.platform == RuntimePlatform.IPhonePlayer, out string reason);
            Reason = "auto: " + reason;
            Apply(tier, Application.isMobilePlatform);
        }

        /// <summary>
        /// Pure decision (tested): a phone with under 3 GB RAM, under 1 GB dedicated GPU memory (not checked on iOS,
        /// where GPU memory is shared), fewer than 6 cores or shader level below 3.5 runs Low. Unknown values (0)
        /// never count against a device; desktop is never auto-lowered.
        /// </summary>
        public static QualityTier AutoSelect(int ramMb, int gpuMb, int cores, int shaderLevel, bool mobile, bool sharedGpuMemory, out string reason)
        {
            if (!mobile) { reason = "desktop"; return QualityTier.Default; }
            if (ramMb > 0 && ramMb < MinRamMb) { reason = "RAM " + ramMb + " MB"; return QualityTier.Low; }
            if (!sharedGpuMemory && gpuMb > 0 && gpuMb < MinGpuMb) { reason = "GPU " + gpuMb + " MB"; return QualityTier.Low; }
            if (cores > 0 && cores < MinCores) { reason = cores + " cores"; return QualityTier.Low; }
            if (shaderLevel > 0 && shaderLevel < MinShaderLevel) { reason = "shader level " + shaderLevel; return QualityTier.Low; }
            reason = "RAM " + ramMb + " MB, GPU " + gpuMb + " MB, " + cores + " cores";
            return QualityTier.Default;
        }

        public static void Apply(QualityTier tier, bool remember)
        {
            Current = tier;
            bool low = tier == QualityTier.Low;
            if (_sun != null) _sun.shadows = low ? LightShadows.None : LightShadows.Soft;
            if (_cam != null)
            {
                var data = _cam.GetUniversalAdditionalCameraData();
                data.renderShadows = !low;
                data.renderPostProcessing = !low;
            }
            // MSAA stays on the URP asset (2x); changing QualitySettings at runtime would also dirty the project in the editor.
            if (remember && SettingsStore.Current.QualityTier != (int)tier)
            {
                SettingsStore.Current.QualityTier = (int)tier;
                SettingsStore.Save();
            }
        }

        public static void Toggle()
        {
            Reason = "debug toggle";
            Apply(Current == QualityTier.Low ? QualityTier.Default : QualityTier.Low, true);
        }
    }
}
