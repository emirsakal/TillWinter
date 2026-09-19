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
        /// <summary>Bumped when the page look changes; UiSetup restyles an older asset in place (S9: dark page, opaque overlay).</summary>
        public const int CurrentStyle = 6;
        public int StyleVersion;

        [Header("Almanac page")]
        public Color Paper = new Color(0.16f, 0.135f, 0.12f, 1f); // UiPalette.Night
        public Color PaperVignette = new Color(0f, 0f, 0f, 0.45f);
        public Color Ink = new Color(0.95f, 0.9f, 0.8f);
        public Color InkMuted = new Color(0.86f, 0.82f, 0.74f); // costs and levels on the dark page were barely above it
        public Color Overlay = new Color(0.09f, 0.08f, 0.06f, 1f);
        public Color Accent = new Color(0.91f, 0.66f, 0.24f); // UiPalette.Honey
        public Color Danger = new Color(0.74f, 0.32f, 0.24f); // UiPalette.Brick
        public Color Gold = new Color(0.95f, 0.78f, 0.25f);
        public Color Coin = new Color(1f, 0.82f, 0.2f);
        public Color Seed = new Color(0.55f, 0.42f, 0.74f); // UiPalette.Plum

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
        public Color EdgeDim = new Color(0.74f, 0.68f, 0.58f, 0.55f);
        /// <summary>The hub emblem and the unbought roots that leave it.</summary>
        public Color Trunk = new Color(0.55f, 0.42f, 0.3f, 1f);
        /// <summary>Levels and padlocks show only from this zoom up; further out a node is just its colour.</summary>
        public float DetailZoom = 0.55f;

        [Header("Winter page")]
        public Color Snowfall = new Color(0.95f, 0.96f, 1f, 0.14f);
        public Color Lamp = new Color(0.91f, 0.66f, 0.24f, 0.14f);
        public Color Stars = new Color(1f, 0.96f, 0.85f, 0.55f);
        /// <summary>Frost on the window: strong as Winter opens, then a thin frame that stays.</summary>
        public Color Frost = new Color(0.86f, 0.93f, 1f, 0.5f);
        /// <summary>Dims behind the hint caption and the confirm sheet, and the lighter one behind the retire hint.</summary>
        public Color Dim = new Color(0f, 0f, 0f, 0.6f);
        public Color DimLight = new Color(0f, 0f, 0f, 0.5f);
        /// <summary>What a node ring flashes toward when bought, and the colour of the edge flow lines.</summary>
        public Color Highlight = Color.white;
        /// <summary>The Heritage page's aged-paper tint (over the grain).</summary>
        public Color Aged = new Color(0.95f, 0.78f, 0.25f, 0.025f);
        public float PulseAmplitude = 0.05f;
        public float ZoomMin = 0.3f; // low enough that the opening view holds the whole Almanac canopy
        public float ZoomMax = 1.6f;
        public float InitialZoom = 0.52f; // the curved canopy reaches further out than the old lanes or the first star

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

        private static readonly System.Collections.Generic.Dictionary<string, TreeTheme> _loaded = new System.Collections.Generic.Dictionary<string, TreeTheme>();

        /// <summary>A theme asset from Resources ("TreeTheme" = Almanac, "HeritageTheme" = Heritage), or defaults when missing.</summary>
        public static TreeTheme Load(string name = "TreeTheme")
        {
            if (_loaded.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<TreeTheme>(name);
            if (t == null)
            {
                t = CreateInstance<TreeTheme>();
                if (name == "HeritageTheme") ApplyHeritageDefaults(t);
            }
            _loaded[name] = t;
            return t;
        }

        /// <summary>S9: the dark Almanac page (the cream page tired the eyes) and an opaque overlay so no sky shows.</summary>
        public static void ApplyAlmanacPage(TreeTheme t)
        {
            var d = CreateInstance<TreeTheme>();
            t.Paper = d.Paper;
            t.PaperVignette = d.PaperVignette;
            t.Ink = d.Ink;
            t.InkMuted = d.InkMuted;
            t.Overlay = d.Overlay;
            t.EdgeDim = d.EdgeDim;
            t.ZoomMin = d.ZoomMin;
            t.Accent = d.Accent;
            t.Danger = d.Danger;
            t.Seed = d.Seed;
            t.InitialZoom = d.InitialZoom; // the layout's reach changed, so the opening framing has to follow it
            DestroyImmediate(d);
        }

        /// <summary>Heirloom look: deep green page, gold accents, seed currency, darker branch colours.</summary>
        public static void ApplyHeritageDefaults(TreeTheme t)
        {
            t.Paper = new Color(0.09f, 0.2f, 0.14f, 0.97f);
            t.PaperVignette = new Color(0f, 0.05f, 0.02f, 0.5f);
            t.Ink = new Color(0.96f, 0.9f, 0.72f);
            t.InkMuted = new Color(0.9f, 0.86f, 0.72f);
            t.Overlay = new Color(0.03f, 0.08f, 0.05f, 1f);
            t.Accent = new Color(0.9f, 0.72f, 0.28f);
            t.Danger = new Color(0.95f, 0.45f, 0.35f);
            t.Gold = new Color(1f, 0.85f, 0.4f);
            t.Coin = new Color(0.78f, 0.6f, 0.95f);
            t.Seed = new Color(0.78f, 0.6f, 0.95f);
            t.Hand = new Color(0.75f, 0.42f, 0.14f);
            t.Soil = new Color(0.48f, 0.3f, 0.16f);
            t.Field = new Color(0.28f, 0.5f, 0.24f);
            t.InitialZoom = 0.5f; // show the whole canopy
            t.Helpers = new Color(0.22f, 0.42f, 0.66f);
            t.Calendar = new Color(0.46f, 0.3f, 0.62f);
            t.EdgeDim = new Color(0.7f, 0.76f, 0.66f, 0.55f);
        }
    }
}
