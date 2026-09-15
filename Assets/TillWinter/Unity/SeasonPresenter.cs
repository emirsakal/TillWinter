using TillWinter.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>
    /// Seasons through light and colour only, all values from <see cref="SeasonPalette"/>: one directional light,
    /// gradient ambient, fog, colour-adjust volume, sky gradient, frost vignette, snow particles, and the
    /// leaf / grass / snow tints pushed to every <see cref="PaletteBinder"/>.
    /// </summary>
    public sealed class SeasonPresenter : MonoBehaviour
    {
        public float BlendSeconds = 1.5f;

        private GameController _game;
        private SeasonPalette _palette;
        private Light _sun;
        private ColorAdjustments _color;
        private Vignette _vignette;
        private VfxPlayer _fx;
        private Season _fromSeason, _toSeason;
        private Material _skyMaterial;
        private Transform _sky;
        private Camera _cam;

        private SeasonLook _from, _to, _current;
        private float _blend = 1f;
        private Season _season;

        private static readonly int SkyTopId = Shader.PropertyToID("_Top");
        private static readonly int SkyBottomId = Shader.PropertyToID("_Bottom");

        public Light Sun => _sun;

        public void Init(GameController game, Camera cam, VisualCatalog catalog, VfxPlayer fx)
        {
            _game = game;
            _cam = cam;
            _fx = fx;
            _palette = SeasonPalette.Load();
            _game.Sim.GenerationStarted += OnGenerationStarted;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.6f;
            _sun.shadowBias = 0.05f;
            _sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            // Sky: gradient quad parented to the camera, behind everything.
            _skyMaterial = catalog.Sky != null ? new Material(catalog.Sky) : null;
            if (_skyMaterial != null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Sky";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(cam.transform, false);
                quad.transform.localPosition = new Vector3(0f, 0f, cam.farClipPlane - 1f);
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = _skyMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _sky = quad.transform;
            }

            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(transform, false);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = profile.Add<ColorAdjustments>(true);
            _color.colorFilter.overrideState = true;
            _color.colorFilter.value = Color.white;
            _color.saturation.overrideState = true;
            _color.saturation.value = 6f;
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0f;
            _vignette.color.overrideState = true;
            _vignette.color.value = new Color(0.55f, 0.75f, 1f);
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.7f;
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.profile = profile;

            _season = _game.State.Season;
            _fromSeason = _toSeason = _season;
            _from = _to = _current = _palette.For(_season, _game.State.GoldenYearActive);
            Apply(_current, 0f);

            _game.Sim.SeasonChanged += OnSeasonChanged;
            _game.Sim.GoldenYearStarted += OnGoldenYear;
        }

        private void OnGoldenYear()
        {
            _from = _current;
            _to = _palette.For(_season, true);
            _blend = 0f;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) { _game.Sim.SeasonChanged -= OnSeasonChanged; _game.Sim.GenerationStarted -= OnGenerationStarted; _game.Sim.GoldenYearStarted -= OnGoldenYear; }
        }

        /// <summary>New generation: the snow melts at once instead of lingering for a particle lifetime.</summary>
        private void OnGenerationStarted() => _fx.Clear(VfxId.Snow);

        private void OnSeasonChanged(Season s)
        {
            _fromSeason = _toSeason;
            _toSeason = s;
            _season = s;
            _from = _current;
            _to = _palette.For(s, _game.State.GoldenYearActive);
            _blend = 0f;
        }

        private void LateUpdate()
        {
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + Time.deltaTime / BlendSeconds);
                float t = Mathf.SmoothStep(0f, 1f, _blend);
                _current = SeasonLook.Lerp(_from, _to, t);
            }
            var state = _game.State;
            float frost = 0f;
            if (state.FrostWarning && !state.IsWinter)
                frost = Mathf.Clamp01(1f - state.SecondsUntilWinter / Mathf.Max(0.01f, _game.State.Stats.FrostWarningSeconds));

            Apply(_current, frost);

            // Ambience: one particle system per season, cross-faded by the same blend as the light.
            float blend = Mathf.SmoothStep(0f, 1f, _blend);
            float wFrom = 1f - blend, wTo = blend;
            float petals = (_fromSeason == Season.Spring ? wFrom : 0f) + (_toSeason == Season.Spring ? wTo : 0f);
            float leaves = (_fromSeason == Season.Autumn ? wFrom : 0f) + (_toSeason == Season.Autumn ? wTo : 0f);
            float snow = (_fromSeason == Season.Winter ? wFrom : 0f) + (_toSeason == Season.Winter ? wTo : 0f);
            bool year = state.Phase == Phase.Year;
            bool golden = state.GoldenYearActive;
            _fx.SetRate(VfxId.Petals, year && !golden ? 5f * petals : 0f);
            _fx.SetRate(VfxId.GoldMotes, year && golden ? 14f : 0f);
            _fx.SetRate(VfxId.Leaves, year ? 9f * leaves : 0f);
            _fx.SetRate(VfxId.Snow, state.IsWinter ? 70f : Mathf.Max(70f * snow, frost * 12f));

            if (_sky != null)
            {
                // Cover the orthographic frustum with margin.
                float h = _cam.orthographicSize * 2f * 1.2f;
                _sky.localScale = new Vector3(h * _cam.aspect, h, 1f);
            }
        }

        private void Apply(SeasonLook look, float frost)
        {
            var cold = new Color(0.75f, 0.85f, 1f);
            _sun.color = Color.Lerp(look.Light, cold, frost * 0.8f);
            _sun.intensity = look.Intensity * (1f - frost * 0.25f);
            _sun.transform.rotation = Quaternion.Euler(look.Angle);
            RenderSettings.ambientSkyColor = Color.Lerp(look.AmbientSky, cold, frost * 0.4f);
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            RenderSettings.fogColor = look.FogColor;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;
            _color.colorFilter.value = Color.Lerp(look.ColorFilter, cold, frost * 0.5f);
            _vignette.intensity.value = frost * 0.35f + (_game.State.IsWinter ? 0.25f : 0f);
            if (_skyMaterial != null)
            {
                _skyMaterial.SetColor(SkyTopId, Color.Lerp(look.SkyTop, cold, frost * 0.3f));
                _skyMaterial.SetColor(SkyBottomId, look.SkyBottom);
            }
            else if (_cam != null) _cam.backgroundColor = look.SkyBottom;
            float snow = Mathf.Max(look.SnowAmount, frost * 0.35f);
            PaletteBinder.SetSeason(snow, Color.Lerp(look.LeafTint, cold, frost * 0.3f), look.GrassTint);
        }
    }
}
