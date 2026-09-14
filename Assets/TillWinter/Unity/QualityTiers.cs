using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    public enum QualityTier { Low, Default }

    /// <summary>
    /// Two tiers: Low (no shadows, no post-processing) and Default (soft shadows, colour-adjust volume).
    /// Auto-selects Low on devices below a memory/GPU threshold; the debug panel can toggle it.
    /// </summary>
    public static class QualityTiers
    {
        public static QualityTier Current { get; private set; } = QualityTier.Default;

        private static Light _sun;
        private static Camera _cam;

        public static void Init(Light sun, Camera cam)
        {
            _sun = sun;
            _cam = cam;
            Apply(AutoSelect());
        }

        /// <summary>Low when the device reports under 3 GB RAM or under 1 GB of GPU memory (0 = unknown, treated as fine).</summary>
        public static QualityTier AutoSelect()
        {
            if (!Application.isMobilePlatform) return QualityTier.Default;
            int ram = SystemInfo.systemMemorySize;
            int vram = SystemInfo.graphicsMemorySize;
            bool low = (ram > 0 && ram < 3072) || (vram > 0 && vram < 1024) || SystemInfo.graphicsShaderLevel < 35;
            return low ? QualityTier.Low : QualityTier.Default;
        }

        public static void Apply(QualityTier tier)
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
            QualitySettings.antiAliasing = low ? 0 : 2;
        }

        public static void Toggle() => Apply(Current == QualityTier.Low ? QualityTier.Default : QualityTier.Low);
    }
}
