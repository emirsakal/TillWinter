using TillWinter.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>
    /// Seasons through light and colour only: one directional light, flat ambient, a full-screen
    /// colour filter, ground colour, camera background, frost vignette and snow.
    /// </summary>
    public sealed class SeasonPresenter : MonoBehaviour
    {
        private struct Look
        {
            public Color Light, Ambient, Tint, Ground, Sky;
            public float Intensity;
            public Vector3 Euler;

            public static Look Lerp(Look a, Look b, float t) => new Look
            {
                Light = Color.Lerp(a.Light, b.Light, t),
                Ambient = Color.Lerp(a.Ambient, b.Ambient, t),
                Tint = Color.Lerp(a.Tint, b.Tint, t),
                Ground = Color.Lerp(a.Ground, b.Ground, t),
                Sky = Color.Lerp(a.Sky, b.Sky, t),
                Intensity = Mathf.Lerp(a.Intensity, b.Intensity, t),
                Euler = Vector3.Lerp(a.Euler, b.Euler, t),
            };
        }

        private static readonly Look Spring = new Look
        {
            Light = new Color(1f, 0.98f, 0.9f), Intensity = 1.15f, Euler = new Vector3(52f, -30f, 0f),
            Ambient = new Color(0.58f, 0.66f, 0.56f), Tint = new Color(0.96f, 1f, 0.94f),
            Ground = new Color(0.46f, 0.68f, 0.36f), Sky = new Color(0.62f, 0.8f, 0.6f),
        };
        private static readonly Look Summer = new Look
        {
            Light = new Color(1f, 0.95f, 0.8f), Intensity = 1.35f, Euler = new Vector3(70f, -20f, 0f),
            Ambient = new Color(0.66f, 0.64f, 0.52f), Tint = new Color(1f, 0.97f, 0.88f),
            Ground = new Color(0.58f, 0.66f, 0.3f), Sky = new Color(0.78f, 0.82f, 0.55f),
        };
        private static readonly Look Autumn = new Look
        {
            Light = new Color(1f, 0.78f, 0.52f), Intensity = 1.05f, Euler = new Vector3(32f, -45f, 0f),
            Ambient = new Color(0.6f, 0.48f, 0.38f), Tint = new Color(1f, 0.88f, 0.74f),
            Ground = new Color(0.66f, 0.5f, 0.28f), Sky = new Color(0.82f, 0.62f, 0.42f),
        };
        private static readonly Look Winter = new Look
        {
            Light = new Color(0.8f, 0.88f, 1f), Intensity = 0.85f, Euler = new Vector3(26f, -45f, 0f),
            Ambient = new Color(0.62f, 0.7f, 0.86f), Tint = new Color(0.86f, 0.92f, 1f),
            Ground = new Color(0.86f, 0.9f, 0.95f), Sky = new Color(0.72f, 0.8f, 0.9f),
        };

        public float BlendSeconds = 1.5f;

        private GameController _game;
        private Light _sun;
        private Renderer _ground;
        private ColorAdjustments _color;
        private Vignette _vignette;
        private ParticleSystem _snow;
        private ParticleSystem.EmissionModule _snowEmission;
        private MaterialPropertyBlock _mpb;

        private Look _from, _to, _current;
        private float _blend = 1f;
        private Season _season;

        public void Init(GameController game)
        {
            _game = game;
            _mpb = new MaterialPropertyBlock();
            _game.Sim.GenerationStarted += OnGenerationStarted;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.55f;
            _sun.shadowBias = 0.05f;
            _sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = AmbientMode.Flat;

            var groundGo = Prims.Primitive(PrimitiveType.Cube, transform, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), Prims.Lit(Spring.Ground, 0.05f));
            _ground = groundGo.GetComponent<Renderer>();
            _ground.shadowCastingMode = ShadowCastingMode.Off;

            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(transform, false);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = profile.Add<ColorAdjustments>(true);
            _color.colorFilter.overrideState = true;
            _color.colorFilter.value = Color.white;
            _color.postExposure.overrideState = true;
            _color.postExposure.value = 0f;
            _color.saturation.overrideState = true;
            _color.saturation.value = 8f;
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

            BuildSnow();

            _season = _game.State.Season;
            _from = _to = _current = LookFor(_season);
            Apply(_current, 0f);

            _game.Sim.SeasonChanged += OnSeasonChanged;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) { _game.Sim.SeasonChanged -= OnSeasonChanged; _game.Sim.GenerationStarted -= OnGenerationStarted; }
        }

        /// <summary>New generation: the snow melts at once instead of lingering for a particle lifetime.</summary>
        private void OnGenerationStarted() => _snow.Clear();

        private void OnSeasonChanged(Season s)
        {
            _season = s;
            _from = _current;
            _to = LookFor(s);
            _blend = 0f;
        }

        private static Look LookFor(Season s)
        {
            switch (s)
            {
                case Season.Summer: return Summer;
                case Season.Autumn: return Autumn;
                case Season.Winter: return Winter;
                default: return Spring;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, BlendSeconds));
                _current = Look.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _blend));
            }

            var state = _game.State;
            float frost = 0f;
            if (state.FrostWarning && !state.IsWinter)
                frost = Mathf.Clamp01(1f - state.SecondsUntilWinter / Mathf.Max(0.01f, _game.State.Stats.FrostWarningSeconds));

            Apply(_current, frost);

            float snowRate = state.IsWinter ? 70f : frost * 12f;
            _snowEmission.rateOverTime = snowRate;
        }

        private void Apply(Look look, float frost)
        {
            var cold = new Color(0.75f, 0.85f, 1f);
            _sun.color = Color.Lerp(look.Light, cold, frost * 0.8f);
            _sun.intensity = Mathf.Lerp(look.Intensity, look.Intensity * 0.8f, frost);
            _sun.transform.rotation = Quaternion.Euler(look.Euler);
            RenderSettings.ambientLight = Color.Lerp(look.Ambient, cold * 0.7f, frost * 0.5f);
            _color.colorFilter.value = Color.Lerp(look.Tint, cold, frost * 0.65f);
            _mpb.SetColor(Prims.BaseColorId, Color.Lerp(look.Ground, Winter.Ground, frost * 0.5f));
            _ground.SetPropertyBlock(_mpb);
            if (_game.Cam != null) _game.Cam.backgroundColor = Color.Lerp(look.Sky, cold, frost * 0.4f);

            float vig = _game.State.IsWinter ? 0.42f : frost * 0.62f;
            _vignette.intensity.value = vig;
            _vignette.color.value = _game.State.IsWinter ? new Color(0.8f, 0.88f, 1f) : new Color(0.55f, 0.72f, 1f);
        }

        private void BuildSnow()
        {
            var go = new GameObject("Snow");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 7f, 0f);
            _snow = go.AddComponent<ParticleSystem>();
            var main = _snow.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.gravityModifier = 0.045f;
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            _snowEmission = _snow.emission;
            _snowEmission.rateOverTime = 0f;
            var shape = _snow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 0.2f, 12f);
            var noise = _snow.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;
            var renderer = _snow.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Prims.BuiltinMesh(PrimitiveType.Sphere);
            renderer.sharedMaterial = Prims.Lit(new Color(0.97f, 0.98f, 1f), 0.1f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _snow.Play();
        }
    }
}
