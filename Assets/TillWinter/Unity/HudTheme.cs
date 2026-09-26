using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Colours and spacing of the year-screen HUD. One asset in Resources/HudTheme; no HUD colour is hard-coded.</summary>
    [CreateAssetMenu(menuName = "Till Winter/HUD Theme", fileName = "HudTheme")]
    public sealed class HudTheme : ScriptableObject
    {
        /// <summary>Bumped when the look changes; UiSetup restyles an older asset in place (round three: one palette).</summary>
        public const int CurrentStyle = 4;
        public int StyleVersion;

        /// <summary>Cards that sit over the field (the away card).</summary>
        public Color CardDark = new Color(0.16f, 0.135f, 0.12f, 0.97f);

        /// <summary>The "this year" card at the bottom of the play screen.</summary>
        public Color YearCard = new Color(0.965f, 0.92f, 0.84f);
        public Color YearCardText = new Color(0.18f, 0.14f, 0.105f);
        public Color YearCardMuted = new Color(0.18f, 0.14f, 0.105f, 0.65f);
        /// <summary>A long streak warms the combo number toward this.</summary>
        public Color ComboHot = new Color(0.95f, 0.42f, 0.24f);


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
        /// <summary>Frost creeping in from the edges and the golden-harvest flash (the HUD animates their alpha from 0).</summary>
        public Color FrostEdge = new Color(0.8f, 0.9f, 1f, 1f);
        public Color Flash = new Color(1f, 1f, 1f, 1f);
        public Color SheetPaper = new Color(0.97f, 0.93f, 0.84f);
        public Color SheetInk = new Color(0.22f, 0.16f, 0.1f);
        public Color SheetMuted = new Color(0.22f, 0.16f, 0.1f, 0.65f);
        public Color SheetButton = new Color(0.36f, 0.55f, 0.3f);
        public Color SheetIdle = new Color(0.62f, 0.56f, 0.46f);
        public Color SheetButtonText = new Color(1f, 0.97f, 0.9f);
        public Color SheetDanger = new Color(0.72f, 0.28f, 0.2f);
        public Color CreditsOverlay = new Color(0.14f, 0.09f, 0.02f, 0.6f);
        public Color BootFade = new Color(0.97f, 0.93f, 0.84f, 1f);

        // Main menu
        public Color MenuShade = new Color(0.07f, 0.06f, 0.05f, 0.82f);
        public Color MenuTitle = new Color(1f, 0.95f, 0.82f);
        /// <summary>The second word of the title: a frosty tint, the winter the farm works towards.</summary>
        public Color MenuTitleFrost = new Color(0.84f, 0.93f, 1f);
        /// <summary>The sprout ornament between title and subtitle.</summary>
        public Color MenuOrnament = new Color(0.42f, 0.6f, 0.35f);
        public Color MenuSubtitle = new Color(1f, 0.93f, 0.78f, 0.85f);
        public Color MenuPrimary = new Color(0.38f, 0.6f, 0.3f);
        public Color MenuSecondary = new Color(0.1f, 0.08f, 0.06f, 0.72f);
        public Color MenuButtonText = new Color(1f, 0.97f, 0.9f);
        public float CoinFontSize = 96f;
        /// <summary>Clear space between the coin icon's rim and the first digit of the counter.</summary>
        public float CoinIconGap = 18f;
        public float SubFontSize = 38f;
        public float BarWidth = 960f;
        public float BarHeight = 18f;
        /// <summary>The season band sits below the island, not at the top: this is its height above the safe area's bottom.</summary>
        public float SeasonBandY = 540f;
        public float SeasonBandHeight = 132f;
        /// <summary>Bar and season name inside that band, measured down from its top edge.</summary>
        public float BarYInBand = -40f;
        public float SeasonNameYInBand = -76f;

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

        /// <summary>Round three: sheets, menu, hints and chips drawn from <see cref="UiPalette"/>, one family instead of four.</summary>
        public static void ApplyPalette(HudTheme t)
        {
            t.SheetPaper = UiPalette.Paper;
            t.SheetInk = UiPalette.Ink;
            t.SheetMuted = UiPalette.InkMuted;
            t.SheetButton = UiPalette.Sage;
            t.SheetIdle = UiPalette.Earth;
            t.SheetButtonText = UiPalette.Cream;
            t.SheetDanger = UiPalette.Brick;
            t.SheetOverlay = UiPalette.Dim;
            t.MenuPrimary = UiPalette.Sage;
            t.MenuSecondary = UiPalette.Earth; // was near-black and translucent: heavy against the sky
            t.MenuButtonText = UiPalette.Cream;
            // Style 3: the bottom shade was a heavy grey band under the buttons; a light warm veil is enough.
            t.MenuShade = UiPalette.WithAlpha(UiPalette.Night, 0.38f);
            t.MenuOrnament = UiPalette.Sage;
            t.BootFade = UiPalette.Paper;
            t.HintBackground = UiPalette.WithAlpha(UiPalette.Night, 0.88f);
            t.HintText = UiPalette.Cream;
            t.HintAccent = UiPalette.Honey;
            t.SeedChip = UiPalette.WithAlpha(UiPalette.Plum, 0.92f);
            t.CardDark = UiPalette.WithAlpha(UiPalette.Night, 0.97f);
            // Style 2: a thicker season timeline; the name moves down with it.
            t.BarHeight = 26f;
            // Style 4: the timeline sat on the island's bottom edge; the band and bar drop clear of it.
            t.SeasonBandY = 540f;
            t.BarYInBand = -40f;
            t.SeasonNameYInBand = -76f;
            t.StyleVersion = CurrentStyle;
        }

        public static HudTheme Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<HudTheme>("HudTheme");
            if (_loaded == null) _loaded = CreateInstance<HudTheme>();
            return _loaded;
        }
    }
}
