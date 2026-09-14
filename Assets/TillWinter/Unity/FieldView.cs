using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Creates and updates one <see cref="PlotView"/> per plot; rebuilds when the field changes; owns the generation decor.</summary>
    public sealed class FieldView : MonoBehaviour
    {
        public static readonly Color SoilDry = new Color(0.66f, 0.52f, 0.34f);
        public static readonly Color SoilWet = new Color(0.36f, 0.24f, 0.14f);
        public static readonly Color SoilRing = new Color(0.72f, 0.5f, 0.24f);
        public static readonly Color SoilWinter = new Color(0.78f, 0.8f, 0.86f);
        public static readonly Color CrackColor = new Color(0.5f, 0.38f, 0.24f);

        private GameController _game;
        private CameraRig _camera;
        private FxManager _fx;
        private AudioManager _audio;
        private readonly Dictionary<GridPos, PlotView> _plots = new Dictionary<GridPos, PlotView>();
        private CropMaterials _materials;
        private FarmDecorView _decor;
        private float _reveal = 99f;

        public void Init(GameController game, CameraRig camera, FxManager fx, AudioManager audio)
        {
            _game = game;
            _camera = camera;
            _fx = fx;
            _audio = audio;
            _materials = new CropMaterials();
            Rebuild();
            _game.Sim.Harvested += OnHarvested;
            _game.Sim.PlotWatered += OnWatered;
            _game.Sim.PlotRipened += OnRipened;
            _game.Sim.CrowAte += OnCrowAte;
            _game.Sim.FieldExpanded += Rebuild;
            _game.Sim.Retired += _ => Rebuild();
            _game.Sim.GenerationStarted += OnGenerationStarted;
            _decor = new GameObject("FarmDecor").AddComponent<FarmDecorView>();
            _decor.transform.SetParent(transform, false);
            _decor.Init(game);
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.Harvested -= OnHarvested;
            _game.Sim.PlotWatered -= OnWatered;
            _game.Sim.PlotRipened -= OnRipened;
            _game.Sim.CrowAte -= OnCrowAte;
            _game.Sim.FieldExpanded -= Rebuild;
            _game.Sim.GenerationStarted -= OnGenerationStarted;
        }

        private void Rebuild()
        {
            var state = _game.State;
            var gone = new List<GridPos>();
            foreach (var kv in _plots) if (!state.InBounds(kv.Key)) gone.Add(kv.Key);
            foreach (var pos in gone)
            {
                Destroy(_plots[pos].gameObject);
                _plots.Remove(pos);
            }
            foreach (var plot in state.Plots)
            {
                if (_plots.ContainsKey(plot.Pos)) continue;
                var go = new GameObject("Plot " + plot.Pos);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<PlotView>();
                view.Init(plot, _materials);
                _plots.Add(plot.Pos, view);
            }
            foreach (var kv in _plots)
                kv.Value.transform.localPosition = _game.PlotToWorld(kv.Key);
            _camera.Frame(state.GridSize, false);
        }

        /// <summary>New generation: plots appear one by one bottom-left to top-right (0.03 s stagger). Tap after 0.5 s skips.</summary>
        private void OnGenerationStarted()
        {
            Rebuild();
            _reveal = 0f;
            foreach (var kv in _plots) kv.Value.transform.localScale = Vector3.one * 0.001f;
        }

        public void SkipReveal()
        {
            _reveal = 99f;
            foreach (var kv in _plots) kv.Value.transform.localScale = Vector3.one;
            _decor.CompleteReveal();
        }

        private void OnHarvested(HarvestEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            if (_plots.TryGetValue(e.Pos, out var view))
            {
                view.Pop();
                _fx.HarvestBurst(_game.PlotToWorld(e.Pos, 0.4f), e.Tier);
            }
            _audio.Play(SfxId.HarvestPop);
        }

        private void OnWatered(GridPos pos)
        {
            if (_plots.TryGetValue(pos, out var view)) view.SproutPop();
            _fx.WaterSplash(_game.PlotToWorld(pos, 0.25f));
            _audio.Play(SfxId.WaterSplash);
        }

        private void OnRipened(GridPos pos)
        {
            if (_plots.TryGetValue(pos, out var view)) view.RipePop();
        }

        private void OnCrowAte(CrowEvent e)
        {
            if (_plots.TryGetValue(e.Pos, out var view)) view.Vanish();
            _fx.Puff(_game.PlotToWorld(e.Pos, 0.35f));
        }

        private void LateUpdate()
        {
            var state = _game.State;
            float dt = Time.deltaTime;
            float t = _game.SimTime;
            if (_reveal < 99f)
            {
                _reveal += dt;
                int n = state.GridSize;
                bool done = true;
                foreach (var kv in _plots)
                {
                    float delay = (kv.Key.Y * n + kv.Key.X) * 0.03f;
                    float p = Mathf.Clamp01((_reveal - delay) / 0.25f);
                    if (p < 1f) done = false;
                    kv.Value.transform.localScale = Vector3.one * Mathf.Max(0.001f, p < 1f ? Mathf.Lerp(0.001f, 1.1f, Prims.EaseOutQuad(p)) : 1f);
                }
                if (_reveal > 0.5f && _game.Pointer != null && _game.Pointer.Current.Tapped) SkipReveal();
                else if (done) _reveal = 99f;
            }
            foreach (var kv in _plots)
            {
                var plot = state.GetPlot(kv.Key);
                bool underRing = !state.IsWinter && state.IsUnderRing(kv.Key);
                kv.Value.Tick(plot, state.IsWinter, underRing, dt, t);
            }
        }
    }

    /// <summary>Shared flat materials for the six crop tiers plus leaf/stem/soil.</summary>
    public sealed class CropMaterials
    {
        public readonly Material Soil = Prims.Lit(FieldView.SoilDry, 0.05f, true);
        public readonly Material Crack = Prims.Lit(FieldView.CrackColor, 0.05f);
        public readonly Material Leaf = Prims.Lit(new Color(0.3f, 0.6f, 0.25f), 0.1f, true);
        public readonly Material Stem = Prims.Lit(new Color(0.24f, 0.48f, 0.2f), 0.1f, true);
        public readonly Material Sprout = Prims.Lit(new Color(0.45f, 0.75f, 0.3f), 0.1f, true);
        public readonly Material[] Fruit =
        {
            Prims.Lit(new Color(0.95f, 0.5f, 0.12f), 0.25f, true),
            Prims.Lit(new Color(0.88f, 0.16f, 0.14f), 0.4f, true),
            Prims.Lit(new Color(0.98f, 0.82f, 0.2f), 0.3f, true),
            Prims.Lit(new Color(0.95f, 0.55f, 0.1f), 0.3f, true),
            Prims.Lit(new Color(0.45f, 0.2f, 0.55f), 0.45f, true),
            Prims.Lit(new Color(1f, 0.85f, 0.35f), 0.5f, true),
        };
    }

    /// <summary>
    /// One plot: soil slab whose colour reads the state (Dry cracked light brown, Wet dark),
    /// a sprout while Wet, a crop built from primitives whose scale follows growth, wobble + glow when Ripe.
    /// </summary>
    public sealed class PlotView : MonoBehaviour
    {
        private const float PopSeconds = 0.24f;
        private const float VanishSeconds = 0.18f;

        private Renderer _soil;
        private readonly List<GameObject> _cracks = new List<GameObject>();
        private Transform _cropRoot;
        private Transform _sprout;
        private readonly List<Renderer> _cropRenderers = new List<Renderer>();
        private CropMaterials _mats;
        private MaterialPropertyBlock _mpb;
        private int _builtTier = -1;

        private float _visualScale;
        private float _sproutScale;
        private float _pop = -1f;
        private bool _vanish;
        private float _ripeGlow;
        private float _ringGlow;
        private float _wetBlend;
        private float _winterBlend;
        private float _phase;
        private float _ripePunch;

        public void Init(Plot plot, CropMaterials mats)
        {
            _mats = mats;
            _mpb = new MaterialPropertyBlock();
            _phase = (plot.Pos.X * 7 + plot.Pos.Y * 13) * 0.37f;

            var soilGo = Prims.Primitive(PrimitiveType.Cube, transform, "Soil", new Vector3(0f, 0.08f, 0f), new Vector3(0.92f, 0.16f, 0.92f), mats.Soil);
            _soil = soilGo.GetComponent<Renderer>();

            var seed = new System.Random(plot.Pos.X * 31 + plot.Pos.Y * 17);
            for (int i = 0; i < 3; i++)
            {
                float x = (float)(seed.NextDouble() * 0.6 - 0.3), z = (float)(seed.NextDouble() * 0.6 - 0.3);
                float len = 0.18f + (float)seed.NextDouble() * 0.2f;
                var crack = Prims.Primitive(PrimitiveType.Cube, transform, "Crack" + i, new Vector3(x, 0.161f, z), new Vector3(len, 0.006f, 0.025f), mats.Crack);
                crack.transform.localRotation = Quaternion.Euler(0f, (float)seed.NextDouble() * 180f, 0f);
                crack.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _cracks.Add(crack);
            }

            _cropRoot = new GameObject("Crop").transform;
            _cropRoot.SetParent(transform, false);
            _cropRoot.localPosition = new Vector3(0f, 0.16f, 0f);

            _sprout = new GameObject("Sprout").transform;
            _sprout.SetParent(transform, false);
            _sprout.localPosition = new Vector3(0f, 0.16f, 0f);
            Prims.Primitive(PrimitiveType.Cylinder, _sprout, "Stem", new Vector3(0f, 0.08f, 0f), new Vector3(0.035f, 0.08f, 0.035f), mats.Stem);
            Prims.Primitive(PrimitiveType.Sphere, _sprout, "LeafA", new Vector3(-0.06f, 0.15f, 0f), new Vector3(0.12f, 0.05f, 0.07f), mats.Sprout);
            Prims.Primitive(PrimitiveType.Sphere, _sprout, "LeafB", new Vector3(0.06f, 0.15f, 0f), new Vector3(0.12f, 0.05f, 0.07f), mats.Sprout);
            _sprout.localScale = Vector3.one * 0.0001f;

            BuildCrop(plot.Tier);
            _cropRoot.localScale = Vector3.one * 0.0001f;
        }

        private void BuildCrop(int tier)
        {
            for (int i = _cropRoot.childCount - 1; i >= 0; i--) Destroy(_cropRoot.GetChild(i).gameObject);
            _cropRenderers.Clear();
            _builtTier = tier;
            Material leaf = _mats.Leaf, stem = _mats.Stem;
            Material fruit = _mats.Fruit[Mathf.Clamp(tier, 0, _mats.Fruit.Length - 1)];
            switch (tier)
            {
                case 0:
                    Add(Prims.MeshObject(Prims.Cone(true), _cropRoot, "Root", Vector3.zero, new Vector3(0.17f, 0.42f, 0.17f), fruit));
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Leaves", new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.16f, 0.3f), leaf));
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Leaves2", new Vector3(0.06f, 0.58f, -0.04f), new Vector3(0.16f, 0.12f, 0.16f), leaf));
                    break;
                case 1:
                    Add(Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stem", new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.2f, 0.06f), stem));
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Fruit", new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.34f, 0.36f), fruit));
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Cap", new Vector3(0f, 0.66f, 0f), new Vector3(0.16f, 0.07f, 0.16f), leaf));
                    break;
                case 2:
                {
                    Add(Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stalk", new Vector3(0f, 0.42f, 0f), new Vector3(0.07f, 0.42f, 0.07f), stem));
                    Add(Prims.Primitive(PrimitiveType.Capsule, _cropRoot, "Cob", new Vector3(0.1f, 0.6f, 0f), new Vector3(0.17f, 0.2f, 0.17f), fruit));
                    var leafA = Prims.Primitive(PrimitiveType.Cube, _cropRoot, "LeafA", new Vector3(-0.14f, 0.45f, 0f), new Vector3(0.28f, 0.03f, 0.1f), leaf);
                    leafA.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    Add(leafA);
                    var leafB = Prims.Primitive(PrimitiveType.Cube, _cropRoot, "LeafB", new Vector3(0.12f, 0.28f, 0.08f), new Vector3(0.26f, 0.03f, 0.1f), leaf);
                    leafB.transform.localRotation = Quaternion.Euler(0f, 30f, -30f);
                    Add(leafB);
                    break;
                }
                case 3:
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Body", new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.38f, 0.5f), fruit));
                    Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Body2", new Vector3(0.12f, 0.2f, 0.1f), new Vector3(0.36f, 0.32f, 0.36f), fruit));
                    Add(Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stem", new Vector3(0f, 0.46f, 0f), new Vector3(0.06f, 0.06f, 0.06f), stem));
                    Add(Prims.Primitive(PrimitiveType.Cube, _cropRoot, "Leaf", new Vector3(-0.2f, 0.3f, 0.1f), new Vector3(0.22f, 0.02f, 0.14f), leaf));
                    break;
                case 4:
                {
                    Add(Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Trellis", new Vector3(0f, 0.4f, 0f), new Vector3(0.05f, 0.4f, 0.05f), stem));
                    Add(Prims.Primitive(PrimitiveType.Cube, _cropRoot, "Bar", new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.03f, 0.03f), stem));
                    var offs = new[] { new Vector3(-0.12f, 0.55f, 0.05f), new Vector3(0.12f, 0.52f, -0.04f), new Vector3(0f, 0.42f, 0.06f), new Vector3(-0.06f, 0.36f, -0.05f), new Vector3(0.08f, 0.3f, 0.03f) };
                    for (int i = 0; i < offs.Length; i++)
                        Add(Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Grape" + i, offs[i], new Vector3(0.16f, 0.16f, 0.16f), fruit));
                    Add(Prims.Primitive(PrimitiveType.Cube, _cropRoot, "Leaf", new Vector3(0.18f, 0.68f, 0.06f), new Vector3(0.16f, 0.02f, 0.12f), leaf));
                    break;
                }
                default:
                {
                    var xs = new[] { -0.14f, 0.02f, 0.15f };
                    for (int i = 0; i < xs.Length; i++)
                    {
                        float h = 0.75f + i * 0.08f;
                        Add(Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stalk" + i, new Vector3(xs[i], h * 0.5f, (i - 1) * 0.08f), new Vector3(0.035f, h * 0.5f, 0.035f), leaf));
                        Add(Prims.Primitive(PrimitiveType.Capsule, _cropRoot, "Head" + i, new Vector3(xs[i], h + 0.1f, (i - 1) * 0.08f), new Vector3(0.09f, 0.12f, 0.09f), fruit));
                    }
                    break;
                }
            }
        }

        private void Add(GameObject go) => _cropRenderers.Add(go.GetComponent<Renderer>());

        public void Pop()
        {
            _pop = 0f;
            _vanish = false;
        }

        public void Vanish()
        {
            _vanish = true;
            _pop = -1f;
        }

        public void SproutPop() => _sproutScale = 1.4f;

        public void RipePop() => _ripePunch = 1f;

        public void Tick(Plot plot, bool winter, bool underRing, float dt, float simTime)
        {
            if (plot.Tier != _builtTier) BuildCrop(plot.Tier);

            bool dry = plot.State == PlotState.Dry;
            bool wet = plot.State == PlotState.Wet;
            bool ripe = plot.State == PlotState.Ripe && !winter;

            float scale;
            if (_pop >= 0f)
            {
                _pop += dt;
                float t = Mathf.Clamp01(_pop / PopSeconds);
                scale = t < 0.3f ? Mathf.Lerp(1f, 1.3f, t / 0.3f) : Mathf.Lerp(1.3f, 0f, Prims.EaseOutQuad((t - 0.3f) / 0.7f));
                if (t >= 1f) _pop = -1f;
                _visualScale = scale;
            }
            else
            {
                float target;
                if (winter || dry) target = 0f;
                else if (wet) target = 0.12f + 0.88f * Prims.EaseOutQuad(Mathf.Clamp01(plot.Progress));
                else target = 1f;
                if (_vanish)
                {
                    _visualScale = Mathf.MoveTowards(_visualScale, 0f, dt / VanishSeconds);
                    if (_visualScale <= 0.001f) _vanish = false;
                }
                else _visualScale = Prims.Damp(_visualScale, target, winter ? 3f : 16f, dt);
                scale = _visualScale;
            }
            _ripePunch = Mathf.Max(0f, _ripePunch - dt * 4f);
            float punch = 1f + 0.18f * Mathf.Sin(_ripePunch * Mathf.PI);
            float squash = ripe && underRing ? 1f - 0.2f * plot.Progress : 1f;
            float xz = Mathf.Lerp(0.55f, 1f, scale) * punch / Mathf.Sqrt(squash);
            _cropRoot.localScale = new Vector3(xz, Mathf.Max(0.0001f, scale * punch * squash), xz);

            float sproutTarget = wet && !winter ? Mathf.Lerp(1f, 0f, Mathf.Clamp01(plot.Progress * 2f)) : 0f;
            _sproutScale = _sproutScale > sproutTarget + 0.3f ? Prims.Damp(_sproutScale, sproutTarget, 6f, dt) : Prims.Damp(_sproutScale, sproutTarget, 14f, dt);
            _sprout.localScale = Vector3.one * Mathf.Max(0.0001f, _sproutScale);

            _ripeGlow = Prims.Damp(_ripeGlow, ripe ? 1f : 0f, 8f, dt);
            float wobble = ripe ? Mathf.Sin(simTime * 6f + _phase) * 7f : 0f;
            float wobble2 = ripe ? Mathf.Sin(simTime * 4.3f + _phase * 1.7f) * 4f : 0f;
            _cropRoot.localRotation = Quaternion.Euler(wobble2, 0f, wobble);
            var glow = Color.Lerp(Color.black, new Color(0.35f, 0.3f, 0.12f), _ripeGlow);
            if (plot.IsGolden && !winter)
            {
                float pulse = 0.35f + 0.25f * Mathf.Sin(simTime * 2.5f + _phase);
                glow = Color.Lerp(glow, new Color(0.9f, 0.75f, 0.15f), pulse);
            }
            for (int i = 0; i < _cropRenderers.Count; i++)
            {
                var r = _cropRenderers[i];
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(Prims.EmissionColorId, glow);
                r.SetPropertyBlock(_mpb);
            }

            _wetBlend = Prims.Damp(_wetBlend, dry || winter ? 0f : 1f, 10f, dt);
            _ringGlow = Prims.Damp(_ringGlow, underRing ? 1f : 0f, 12f, dt);
            _winterBlend = Prims.Damp(_winterBlend, winter ? 1f : 0f, 4f, dt);
            var soil = Color.Lerp(FieldView.SoilDry, FieldView.SoilWet, _wetBlend);
            soil = Color.Lerp(soil, FieldView.SoilRing, _ringGlow * 0.6f);
            soil = Color.Lerp(soil, FieldView.SoilWinter, _winterBlend);
            _soil.GetPropertyBlock(_mpb);
            _mpb.SetColor(Prims.BaseColorId, soil);
            _mpb.SetColor(Prims.EmissionColorId, Color.Lerp(Color.black, new Color(0.25f, 0.18f, 0.06f), _ringGlow));
            _soil.SetPropertyBlock(_mpb);
            bool showCracks = _wetBlend < 0.5f && _winterBlend < 0.5f;
            for (int i = 0; i < _cracks.Count; i++)
                if (_cracks[i].activeSelf != showCracks) _cracks[i].SetActive(showCracks);
        }
    }
}
