using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Everything the light, sky, fog and foliage do per season. Lerped by <see cref="SeasonPresenter"/>.</summary>
    [System.Serializable]
    public struct SeasonLook
    {
        [Header("Directional light")]
        public Color Light;
        public float Intensity;
        public Vector3 Angle;
        [Header("Ambient (gradient)")]
        public Color AmbientSky, AmbientEquator, AmbientGround;
        [Header("Fog")]
        public Color FogColor;
        public float FogStart, FogEnd;
        [Header("Tints")]
        public Color GrassTint, LeafTint, ColorFilter;
        [Header("Sky gradient")]
        public Color SkyTop, SkyBottom;
        [Range(0f, 1f)] public float SnowAmount;

        public static SeasonLook Lerp(SeasonLook a, SeasonLook b, float t) => new SeasonLook
        {
            Light = Color.Lerp(a.Light, b.Light, t),
            Intensity = Mathf.Lerp(a.Intensity, b.Intensity, t),
            Angle = Vector3.Lerp(a.Angle, b.Angle, t),
            AmbientSky = Color.Lerp(a.AmbientSky, b.AmbientSky, t),
            AmbientEquator = Color.Lerp(a.AmbientEquator, b.AmbientEquator, t),
            AmbientGround = Color.Lerp(a.AmbientGround, b.AmbientGround, t),
            FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t),
            FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
            GrassTint = Color.Lerp(a.GrassTint, b.GrassTint, t),
            LeafTint = Color.Lerp(a.LeafTint, b.LeafTint, t),
            ColorFilter = Color.Lerp(a.ColorFilter, b.ColorFilter, t),
            SkyTop = Color.Lerp(a.SkyTop, b.SkyTop, t),
            SkyBottom = Color.Lerp(a.SkyBottom, b.SkyBottom, t),
            SnowAmount = Mathf.Lerp(a.SnowAmount, b.SnowAmount, t),
        };
    }

    /// <summary>Four looks, one asset (Resources/SeasonPalette). Winter tints materials white through the binder rather than swapping meshes.</summary>
    [CreateAssetMenu(menuName = "Till Winter/Season Palette", fileName = "SeasonPalette")]
    public sealed class SeasonPalette : ScriptableObject
    {
        public SeasonLook Spring = new SeasonLook
        {
            Light = new Color(1f, 0.98f, 0.9f), Intensity = 1.15f, Angle = new Vector3(52f, -30f, 0f),
            AmbientSky = new Color(0.62f, 0.76f, 0.9f), AmbientEquator = new Color(0.6f, 0.68f, 0.56f), AmbientGround = new Color(0.36f, 0.42f, 0.3f),
            FogColor = new Color(0.78f, 0.88f, 0.86f), FogStart = 34f, FogEnd = 60f,
            GrassTint = new Color(1f, 1f, 1f), LeafTint = new Color(1f, 1f, 1f), ColorFilter = new Color(0.96f, 1f, 0.94f),
            SkyTop = new Color(0.55f, 0.76f, 0.95f), SkyBottom = new Color(0.86f, 0.94f, 0.88f), SnowAmount = 0f,
        };
        public SeasonLook Summer = new SeasonLook
        {
            Light = new Color(1f, 0.95f, 0.8f), Intensity = 1.35f, Angle = new Vector3(70f, -20f, 0f),
            AmbientSky = new Color(0.7f, 0.78f, 0.9f), AmbientEquator = new Color(0.7f, 0.66f, 0.5f), AmbientGround = new Color(0.4f, 0.38f, 0.26f),
            FogColor = new Color(0.9f, 0.9f, 0.78f), FogStart = 34f, FogEnd = 60f,
            GrassTint = new Color(1.05f, 0.98f, 0.8f), LeafTint = new Color(1f, 0.98f, 0.85f), ColorFilter = new Color(1f, 0.97f, 0.88f),
            SkyTop = new Color(0.45f, 0.7f, 0.98f), SkyBottom = new Color(0.95f, 0.93f, 0.78f), SnowAmount = 0f,
        };
        public SeasonLook Autumn = new SeasonLook
        {
            Light = new Color(1f, 0.78f, 0.52f), Intensity = 1.05f, Angle = new Vector3(32f, -45f, 0f),
            AmbientSky = new Color(0.8f, 0.66f, 0.5f), AmbientEquator = new Color(0.62f, 0.5f, 0.38f), AmbientGround = new Color(0.36f, 0.28f, 0.2f),
            FogColor = new Color(0.9f, 0.76f, 0.6f), FogStart = 30f, FogEnd = 56f,
            GrassTint = new Color(1.1f, 0.85f, 0.55f), LeafTint = new Color(1.3f, 0.75f, 0.35f), ColorFilter = new Color(1f, 0.88f, 0.74f),
            SkyTop = new Color(0.72f, 0.6f, 0.62f), SkyBottom = new Color(0.98f, 0.82f, 0.62f), SnowAmount = 0f,
        };
        public SeasonLook Winter = new SeasonLook
        {
            Light = new Color(0.8f, 0.88f, 1f), Intensity = 0.85f, Angle = new Vector3(26f, -45f, 0f),
            AmbientSky = new Color(0.7f, 0.78f, 0.92f), AmbientEquator = new Color(0.62f, 0.7f, 0.86f), AmbientGround = new Color(0.5f, 0.56f, 0.68f),
            FogColor = new Color(0.8f, 0.86f, 0.95f), FogStart = 26f, FogEnd = 50f,
            GrassTint = new Color(0.9f, 0.92f, 1f), LeafTint = new Color(0.8f, 0.86f, 0.95f), ColorFilter = new Color(0.86f, 0.92f, 1f),
            SkyTop = new Color(0.55f, 0.62f, 0.78f), SkyBottom = new Color(0.86f, 0.9f, 0.96f), SnowAmount = 1f,
        };

        /// <summary>The Golden Year (GDD §8): warm low sun, amber sky, gilded grass.</summary>
        public SeasonLook Golden = new SeasonLook
        {
            Light = new Color(1f, 0.86f, 0.55f), Intensity = 1.25f, Angle = new Vector3(38f, -35f, 0f),
            AmbientSky = new Color(0.95f, 0.8f, 0.55f), AmbientEquator = new Color(0.85f, 0.7f, 0.45f), AmbientGround = new Color(0.45f, 0.35f, 0.2f),
            FogColor = new Color(1f, 0.85f, 0.6f), FogStart = 32f, FogEnd = 58f,
            GrassTint = new Color(1.15f, 0.95f, 0.6f), LeafTint = new Color(1.2f, 0.95f, 0.5f), ColorFilter = new Color(1f, 0.92f, 0.75f),
            SkyTop = new Color(0.95f, 0.72f, 0.45f), SkyBottom = new Color(1f, 0.92f, 0.7f), SnowAmount = 0f,
        };

        /// <summary>Every season of the Golden Year looks golden; its Winter is a normal Winter.</summary>
        public SeasonLook For(Season s, bool golden) => golden && s != Season.Winter ? Golden : For(s);

        public SeasonLook For(Season s)
        {
            switch (s)
            {
                case Season.Summer: return Summer;
                case Season.Autumn: return Autumn;
                case Season.Winter: return Winter;
                default: return Spring;
            }
        }

        private static SeasonPalette _loaded;

        public static SeasonPalette Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<SeasonPalette>("SeasonPalette");
            if (_loaded == null) _loaded = CreateInstance<SeasonPalette>();
            return _loaded;
        }
    }
}
