using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Every colour and metric of the skill tree screens (Almanac now, Heritage in S5). One asset in
    /// Resources/TreeTheme so the visual pass can restyle without touching code. No UI colour is hard-coded.
    /// </summary>
    [CreateAssetMenu(menuName = "Till Winter/Tree Theme", fileName = "TreeTheme")]
    public sealed class TreeTheme : ScriptableObject
    {
        [Header("Almanac page")]
        public Color Paper = new Color(0.97f, 0.93f, 0.84f, 0.93f);
        public Color PaperVignette = new Color(0.62f, 0.5f, 0.34f, 0.35f);
        public Color Ink = new Color(0.16f, 0.12f, 0.08f);
        public Color InkMuted = new Color(0.45f, 0.4f, 0.34f);
        public Color Overlay = new Color(0.05f, 0.08f, 0.14f, 0.55f);
        public Color Accent = new Color(0.95f, 0.62f, 0.2f);
        public Color Danger = new Color(0.8f, 0.25f, 0.2f);
        public Color Gold = new Color(0.95f, 0.78f, 0.25f);
        public Color Coin = new Color(1f, 0.82f, 0.2f);
        public Color Seed = new Color(0.62f, 0.42f, 0.85f);

        [Header("Branches")]
        public Color Hand = new Color(0.95f, 0.55f, 0.2f);
        public Color Soil = new Color(0.6f, 0.4f, 0.22f);
        public Color Field = new Color(0.35f, 0.65f, 0.3f);
        public Color Helpers = new Color(0.3f, 0.55f, 0.85f);
        public Color Calendar = new Color(0.6f, 0.4f, 0.8f);

        [Header("Nodes")]
        public float NodeSize = 112f;
        public float UnitPixels = 108f;
        public float LockedSaturation = 0.25f;
        public float LockedAlpha = 0.75f;
        public float EdgeWidth = 6f;
        public Color EdgeDim = new Color(0.55f, 0.48f, 0.4f, 0.45f);
        public float PulseAmplitude = 0.05f;
        public float ZoomMin = 0.5f;
        public float ZoomMax = 1.6f;
        public float InitialZoom = 0.85f;

        public Color BranchColor(Branch b)
        {
            switch (b)
            {
                case Branch.Hand: return Hand;
                case Branch.Soil: return Soil;
                case Branch.Field: return Field;
                case Branch.Helpers: return Helpers;
                default: return Calendar;
            }
        }

        public static Color Desaturate(Color c, float saturation)
        {
            float grey = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return new Color(Mathf.Lerp(grey, c.r, saturation), Mathf.Lerp(grey, c.g, saturation), Mathf.Lerp(grey, c.b, saturation), c.a);
        }

        private static TreeTheme _loaded;

        /// <summary>The theme asset from Resources, or a default instance when it is missing.</summary>
        public static TreeTheme Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<TreeTheme>("TreeTheme");
            if (_loaded == null) _loaded = CreateInstance<TreeTheme>();
            return _loaded;
        }
    }
}
