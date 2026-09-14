using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Creates and updates one <see cref="PlotView"/> per plot; rebuilds when the field expands.</summary>
    public sealed class FieldView : MonoBehaviour
    {
        public static readonly Color SoilColor = new Color(0.43f, 0.29f, 0.17f);
        public static readonly Color SoilRingColor = new Color(0.66f, 0.47f, 0.24f);
        public static readonly Color SoilWinterColor = new Color(0.78f, 0.8f, 0.86f);

        private GameController _game;
        private CameraRig _camera;
        private FxManager _fx;
        private AudioManager _audio;
        private readonly Dictionary<GridPos, PlotView> _plots = new Dictionary<GridPos, PlotView>();
        private Material _soilMaterial;
        private Material[] _cropMaterials;

        public void Init(GameController game, CameraRig camera, FxManager fx, AudioManager audio)
        {
            _game = game;
            _camera = camera;
            _fx = fx;
            _audio = audio;
            _soilMaterial = Prims.Lit(SoilColor, 0.05f, true);
            _cropMaterials = new[]
            {
                Prims.Lit(new Color(0.95f, 0.5f, 0.12f), 0.25f, true), // carrot orange
                Prims.Lit(new Color(0.88f, 0.16f, 0.14f), 0.4f, true), // tomato red
                Prims.Lit(new Color(0.98f, 0.82f, 0.2f), 0.3f, true),  // corn yellow
                Prims.Lit(new Color(0.3f, 0.6f, 0.25f), 0.1f, true),   // leaf green
                Prims.Lit(new Color(0.24f, 0.48f, 0.2f), 0.1f, true),  // stem green
            };
            Rebuild();
            _game.Sim.Harvested += OnHarvested;
            _game.Sim.CrowAte += OnCrowAte;
            _game.Sim.FieldExpanded += Rebuild;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.Harvested -= OnHarvested;
            _game.Sim.CrowAte -= OnCrowAte;
            _game.Sim.FieldExpanded -= Rebuild;
        }

        private void Rebuild()
        {
            var state = _game.State;
            foreach (var plot in state.Plots)
            {
                if (_plots.ContainsKey(plot.Pos)) continue;
                var go = new GameObject("Plot " + plot.Pos);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<PlotView>();
                view.Init(plot, _soilMaterial, _cropMaterials);
                _plots.Add(plot.Pos, view);
            }
            foreach (var kv in _plots)
                kv.Value.transform.localPosition = _game.PlotToWorld(kv.Key);
            _camera.Frame(state.GridSize, false);
        }

        private void OnHarvested(HarvestEvent e)
        {
            if (_plots.TryGetValue(e.Pos, out var view))
            {
                view.Pop();
                _fx.HarvestBurst(_game.PlotToWorld(e.Pos, 0.4f), e.Tier);
            }
            _audio.Play(SfxId.HarvestPop);
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
            foreach (var kv in _plots)
            {
                var plot = state.GetPlot(kv.Key);
                bool underRing = !state.IsWinter && state.IsUnderRing(kv.Key);
                kv.Value.Tick(plot, state.IsWinter, underRing, dt, t);
            }
        }
    }

    /// <summary>One plot: a soil slab plus a crop built from primitives whose Y-scale follows growth.</summary>
    public sealed class PlotView : MonoBehaviour
    {
        private const float PopSeconds = 0.24f;
        private const float VanishSeconds = 0.18f;

        private Renderer _soil;
        private Transform _cropRoot;
        private readonly List<Renderer> _cropRenderers = new List<Renderer>();
        private Material _soilMaterial;
        private Material[] _cropMaterials;
        private MaterialPropertyBlock _mpb;
        private CropTier _builtTier = (CropTier)(-1);

        private float _visualScale = 0.1f;
        private float _pop = -1f;
        private bool _vanish;
        private float _ripeGlow;
        private float _ringGlow;
        private float _winterBlend;
        private float _phase;

        public void Init(Plot plot, Material soil, Material[] crops)
        {
            _soilMaterial = soil;
            _cropMaterials = crops;
            _mpb = new MaterialPropertyBlock();
            _phase = (plot.Pos.X * 7 + plot.Pos.Y * 13) * 0.37f;

            var soilGo = Prims.Primitive(PrimitiveType.Cube, transform, "Soil", new Vector3(0f, 0.08f, 0f), new Vector3(0.92f, 0.16f, 0.92f), soil);
            _soil = soilGo.GetComponent<Renderer>();

            _cropRoot = new GameObject("Crop").transform;
            _cropRoot.SetParent(transform, false);
            _cropRoot.localPosition = new Vector3(0f, 0.16f, 0f);
            BuildCrop(plot.Tier);
        }

        private void BuildCrop(CropTier tier)
        {
            for (int i = _cropRoot.childCount - 1; i >= 0; i--) Destroy(_cropRoot.GetChild(i).gameObject);
            _cropRenderers.Clear();
            _builtTier = tier;
            Material leaf = _cropMaterials[3], stem = _cropMaterials[4];
            switch (tier)
            {
                case CropTier.Carrot:
                {
                    var body = Prims.MeshObject(Prims.Cone(true), _cropRoot, "Root", new Vector3(0f, 0f, 0f), new Vector3(0.17f, 0.42f, 0.17f), _cropMaterials[0]);
                    var leaves = Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Leaves", new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.16f, 0.3f), leaf);
                    var leaves2 = Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Leaves2", new Vector3(0.06f, 0.58f, -0.04f), new Vector3(0.16f, 0.12f, 0.16f), leaf);
                    _cropRenderers.Add(body.GetComponent<Renderer>());
                    _cropRenderers.Add(leaves.GetComponent<Renderer>());
                    _cropRenderers.Add(leaves2.GetComponent<Renderer>());
                    break;
                }
                case CropTier.Tomato:
                {
                    var stemGo = Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stem", new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.2f, 0.06f), stem);
                    var fruit = Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Fruit", new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.34f, 0.36f), _cropMaterials[1]);
                    var cap = Prims.Primitive(PrimitiveType.Sphere, _cropRoot, "Cap", new Vector3(0f, 0.66f, 0f), new Vector3(0.16f, 0.07f, 0.16f), leaf);
                    _cropRenderers.Add(stemGo.GetComponent<Renderer>());
                    _cropRenderers.Add(fruit.GetComponent<Renderer>());
                    _cropRenderers.Add(cap.GetComponent<Renderer>());
                    break;
                }
                default:
                {
                    var stalk = Prims.Primitive(PrimitiveType.Cylinder, _cropRoot, "Stalk", new Vector3(0f, 0.42f, 0f), new Vector3(0.07f, 0.42f, 0.07f), stem);
                    var cob = Prims.Primitive(PrimitiveType.Capsule, _cropRoot, "Cob", new Vector3(0.1f, 0.6f, 0f), new Vector3(0.17f, 0.2f, 0.17f), _cropMaterials[2]);
                    var leafA = Prims.Primitive(PrimitiveType.Cube, _cropRoot, "LeafA", new Vector3(-0.14f, 0.45f, 0f), new Vector3(0.28f, 0.03f, 0.1f), leaf);
                    leafA.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    var leafB = Prims.Primitive(PrimitiveType.Cube, _cropRoot, "LeafB", new Vector3(0.12f, 0.28f, 0.08f), new Vector3(0.26f, 0.03f, 0.1f), leaf);
                    leafB.transform.localRotation = Quaternion.Euler(0f, 30f, -30f);
                    _cropRenderers.Add(stalk.GetComponent<Renderer>());
                    _cropRenderers.Add(cob.GetComponent<Renderer>());
                    _cropRenderers.Add(leafA.GetComponent<Renderer>());
                    _cropRenderers.Add(leafB.GetComponent<Renderer>());
                    break;
                }
            }
        }

        /// <summary>Harvest: scale punch 1.3 then to 0.</summary>
        public void Pop()
        {
            _pop = 0f;
            _vanish = false;
        }

        /// <summary>Crow ate it: shrink away quickly.</summary>
        public void Vanish()
        {
            _vanish = true;
            _pop = -1f;
        }

        public void Tick(Plot plot, bool winter, bool underRing, float dt, float simTime)
        {
            if (plot.Tier != _builtTier) BuildCrop(plot.Tier);

            // --- scale
            float scale;
            if (_pop >= 0f)
            {
                _pop += dt;
                float t = Mathf.Clamp01(_pop / PopSeconds);
                scale = t < 0.3f ? Mathf.Lerp(1f, 1.3f, t / 0.3f) : Mathf.Lerp(1.3f, 0f, Prims.EaseOutQuad((t - 0.3f) / 0.7f));
                if (t >= 1f)
                {
                    _pop = -1f;
                    _visualScale = 0f;
                }
                _visualScale = scale;
            }
            else
            {
                float target = winter ? 0f : 0.1f + 0.9f * Prims.EaseOutQuad(Mathf.Clamp01(plot.Growth));
                if (_vanish)
                {
                    _visualScale = Mathf.MoveTowards(_visualScale, 0f, dt / VanishSeconds);
                    if (_visualScale <= 0.001f) _vanish = false;
                }
                else
                {
                    _visualScale = Prims.Damp(_visualScale, target, winter ? 3f : 16f, dt);
                }
                scale = _visualScale;
            }
            float xz = Mathf.Lerp(0.55f, 1f, scale);
            _cropRoot.localScale = new Vector3(xz, Mathf.Max(0.0001f, scale), xz);

            // --- ripe wobble + glow
            bool ripe = plot.IsRipe && !winter;
            _ripeGlow = Prims.Damp(_ripeGlow, ripe ? 1f : 0f, 8f, dt);
            float wobble = ripe ? Mathf.Sin(simTime * 6f + _phase) * 7f : 0f;
            float wobble2 = ripe ? Mathf.Sin(simTime * 4.3f + _phase * 1.7f) * 4f : 0f;
            _cropRoot.localRotation = Quaternion.Euler(wobble2, 0f, wobble);
            var glow = Color.Lerp(Color.black, new Color(0.35f, 0.3f, 0.12f), _ripeGlow);
            for (int i = 0; i < _cropRenderers.Count; i++)
            {
                var r = _cropRenderers[i];
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(Prims.EmissionColorId, glow);
                r.SetPropertyBlock(_mpb);
            }

            // --- soil: ring glow + winter pale
            _ringGlow = Prims.Damp(_ringGlow, underRing ? 1f : 0f, 12f, dt);
            _winterBlend = Prims.Damp(_winterBlend, winter ? 1f : 0f, 4f, dt);
            var soil = Color.Lerp(FieldView.SoilColor, FieldView.SoilRingColor, _ringGlow);
            soil = Color.Lerp(soil, FieldView.SoilWinterColor, _winterBlend);
            _soil.GetPropertyBlock(_mpb);
            _mpb.SetColor(Prims.BaseColorId, soil);
            _mpb.SetColor(Prims.EmissionColorId, Color.Lerp(Color.black, new Color(0.25f, 0.18f, 0.06f), _ringGlow));
            _soil.SetPropertyBlock(_mpb);
        }
    }
}
