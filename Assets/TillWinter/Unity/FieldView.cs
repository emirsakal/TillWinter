using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Creates and updates one <see cref="PlotView"/> per plot from the catalogue; rebuilds when the field changes; owns the generation decor.</summary>
    public sealed class FieldView : MonoBehaviour
    {
        private GameController _game;
        private CameraRig _camera;
        private FxManager _fx;
        private AudioManager _audio;
        private VisualCatalog _catalog;
        private readonly Dictionary<GridPos, PlotView> _plots = new Dictionary<GridPos, PlotView>();
        private FarmDecorView _decor;
        private float _reveal = 99f;

        public void Init(GameController game, CameraRig camera, FxManager fx, AudioManager audio, VisualCatalog catalog)
        {
            _game = game;
            _camera = camera;
            _fx = fx;
            _audio = audio;
            _catalog = catalog;
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
            _decor.Init(game, catalog);
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
                var go = _catalog.Spawn(_catalog.Plot, transform, "Plot");
                go.name = "Plot " + plot.Pos;
                var view = go.AddComponent<PlotView>();
                view.Init(plot, _catalog);
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

    /// <summary>
    /// One plot from the catalogue: soil whose palette slot reads the state (Dry cracked, Wet dark + droplets),
    /// three crop stage prefabs toggled by Wet progress, wobble + emissive glow when Ripe, golden = Golden slot + emission.
    /// </summary>
    public sealed class PlotView : MonoBehaviour
    {
        private const float PopSeconds = 0.24f;
        private const float VanishSeconds = 0.18f;

        private VisualCatalog _catalog;
        private PaletteBinder _soilBinder;
        private GameObject _cracks, _droplets;
        private Transform _cropRoot;
        private readonly GameObject[] _stages = new GameObject[3];
        private readonly PaletteBinder[] _stageBinders = new PaletteBinder[3];
        private int _builtTier = -1;
        private int _stage = -1;
        private bool _golden;

        private float _visualScale;
        private float _pop = -1f;
        private bool _vanish;
        private float _ripeGlow;
        private float _ringGlow;
        private float _wetBlend;
        private float _winterBlend;
        private float _phase;
        private float _ripePunch;
        private float _lift;

        public void Init(Plot plot, VisualCatalog catalog)
        {
            _catalog = catalog;
            _phase = (plot.Pos.X * 7 + plot.Pos.Y * 13) * 0.37f;
            _soilBinder = GetComponent<PaletteBinder>();
            _cracks = transform.Find("Cracks")?.gameObject;
            _droplets = transform.Find("Droplets")?.gameObject;
            _cropRoot = transform.Find("CropAnchor");
            if (_cropRoot == null)
            {
                _cropRoot = new GameObject("CropAnchor").transform;
                _cropRoot.SetParent(transform, false);
                _cropRoot.localPosition = new Vector3(0f, 0.16f, 0f);
            }
            BuildCrop(plot.Tier);
            _cropRoot.localScale = Vector3.one * 0.0001f;
        }

        private void BuildCrop(int tier)
        {
            for (int i = 0; i < 3; i++) if (_stages[i] != null) Destroy(_stages[i]);
            _builtTier = tier;
            var visual = _catalog.Crops != null && tier < _catalog.Crops.Length ? _catalog.Crops[tier] : null;
            for (int i = 0; i < 3; i++)
            {
                _stages[i] = _catalog.Spawn(visual?.Stage(i), _cropRoot, "Stage" + i);
                _stageBinders[i] = _stages[i].GetComponent<PaletteBinder>();
                _stages[i].SetActive(false);
            }
            _stage = -1;
            _golden = false;
        }

        private void ShowStage(int stage)
        {
            if (stage == _stage) return;
            _stage = stage;
            for (int i = 0; i < 3; i++) _stages[i].SetActive(i == stage);
        }

        private void SetGolden(bool golden, float simTime)
        {
            var palette = Palette.Load();
            float pulse = golden ? 0.6f + 0.4f * Mathf.Sin(simTime * 2.5f + _phase) : 0f;
            for (int i = 0; i < 3; i++)
            {
                var b = _stageBinders[i];
                if (b == null) continue;
                if (golden)
                {
                    for (int t = 0; t < 6; t++) b.Override((PaletteSlot)((int)PaletteSlot.Crop0 + t), palette.Golden);
                    b.SetEmission(palette.GoldenGlow * pulse);
                }
                else if (_golden)
                {
                    for (int t = 0; t < 6; t++) b.ClearOverride((PaletteSlot)((int)PaletteSlot.Crop0 + t));
                    b.SetEmission(Color.black);
                }
            }
            _golden = golden;
        }

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

        public void SproutPop() => _ripePunch = 0.6f;

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
                else if (wet) target = 0.35f + 0.65f * Prims.EaseOutQuad(Mathf.Clamp01(plot.Progress));
                else target = 1f;
                if (_vanish)
                {
                    _visualScale = Mathf.MoveTowards(_visualScale, 0f, dt / VanishSeconds);
                    if (_visualScale <= 0.001f) _vanish = false;
                }
                else _visualScale = Prims.Damp(_visualScale, target, winter ? 3f : 16f, dt);
                scale = _visualScale;
            }

            // Stage by Wet progress: 0–0.33 sprout, 0.33–0.8 growing, 0.8–1 and Ripe = ripe mesh.
            int stage = ripe || (wet && plot.Progress >= 0.8f) ? 2 : wet && plot.Progress >= 0.33f ? 1 : 0;
            ShowStage(stage);

            _ripePunch = Mathf.Max(0f, _ripePunch - dt * 4f);
            float punch = 1f + 0.18f * Mathf.Sin(_ripePunch * Mathf.PI);
            float squash = ripe && underRing ? 1f - 0.2f * plot.Progress : 1f;
            float xz = Mathf.Lerp(0.7f, 1f, scale) * punch / Mathf.Sqrt(squash);
            _cropRoot.localScale = new Vector3(xz, Mathf.Max(0.0001f, scale * punch * squash), xz);

            _ripeGlow = Prims.Damp(_ripeGlow, ripe ? 1f : 0f, 8f, dt);
            float wobble = ripe ? Mathf.Sin(simTime * 6f + _phase) * 7f : 0f;
            float wobble2 = ripe ? Mathf.Sin(simTime * 4.3f + _phase * 1.7f) * 4f : 0f;
            _cropRoot.localRotation = Quaternion.Euler(wobble2, 0f, wobble);
            bool golden = plot.IsGolden && !winter;
            if (golden || _golden) SetGolden(golden, simTime);
            var stageBinder = _stageBinders[stage];
            if (stageBinder != null && !golden) stageBinder.SetEmission(Color.Lerp(Color.black, new Color(0.3f, 0.24f, 0.08f), _ripeGlow));

            // Soil: Dry -> Wet -> ring lift -> winter white, all through the binder.
            _wetBlend = Prims.Damp(_wetBlend, dry || winter ? 0f : 1f, 10f, dt);
            _ringGlow = Prims.Damp(_ringGlow, underRing ? 1f : 0f, 12f, dt);
            _winterBlend = Prims.Damp(_winterBlend, winter ? 1f : 0f, 4f, dt);
            var palette = Palette.Load();
            var soil = Color.Lerp(palette.SoilDry, palette.SoilWet, _wetBlend);
            soil = Color.Lerp(soil, palette.SoilRing, _ringGlow * 0.6f);
            _soilBinder.Override(PaletteSlot.SoilDry, soil);
            _soilBinder.SetTintMultiplier(Color.Lerp(Color.white, new Color(1.18f, 1.14f, 1.05f), _ringGlow));
            _soilBinder.SetEmission(Color.Lerp(Color.black, new Color(0.2f, 0.14f, 0.04f), _ringGlow));
            _lift = Prims.Damp(_lift, underRing ? 0.05f : 0f, 12f, dt);
            var pos = transform.localPosition;
            transform.localPosition = new Vector3(pos.x, _lift, pos.z);
            bool showCracks = _wetBlend < 0.5f && _winterBlend < 0.5f;
            if (_cracks != null && _cracks.activeSelf != showCracks) _cracks.SetActive(showCracks);
            bool showDrops = wet && !winter && plot.Progress < 0.5f;
            if (_droplets != null && _droplets.activeSelf != showDrops) _droplets.SetActive(showDrops);
        }
    }
}
