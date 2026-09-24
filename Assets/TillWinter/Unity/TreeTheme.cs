using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// Every colour and metric of the skill tree screens (Almanac and Heritage). One asset in Resources per tree
    /// ("TreeTheme", "HeritageTheme") so the visual pass can restyle without touching code. No UI colour is hard-coded.
    /// Style 8 (v3.4): the tree is a field seen from above on a winter-day page; the beds are the nodes.
    /// </summary>
    [CreateAssetMenu(menuName = "Till Winter/Tree Theme", fileName = "TreeTheme")]
    public sealed class TreeTheme : ScriptableObject
    {
        /// <summary>Bumped when the page look changes; UiSetup restyles an older asset in place.</summary>
        public const int CurrentStyle = 8;
        public int StyleVersion;

        [Header("Page")]
        /// <summary>The page behind the field: pale winter daylight for the Almanac, a lilac dusk for Heritage.</summary>
        public Color Paper = new Color(0.933f, 0.941f, 0.957f, 1f);
        /// <summary>Card faces (the node sheet, the confirm box) on the page.</summary>
        public Color Card = new Color(0.984f, 0.973f, 0.945f, 1f);
        public Color Ink = new Color(0.18f, 0.165f, 0.22f);
        public Color InkMuted = new Color(0.18f, 0.165f, 0.22f, 0.6f);
        public Color Overlay = new Color(0.933f, 0.941f, 0.957f, 1f);
        public Color Accent = new Color(0.894f, 0.663f, 0.235f); // the warm button
        public Color Danger = new Color(0.74f, 0.32f, 0.24f);
        public Color Gold = new Color(0.95f, 0.78f, 0.25f);
        public Color Coin = new Color(1f, 0.82f, 0.2f);
        public Color Seed = new Color(0.557f, 0.435f, 0.851f); // the lilac of the seed currency
        /// <summary>Text links (Heritage, hand the farm on) under the field.</summary>
        public Color Link = new Color(0.18f, 0.165f, 0.22f, 0.7f);

        [Header("Field")]
        /// <summary>The soil of the field and the snow rim around it.</summary>
        public Color FieldSoil = new Color(0.486f, 0.357f, 0.255f, 1f);
        public Color FieldSnow = new Color(0.973f, 0.98f, 0.992f, 1f);
        /// <summary>Furrows between beds; a furrow between two bought beds warms to <see cref="FurrowLit"/>.</summary>
        public Color Furrow = new Color(0.36f, 0.255f, 0.188f, 0.7f);
        public Color FurrowLit = new Color(0.94f, 0.72f, 0.376f, 0.9f);
        /// <summary>A bought bed has grown something; a maxed one is gold; an available one is tilled with a seed in it; a locked one is hard, cracked ground.</summary>
        public Color BedGrown = new Color(0.31f, 0.561f, 0.247f);
        public Color Crop = new Color(0.561f, 0.824f, 0.431f);
        public Color CropHighlight = new Color(0.718f, 0.925f, 0.592f);
        public Color CropMaxed = new Color(0.98f, 0.82f, 0.36f);
        public Color BedTilled = new Color(0.29f, 0.196f, 0.129f);
        public Color SeedDot = new Color(0.918f, 0.843f, 0.706f);
        public Color BedHard = new Color(0.655f, 0.529f, 0.396f, 0.95f);
        public Color Crack = new Color(0.545f, 0.424f, 0.306f);
        /// <summary>The branch names written on the field.</summary>
        public Color BranchLabel = new Color(0.984f, 0.922f, 0.824f, 0.6f);
        /// <summary>The outline around the selected bed and the flash a bought bed makes.</summary>
        public Color Highlight = new Color(0.984f, 0.953f, 0.902f);

        [Header("Branches")]
        public Color Hand = new Color(0.95f, 0.55f, 0.2f);
        public Color Soil = new Color(0.6f, 0.4f, 0.22f);
        public Color Field = new Color(0.35f, 0.65f, 0.3f);
        public Color Helpers = new Color(0.3f, 0.55f, 0.85f);
        public Color Calendar = new Color(0.6f, 0.4f, 0.8f);

        [Header("Metrics (layout units)")]
        /// <summary>Bed sizes for a branch root, a node with children and a leaf.</summary>
        public float BedRoot = 0.78f;
        public float BedMid = 0.64f;
        public float BedLeaf = 0.54f;
        /// <summary>Furrow width as a fraction of a unit.</summary>
        public float FurrowWidth = 0.07f;
        /// <summary>Snow rim around the soil and the soil's inset from the tree canvas, in pixels.</summary>
        public float SnowRim = 8f;
        public float FieldPadding = 20f;
        /// <summary>Kept for the tree view's zoom report: one layout unit at the theme's reference scale.</summary>
        public float UnitPixels = 108f;
        public float PulseAmplitude = 0.04f;

        [Header("Dims")]
        /// <summary>Dims behind the hint caption and the confirm sheet, and the lighter one behind the retire hint.</summary>
        public Color Dim = new Color(0.18f, 0.165f, 0.22f, 0.55f);
        public Color DimLight = new Color(0.18f, 0.165f, 0.22f, 0.4f);

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
            else if (t.StyleVersion < CurrentStyle)
            {
                // An asset from before the field look: restyle the loaded copy so the screen never mixes the two.
                if (name == "HeritageTheme") ApplyHeritageDefaults(t); else ApplyFieldDefaults(t);
            }
            _loaded[name] = t;
            return t;
        }

        /// <summary>Style 8: the Almanac field on a winter-day page. Everything the asset carries is reset to the defaults.</summary>
        public static void ApplyFieldDefaults(TreeTheme t)
        {
            var d = CreateInstance<TreeTheme>();
            CopyFrom(t, d);
            DestroyImmediate(d);
            t.StyleVersion = CurrentStyle;
        }

        /// <summary>Heritage: the same field at dusk. Lilac page, cooler soil, golden-wheat beds for what is owned, seeds as the currency.</summary>
        public static void ApplyHeritageDefaults(TreeTheme t)
        {
            var d = CreateInstance<TreeTheme>();
            CopyFrom(t, d);
            DestroyImmediate(d);
            t.Paper = new Color(0.902f, 0.882f, 0.925f, 1f);
            t.Overlay = t.Paper;
            t.Card = new Color(0.984f, 0.973f, 0.945f, 1f);
            t.FieldSoil = new Color(0.42f, 0.31f, 0.286f, 1f);
            t.FieldSnow = new Color(0.965f, 0.945f, 0.98f, 1f);
            t.Furrow = new Color(0.302f, 0.216f, 0.2f, 0.75f);
            t.FurrowLit = new Color(0.851f, 0.659f, 0.235f, 0.9f);
            t.BedGrown = new Color(0.788f, 0.604f, 0.204f);
            t.Crop = new Color(0.945f, 0.824f, 0.478f);
            t.CropHighlight = new Color(1f, 0.941f, 0.722f);
            t.CropMaxed = new Color(1f, 0.941f, 0.722f);
            t.BedTilled = new Color(0.247f, 0.173f, 0.161f);
            t.SeedDot = new Color(0.788f, 0.706f, 0.961f);
            t.BedHard = new Color(0.616f, 0.518f, 0.467f, 0.95f);
            t.Crack = new Color(0.518f, 0.412f, 0.369f);
            t.BranchLabel = new Color(0.953f, 0.902f, 0.941f, 0.6f);
            t.Coin = new Color(0.557f, 0.435f, 0.851f);
            // Branch colours a shade deeper than the Almanac's: the two trees must never be told apart by the page alone.
            t.Hand = new Color(0.75f, 0.42f, 0.14f);
            t.Soil = new Color(0.48f, 0.3f, 0.16f);
            t.Field = new Color(0.28f, 0.5f, 0.24f);
            t.Helpers = new Color(0.22f, 0.42f, 0.66f);
            t.Calendar = new Color(0.46f, 0.3f, 0.62f);
            t.StyleVersion = CurrentStyle;
        }

        private static void CopyFrom(TreeTheme t, TreeTheme d)
        {
            t.Paper = d.Paper; t.Card = d.Card; t.Ink = d.Ink; t.InkMuted = d.InkMuted; t.Overlay = d.Overlay;
            t.Accent = d.Accent; t.Danger = d.Danger; t.Gold = d.Gold; t.Coin = d.Coin; t.Seed = d.Seed; t.Link = d.Link;
            t.FieldSoil = d.FieldSoil; t.FieldSnow = d.FieldSnow; t.Furrow = d.Furrow; t.FurrowLit = d.FurrowLit;
            t.BedGrown = d.BedGrown; t.Crop = d.Crop; t.CropHighlight = d.CropHighlight; t.CropMaxed = d.CropMaxed;
            t.BedTilled = d.BedTilled; t.SeedDot = d.SeedDot; t.BedHard = d.BedHard; t.Crack = d.Crack;
            t.BranchLabel = d.BranchLabel; t.Highlight = d.Highlight;
            t.Hand = d.Hand; t.Soil = d.Soil; t.Field = d.Field; t.Helpers = d.Helpers; t.Calendar = d.Calendar;
            t.BedRoot = d.BedRoot; t.BedMid = d.BedMid; t.BedLeaf = d.BedLeaf; t.FurrowWidth = d.FurrowWidth;
            t.SnowRim = d.SnowRim; t.FieldPadding = d.FieldPadding; t.UnitPixels = d.UnitPixels; t.PulseAmplitude = d.PulseAmplitude;
            t.Dim = d.Dim; t.DimLight = d.DimLight;
        }
    }
}
