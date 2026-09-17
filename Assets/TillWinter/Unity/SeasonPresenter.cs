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
        private Bloom _bloom;
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
        private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        private static readonly int SunPosId = Shader.PropertyToID("_SunPos");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int CloudsId = Shader.PropertyToID("_Clouds");
        private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
        private static readonly int HazeId = Shader.PropertyToID("_Haze");
        /// <summary>Reduce motion stops the foliage sway (TW_Toon).</summary>
        public static readonly int CalmId = Shader.PropertyToID("_TW_Calm");
        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonPosId = Shader.PropertyToID("_MoonPos");
        private static readonly int RainbowId = Shader.PropertyToID("_Rainbow");
        private static readonly int GustId = Shader.PropertyToID("_TW_Gust");
        private static readonly int RimTintId = Shader.PropertyToID("_TW_RimTint");
        /// <summary>Seconds a rainbow stays after the rain cloud is tapped.</summary>
        private const float RainbowSeconds = 9f;
        private float _rainbow;
        private float _dusk;

        /// <summary>Morning mist at the start of each year, burnt off over a few seconds.</summary>
        private const float MistSeconds = 7f;
        private float _mist = 1f;
        private float _snow;
        private float _clouds;

        public Light Sun => _sun;

        public void Init(GameController game, Camera cam, VisualCatalog catalog, VfxPlayer fx)
        {
            _game = game;
            _cam = cam;
            _fx = fx;
            _palette = SeasonPalette.Load();
            _game.Sim.GenerationStarted += OnGenerationStarted;
            _game.Sim.YearStarted += OnYearStarted;
            _game.Sim.RainCloudTapped += OnRain;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.72f;
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
            // Ripe glow, golden harvests, node purchases and the Golden Year are all emissive: let them bleed a little.
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 0.9f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 0.55f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.6f;
            _bloom.tint.overrideState = true;
            _bloom.tint.value = new Color(1f, 0.96f, 0.88f);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.profile = profile;

            _season = _game.State.Season;
            _fromSeason = _toSeason = _season;
            _from = _to = _current = _palette.For(_season, _game.State.GoldenYearActive);
            _snow = _current.SnowAmount;
            _clouds = CloudsFor(_season);
            if (_game.State.YearTime > 5f) _mist = 0f;
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
            if (_game != null && _game.Sim != null) { _game.Sim.SeasonChanged -= OnSeasonChanged; _game.Sim.GenerationStarted -= OnGenerationStarted; _game.Sim.YearStarted -= OnYearStarted; _game.Sim.GoldenYearStarted -= OnGoldenYear; _game.Sim.RainCloudTapped -= OnRain; }
        }

        /// <summary>New generation: the snow melts at once instead of lingering for a particle lifetime.</summary>
        private void OnGenerationStarted()
        {
            _fx.Clear(VfxId.Snow);
            _rebirth = 1f; // the new farm fades up out of the dark
            if (SettingsStore.MotionAllowed && CameraRig.Instance != null) CameraRig.Instance.Settle(1.25f);
        }

        private float _rebirth;

        /// <summary>A new year: the emission stops with Winter, but flakes already in the air lived on into Spring.</summary>
        private void OnRain() => _rainbow = RainbowSeconds;

        private void OnYearStarted()
        {
            _fx.Clear(VfxId.Snow);
            _mist = 1f;
        }

        /// <summary>Rim light per season: warm in autumn and the Golden Year, cool in winter, soft gold in summer.</summary>
        private static Color RimFor(Season s, bool golden)
        {
            if (golden) return new Color(1f, 0.78f, 0.4f, 0.75f);
            switch (s)
            {
                case Season.Autumn: return new Color(1f, 0.72f, 0.42f, 0.7f);
                case Season.Winter: return new Color(0.78f, 0.88f, 1f, 0.6f);
                case Season.Summer: return new Color(1f, 0.92f, 0.66f, 0.4f);
                default: return new Color(1f, 0.97f, 0.88f, 0f);
            }
        }

        /// <summary>Cloud cover per season: a clear summer, a heavy grey winter.</summary>
        private static float CloudsFor(Season s) => s == Season.Summer ? 0.3f : s == Season.Autumn ? 0.6f : s == Season.Winter ? 0.8f : 0.45f;

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

            Shader.SetGlobalFloat(CalmId, SettingsStore.MotionAllowed ? 0f : 1f);
            float dtSky = Time.deltaTime;
            _mist = Mathf.Max(0f, _mist - dtSky / MistSeconds);
            // Snow settles slowly and melts quickly: the field whitens over several seconds instead of at once.
            float snowTarget = _current.SnowAmount;
            _snow = Prims.Damp(_snow, snowTarget, snowTarget > _snow ? 0.5f : 3f, dtSky);
            _clouds = Prims.Damp(_clouds, CloudsFor(_toSeason), 0.8f, dtSky);
            _rainbow = Mathf.Max(0f, _rainbow - dtSky);
            _rebirth = Mathf.Max(0f, _rebirth - dtSky / 2.2f);
            // Evening falls as the frost nears: the sun sinks and a moon rises.
            _dusk = Prims.Damp(_dusk, frost, 2f, dtSky);
            // Autumn wind comes in gusts.
            float gust = _toSeason == Season.Autumn && state.Phase == Phase.Year ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 0.5f)), 6f) * 2.5f : 0f;
            Shader.SetGlobalFloat(GustId, gust);
            Shader.SetGlobalColor(RimTintId, RimFor(_toSeason, state.GoldenYearActive));
            Apply(_current, frost);

            // Ambience: one particle system per season, cross-faded by the same blend as the light.
            float blend = Mathf.SmoothStep(0f, 1f, _blend);
            float wFrom = 1f - blend, wTo = blend;
            float petals = (_fromSeason == Season.Spring ? wFrom : 0f) + (_toSeason == Season.Spring ? wTo : 0f);
            float leaves = (_fromSeason == Season.Autumn ? wFrom : 0f) + (_toSeason == Season.Autumn ? wTo : 0f);
            bool year = state.Phase == Phase.Year;
            bool golden = state.GoldenYearActive;
            _fx.SetRate(VfxId.Petals, year && !golden ? 5f * petals : 0f);
            _fx.SetRate(VfxId.GoldMotes, year && golden ? 14f : 0f);
            _fx.SetRate(VfxId.Leaves, year ? 9f * leaves : 0f);
            // Winter snows at once; the season blend only ever mattered on the way out of Winter, where it kept snowing
            // into Spring for the whole blend. Outside Winter only the frost warning brings a few flakes.
            _fx.SetRate(VfxId.Snow, state.IsWinter ? 70f : frost * 12f);

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
            float mist = Mathf.SmoothStep(0f, 1f, _mist);
            RenderSettings.fogColor = Color.Lerp(look.FogColor, look.SkyBottom, mist * 0.6f);
            RenderSettings.fogStartDistance = Mathf.Lerp(look.FogStart, look.FogStart * 0.72f, mist);
            RenderSettings.fogEndDistance = look.FogEnd;
            _color.colorFilter.value = Color.Lerp(Color.Lerp(look.ColorFilter, cold, frost * 0.5f), Color.black, Mathf.SmoothStep(0f, 1f, _rebirth) * 0.75f);
            _vignette.intensity.value = frost * 0.35f + (_game.State.IsWinter ? 0.25f : 0f);
            if (_skyMaterial != null)
            {
                // Dusk: the top of the sky deepens toward evening blue as the frost nears.
                _skyMaterial.SetColor(SkyTopId, Color.Lerp(Color.Lerp(look.SkyTop, cold, frost * 0.3f), look.SkyTop * 0.55f, _dusk * 0.5f));
                _skyMaterial.SetColor(SkyBottomId, look.SkyBottom);
                // The sun rides higher the steeper the light (summer), sits low in autumn and winter.
                float elevation = Mathf.InverseLerp(20f, 72f, look.Angle.x);
                _skyMaterial.SetVector(SunPosId, new Vector4(0.84f - elevation * 0.04f + _dusk * 0.06f, 0.69f + elevation * 0.06f - _dusk * 0.06f, 0f, 0f));
                _skyMaterial.SetVector(MoonPosId, new Vector4(0.2f, 0.72f + _dusk * 0.03f, 0f, 0f));
                var moon = Color.Lerp(Color.white, look.SkyTop, 0.15f);
                moon.a = _dusk * 0.9f;
                _skyMaterial.SetColor(MoonColorId, moon);
                _skyMaterial.SetFloat(RainbowId, Mathf.Clamp01(_rainbow / 2f) * Mathf.Clamp01((RainbowSeconds - _rainbow) / 1.5f));
                _skyMaterial.SetFloat(SunSizeId, 0.034f);
                var sunColor = Color.Lerp(look.Light, new Color(1f, 0.86f, 0.55f), 0.3f);
                sunColor.a = Mathf.Lerp(0.95f, 0.5f, frost) * (_game.State.IsWinter ? 0.55f : 1f);
                _skyMaterial.SetColor(SunColorId, sunColor);
                if (_cam != null) _skyMaterial.SetFloat(AspectId, _cam.aspect);
                _skyMaterial.SetFloat(CloudsId, _clouds);
                _skyMaterial.SetColor(CloudColorId, Color.Lerp(Color.white, look.SkyBottom, 0.35f));
                _skyMaterial.SetFloat(HazeId, Mathf.Max(mist, _toSeason == Season.Summer ? 0.35f : 0.12f));
            }
            else if (_cam != null) _cam.backgroundColor = look.SkyBottom;
            float snow = Mathf.Max(_snow, frost * 0.35f);
            PaletteBinder.SetSeason(snow, Color.Lerp(look.LeafTint, cold, frost * 0.3f), look.GrassTint);
        }
    }
}
