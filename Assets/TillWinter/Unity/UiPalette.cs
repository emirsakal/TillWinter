using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The one colour family every screen draws from (visual review round three): warm earth, cream paper, sage
    /// green and honey, with brick for danger and plum for heritage seeds. Themes are restyled from these values by
    /// UiSetup; views still read their colours from HudTheme and TreeTheme, never from here directly.
    /// </summary>
    public static class UiPalette
    {
        /// <summary>Body text on paper; also the darkest line work.</summary>
        public static readonly Color Ink = new Color(0.18f, 0.14f, 0.105f);
        public static readonly Color InkMuted = new Color(0.18f, 0.14f, 0.105f, 0.65f);
        /// <summary>Sheets and dialogs: the farm notebook.</summary>
        public static readonly Color Paper = new Color(0.965f, 0.92f, 0.84f);
        /// <summary>Text on dark or coloured surfaces.</summary>
        public static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
        /// <summary>Primary action: growth.</summary>
        public static readonly Color Sage = new Color(0.42f, 0.6f, 0.35f);
        /// <summary>Highlights, progress, coins.</summary>
        public static readonly Color Honey = new Color(0.91f, 0.66f, 0.24f);
        /// <summary>Secondary actions: tilled soil and old wood.</summary>
        public static readonly Color Earth = new Color(0.6f, 0.48f, 0.36f);
        /// <summary>Destructive actions.</summary>
        public static readonly Color Brick = new Color(0.74f, 0.32f, 0.24f);
        /// <summary>Heritage and seeds.</summary>
        public static readonly Color Plum = new Color(0.55f, 0.42f, 0.74f);
        /// <summary>Dark surfaces: Winter page, cards over the field.</summary>
        public static readonly Color Night = new Color(0.16f, 0.135f, 0.12f);
        /// <summary>Behind a sheet: the world dimmed, not blacked out.</summary>
        public static readonly Color Dim = new Color(0.1f, 0.08f, 0.06f, 0.72f);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
