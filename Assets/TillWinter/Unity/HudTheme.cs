using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Colours and spacing of the year-screen HUD. One asset in Resources/HudTheme; no HUD colour is hard-coded.</summary>
    [CreateAssetMenu(menuName = "Till Winter/HUD Theme", fileName = "HudTheme")]
    public sealed class HudTheme : ScriptableObject
    {
        [Header("Text")]
        public Color Text = new Color(1f, 0.97f, 0.9f);
        public Color TextMuted = new Color(1f, 0.97f, 0.9f, 0.8f);
        public Color Coin = new Color(1f, 0.82f, 0.2f);
        /// <summary>Golden-harvest coins in flight.</summary>
        public Color Gold = new Color(1f, 0.92f, 0.5f);
        public Color CoinInner = new Color(0.85f, 0.6f, 0.1f);
        public Color Combo = new Color(1f, 0.9f, 0.5f);
        public Color Seed = new Color(0.78f, 0.6f, 0.95f);
        public Color SeedChip = new Color(0.35f, 0.2f, 0.5f, 0.85f);

        [Header("Season bar")]
        public Color Spring = new Color(0.55f, 0.8f, 0.4f);
        public Color Summer = new Color(0.95f, 0.85f, 0.35f);
        public Color Autumn = new Color(0.92f, 0.55f, 0.25f);
        public Color Winter = new Color(0.7f, 0.82f, 0.95f);
        public Color Frost = new Color(0.4f, 0.6f, 1f);
        public Color BarBackground = new Color(0f, 0f, 0f, 0.35f);
        public Color BarElapsed = new Color(0.1f, 0.08f, 0.06f, 0.5f);
        public Color BarMarker = new Color(1f, 0.97f, 0.9f);

        [Header("Hints")]
        public Color HintBackground = new Color(0.1f, 0.08f, 0.06f, 0.8f);
        public Color HintText = new Color(1f, 0.97f, 0.9f);
        public Color HintAccent = new Color(0.95f, 0.62f, 0.2f);

        [Header("Spacing (reference 1080 wide)")]
        public float TopPadding = 40f;

        // Sheets (pause, settings, credits, stats) and the ending roll (S9)
        public Color PauseButton = new Color(0f, 0f, 0f, 0.35f);
        public Color SheetOverlay = new Color(0.08f, 0.06f, 0.04f, 0.7f);
        public Color SheetPaper = new Color(0.97f, 0.93f, 0.84f);
        public Color SheetInk = new Color(0.22f, 0.16f, 0.1f);
        public Color SheetMuted = new Color(0.22f, 0.16f, 0.1f, 0.65f);
        public Color SheetButton = new Color(0.36f, 0.55f, 0.3f);
        public Color SheetIdle = new Color(0.62f, 0.56f, 0.46f);
        public Color SheetButtonText = new Color(1f, 0.97f, 0.9f);
        public Color SheetDanger = new Color(0.72f, 0.28f, 0.2f);
        public Color CreditsOverlay = new Color(0.14f, 0.09f, 0.02f, 0.6f);
        public Color BootFade = new Color(0.97f, 0.93f, 0.84f, 1f);
        public float CoinFontSize = 96f;
        public float SubFontSize = 38f;
        public float BarWidth = 960f;
        public float BarHeight = 18f;
        public float BarY = -330f;
        public float SeasonNameY = -380f;

        public Color SeasonColor(Season s)
        {
            switch (s)
            {
                case Season.Spring: return Spring;
                case Season.Summer: return Summer;
                case Season.Autumn: return Autumn;
                default: return Winter;
            }
        }

        private static HudTheme _loaded;

        public static HudTheme Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<HudTheme>("HudTheme");
            if (_loaded == null) _loaded = CreateInstance<HudTheme>();
            return _loaded;
        }
    }
}
