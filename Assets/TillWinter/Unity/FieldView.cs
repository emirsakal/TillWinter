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
        private VfxPlayer _fx;
        private readonly HashSet<GridPos> _newPlots = new HashSet<GridPos>();
        private float _sheen;
        private static readonly Color[] TierColors = new Color[6];
        private AudioManager _audio;
        private VisualCatalog _catalog;
        private readonly Dictionary<GridPos, PlotView> _plots = new Dictionary<GridPos, PlotView>();
        private FarmDecorView _decor;
        private float _reveal = 99f;

        public void Init(GameController game, CameraRig camera, VfxPlayer fx, AudioManager audio, VisualCatalog catalog)
        {
            _game = game;
            _camera = camera;
            _fx = fx;
            _audio = audio;
            _catalog = catalog;
            var palette = Palette.Load();
            for (int t = 0; t < 6; t++) TierColors[t] = palette.Crop(t);
            Rebuild();
            _game.Sim.Harvested += OnHarvested;
            _game.Sim.PlotWatered += OnWatered;
            _game.Sim.PlotRipened += OnRipened;
            _game.Sim.PlotCleared += OnCleared;
            _game.Sim.PlotDriedOut += OnDriedOut;
            _game.Sim.CrowAte += OnCrowAte;
            _game.Sim.FieldExpanded += OnFieldExpanded;
            _game.Sim.RainCloudTapped += OnRainSweep;
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
            _game.Sim.PlotCleared -= OnCleared;
            _game.Sim.PlotDriedOut -= OnDriedOut;
            _game.Sim.PlotRipened -= OnRipened;
            _game.Sim.CrowAte -= OnCrowAte;
            _game.Sim.FieldExpanded -= OnFieldExpanded;
            _game.Sim.RainCloudTapped -= OnRainSweep;
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
                view.Init(plot, _catalog, _game);
                _plots.Add(plot.Pos, view);
                _newPlots.Add(plot.Pos);
            }
            foreach (var kv in _plots)
                kv.Value.transform.localPosition = _game.PlotToWorld(kv.Key);
            _camera.Frame(state.GridSize, false);
        }

        /// <summary>New generation: plots appear one by one bottom-left to top-right (0.03 s stagger). Tap after 0.5 s skips.</summary>
        private void OnGenerationStarted()
        {
            _newPlots.Clear();
            Rebuild();
            _reveal = 0f;
            foreach (var kv in _plots) { kv.Value.transform.localScale = Vector3.one * 0.001f; _newPlots.Add(kv.Key); }
            _fx.Play(VfxId.MeltSparkle, Vector3.zero);
        }

        /// <summary>Expansion: only the new plots pop in, staggered, each with a dust puff.</summary>
        private void OnFieldExpanded()
        {
            _newPlots.Clear();
            Rebuild();
            if (_newPlots.Count == 0) return;
            _reveal = 0f;
            foreach (var pos in _newPlots) _plots[pos].transform.localScale = Vector3.one * 0.001f;
            _audio.Play(SfxId.Expansion);
        }

        /// <summary>Rain cloud tapped: sweep across the field and a brief wet sheen on every plot.</summary>
        private void OnRainSweep()
        {
            _fx.Play(VfxId.RainSweep, Vector3.zero);
            _sheen = 1f;
        }

        public void SkipReveal()
        {
            _reveal = 99f;
            foreach (var kv in _plots) kv.Value.transform.localScale = Vector3.one;
            _newPlots.Clear();
            _decor.CompleteReveal();
        }

        private void OnHarvested(HarvestEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            var at = _game.PlotToWorld(e.Pos, 0.4f);
            if (_plots.TryGetValue(e.Pos, out var view)) view.Pop();
            bool ring = e.Source == HarvestSource.Ring;
            // Combo pitch: +2 % per step, capped at +30 %, reset on break (Combo is 0 then).
            _audio.HarvestPitch = 1f + Mathf.Min(0.3f, 0.02f * Mathf.Max(0, _game.State.Combo - 1));
            if (e.WasGolden)
            {
                _fx.Play(VfxId.HarvestGolden, at);
                _audio.Play(SfxId.GoldenHarvest);
                Haptics.Play(HapticKind.Medium);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.04f, 0.15f);
                HudView.Instance?.Flash(0.1f, 0.06f);
            }
            else
            {
                // The streak makes the burst bigger and warmer: the most repeated moment in the game pays off.
                float streak = Mathf.Clamp01((_game.State.Combo - 1) / 9f);
                var tint = Color.Lerp(TierColors[Mathf.Clamp(e.Tier, 0, 5)], Palette.Load().Golden, streak * 0.5f);
                _fx.Play(VfxId.Harvest, at, (ring ? 1f + e.Tier * 0.15f : 0.6f) * (1f + 0.35f * streak), tint);
                _audio.Play(SfxId.HarvestPop, ring ? 1f : 0.7f);
                if (ring) Haptics.Play(HapticKind.Light);
            }
        }

        /// <summary>The ring has cleared a stony plot (GDD §2.4 v1.8): the stones burst away in a puff of dirt.</summary>
        private void OnCleared(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            var at = _game.PlotToWorld(pos, 0.2f);
            _fx.Play(VfxId.SoilPuff, at, 1.8f);
            _fx.Play(VfxId.PlotPop, at);
            _audio.Play(SfxId.Expansion, 0.8f);
        }

        /// <summary>Summer drought (GDD §3.2 v1.9): a neglected Wet plot cracks dry in a small dust puff.</summary>
        private void OnDriedOut(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            _fx.Play(VfxId.SoilPuff, _game.PlotToWorld(pos, 0.2f), 0.8f);
        }

        private void OnWatered(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return; // hours away on resume: the field just shows where it is now
            if (_plots.TryGetValue(pos, out var view)) view.SproutPop();
            var at = _game.PlotToWorld(pos, 0.2f);
            _fx.Play(VfxId.WaterSplash, at);
            _audio.Play(SfxId.WaterSplash);
        }

        private void OnRipened(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            if (_plots.TryGetValue(pos, out var view)) view.RipePop();
            _fx.Play(VfxId.RipeSparkle, _game.PlotToWorld(pos, 0.6f));
        }

        private void OnCrowAte(CrowEvent e)
        {
            if (_plots.TryGetValue(e.Pos, out var view)) view.Vanish();
            _fx.Play(VfxId.SoilPuff, _game.PlotToWorld(e.Pos, 0.35f));
        }

        private void LateUpdate()
        {
            var state = _game.State;
            float dt = Time.deltaTime;
            float t = _game.SimTime;
            // Plots bought in Winter wait for the field to be in view: they land when the year starts.
            if (_reveal < 99f && state.Phase == Phase.Year)
            {
                _reveal += dt;
                int n = state.GridSize;
                bool done = true;
                int order = 0;
                foreach (var kv in _plots)
                {
                    if (!_newPlots.Contains(kv.Key)) continue;
                    float delay = (_newPlots.Count == _plots.Count ? kv.Key.Y * n + kv.Key.X : order++) * 0.03f;
                    float prev = Mathf.Clamp01((_reveal - dt - delay) / 0.25f);
                    float p = Mathf.Clamp01((_reveal - delay) / 0.25f);
                    if (prev <= 0f && p > 0f) _fx.Play(VfxId.PlotPop, _game.PlotToWorld(kv.Key, 0.15f));
                    if (p < 1f) done = false;
                    kv.Value.transform.localScale = Vector3.one * Mathf.Max(0.001f, p < 1f ? Mathf.Lerp(0.001f, 1.1f, Prims.EaseOutQuad(p)) : 1f);
                }
                if (_reveal > 0.5f && _game.Pointer != null && _game.Pointer.Current.Tapped) SkipReveal();
                else if (done) _reveal = 99f;
            }
            if (CameraRig.Instance != null)
            {
                CameraRig.Instance.Excitement = Mathf.Clamp01((state.Combo - 1) / 9f);
                CameraRig.Instance.PulledBack = state.IsWinter;
            }
            _sheen = Mathf.Max(0f, _sheen - dt / 1.2f);
            foreach (var kv in _plots)
            {
                var plot = state.GetPlot(kv.Key);
                bool underRing = !state.IsWinter && state.IsUnderRing(kv.Key);
                kv.Value.Tick(plot, state.IsWinter, underRing, dt, t, _sheen);
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
        private const float StageSwapSeconds = 0.2f;
        private const float VanishSeconds = 0.18f;

        private VisualCatalog _catalog;
        private GameController _game;
        private PaletteBinder _soilBinder;
        private GameObject _cracks, _droplets;
        private Transform _cropRoot;
        private Transform _ripeMark;
        private float _ripeTop = 0.9f;
        private float _markShow;
        private float _goldSpark;
        private float _stale;
        private Transform _rocks;
        private float _rockShow;
        private float _stageSwap = 1f;
        private readonly GameObject[] _stages = new GameObject[3];
        private readonly PaletteBinder[] _stageBinders = new PaletteBinder[3];
        private int _builtTier = -1;
        private int _stage = -1;
        private bool _golden;
        private bool _ripeTinted;

        private float _visualScale;
        private float _pop = -1f;
        private bool _vanish;
        private float _ripeGlow;
        private float _wetBlend;
        private float _winterBlend;
        private float _phase;
        private float _ripePunch;

        public void Init(Plot plot, VisualCatalog catalog, GameController game)
        {
            _catalog = catalog;
            _game = game;
            _phase = (plot.Pos.X * 7 + plot.Pos.Y * 13) * 0.37f;
            _soilBinder = GetComponent<PaletteBinder>();
            _cracks = transform.Find("Cracks")?.gameObject;
            // Every plot instantiates the same prefab, whose crack layout was baked once with a fixed seed, so a
            // field read as one stamped tile repeated. A deterministic quarter-turn, a small skew and a mirror per
            // grid position give each plot its own dirt without touching the shared mesh.
            if (_cracks != null)
            {
                int h = (plot.Pos.X * 73856093) ^ (plot.Pos.Y * 19349663);
                if (h < 0) h = -h;
                _cracks.transform.localRotation = Quaternion.Euler(0f, (h % 4) * 90f + (h / 4 % 5) * 6f, 0f);
                var cs = _cracks.transform.localScale;
                _cracks.transform.localScale = new Vector3(cs.x * ((h / 32 % 2) == 0 ? 1f : -1f), cs.y, cs.z);
            }
            _droplets = transform.Find("Droplets")?.gameObject;
            _cropRoot = transform.Find("CropAnchor");
            if (_cropRoot == null)
            {
                _cropRoot = new GameObject("CropAnchor").transform;
                _cropRoot.SetParent(transform, false);
                _cropRoot.localPosition = new Vector3(0f, 0.2f, 0f);
            }
            _ripeMark = transform.Find("RipeMark");
            BuildRocks(plot);
            BuildCrop(plot.Tier);
            _cropRoot.localScale = Vector3.one * 0.0001f;
        }

        /// <summary>Stony ground (GDD §2.4 v1.8): three rocks laid out per grid position, hidden on any other plot.</summary>
        private void BuildRocks(Plot plot)
        {
            _rocks = new GameObject("Rocks").transform;
            _rocks.SetParent(transform, false);
            _rocks.localPosition = new Vector3(0f, 0.12f, 0f);
            int h = (plot.Pos.X * 92821) ^ (plot.Pos.Y * 68917);
            if (h < 0) h = -h;
            for (int i = 0; i < 3; i++)
            {
                var rock = _catalog.Spawn(_catalog.Rock, _rocks, "Rock" + i).transform;
                float a = (h % 360 + i * 120) * Mathf.Deg2Rad;
                float r = i == 0 ? 0.08f : 0.26f;
                rock.localPosition = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                rock.localRotation = Quaternion.Euler(0f, (h / 7 % 360) + i * 97f, 0f);
                rock.localScale = Vector3.one * (i == 0 ? 0.8f : 0.6f); // the catalogue rock is a pebble at plot scale
            }
            _rockShow = plot.IsStony ? 1f : 0f;
            _rocks.localScale = Vector3.one * Mathf.Max(0.0001f, _rockShow);
            _rocks.gameObject.SetActive(plot.IsStony);
        }

        private void BuildCrop(int tier)
        {
            for (int i = 0; i < 3; i++) if (_stages[i] != null) Destroy(_stages[i]);
            _builtTier = tier;
            var visual = _catalog.Crops != null && tier < _catalog.Crops.Length ? _catalog.Crops[tier] : null;
            var rootScale = _cropRoot.localScale;
            var rootRot = _cropRoot.localRotation;
            _cropRoot.localScale = Vector3.one;
            _cropRoot.localRotation = Quaternion.identity;
            for (int i = 0; i < 3; i++)
            {
                _stages[i] = _catalog.Spawn(visual?.Stage(i), _cropRoot, "Stage" + i);
                _stageBinders[i] = _stages[i].GetComponent<PaletteBinder>();
                if (i == 2)
                {
                    // The ready marker floats just over the ripe bed, whatever its height (corn is three times a carrot).
                    float top = 0f;
                    foreach (var r in _stages[i].GetComponentsInChildren<Renderer>())
                        top = Mathf.Max(top, transform.InverseTransformPoint(r.bounds.max).y);
                    _ripeTop = top + 0.22f;
                }
                _stages[i].SetActive(false);
            }
            _cropRoot.localScale = rootScale;
            _cropRoot.localRotation = rootRot;
            if (_ripeMark != null) _soilBinder.Override(PaletteSlot.Crop0, Palette.Load().Crop(tier));
            _stage = -1;
            _golden = false;
        }

        private void ShowStage(int stage)
        {
            if (stage == _stage) return;
            bool sprouted = _stage == 0 && stage == 1;
            _stage = stage;
            for (int i = 0; i < 3; i++) _stages[i].SetActive(i == stage);
            _stageSwap = 0f; // the new model comes up out of the old one instead of appearing at full size
            if (sprouted && _game != null && !_game.Sim.IsSimulatingOffline)
            {
                VfxPlayer.Fire(VfxId.Sprout, transform.position + Vector3.up * 0.25f);
                AudioManager.Instance?.Play(SfxId.Sprout);
            }
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
                    b.SetEmission(palette.GoldenGlow * (pulse * 1.35f)); // a touch past white, so bloom catches it
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

        public void Tick(Plot plot, bool winter, bool underRing, float dt, float simTime, float sheen = 0f)
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

            // The ripe model is the only thing that says "harvest me", so it waits for Ripe: an almost-grown plot
            // used to show the same orange carrot as a ready one, which read as the reverse of the truth.
            int stage = ripe ? 2 : wet && plot.Progress >= 0.45f ? 1 : 0;
            ShowStage(stage);

            _ripePunch = Mathf.Max(0f, _ripePunch - dt * 4f);
            float punch = 1f + 0.18f * Mathf.Sin(_ripePunch * Mathf.PI);
            float squash = ripe && underRing ? 1f - 0.2f * plot.Progress : 1f;
            // A stage swap eases in over a fifth of a second: sprout to stalk used to jump in one frame.
            _stageSwap = Mathf.Min(1f, _stageSwap + dt / StageSwapSeconds);
            float swap = Mathf.Lerp(0.62f, 1f, Prims.EaseOutBack(_stageSwap));
            float xz = Mathf.Lerp(0.7f, 1f, scale) * punch * swap / Mathf.Sqrt(squash);
            _cropRoot.localScale = new Vector3(xz, Mathf.Max(0.0001f, scale * punch * squash * swap), xz);

            // Over-ripening (GDD §2.5 v1.6): the longer a crop stands, the duller it looks and the lower it hangs.
            float fresh = ripe && _game != null ? (float)_game.Sim.Freshness(plot) : 1f;
            float stale = Mathf.Clamp01((1f - fresh) / Mathf.Max(0.01f, 1f - (float)_game.Sim.Config.OverripeMinValue));
            _stale = Prims.Damp(_stale, ripe ? stale : 0f, 6f, dt);
            _ripeGlow = Prims.Damp(_ripeGlow, ripe ? 1f : 0f, 8f, dt);
            float wobble = ripe ? Mathf.Sin(simTime * 6f + _phase) * 7f : 0f;
            float wobble2 = ripe ? Mathf.Sin(simTime * 4.3f + _phase * 1.7f) * 4f : 0f;
            _cropRoot.localRotation = Quaternion.Euler(wobble2 + _stale * 9f, 0f, wobble);
            bool golden = plot.IsGolden && !winter;
            if (golden || _golden) SetGolden(golden, simTime);
            // A golden crop keeps shedding sparkles (the VFX pool rate-limits them with every other burst).
            if (golden && SettingsStore.MotionAllowed && _game != null && !_game.Sim.IsSimulatingOffline)
            {
                _goldSpark -= dt;
                if (_goldSpark <= 0f)
                {
                    _goldSpark = 0.7f + (_phase % 1f) * 0.3f;
                    VfxPlayer.Fire(VfxId.RipeSparkle, transform.position + Vector3.up * (0.4f + _visualScale * 0.5f), 0.8f);
                }
            }

            // A ready plant has to read "ready" from across the board. Kenney's carrot is mostly leafy top with the
            // root at soil level, so at this camera angle a ripe one looked as green as a growing one whatever the
            // stage rule said; the ripe model's foliage warms toward the crop's own colour instead. Golden keeps its
            // own override, and the tint is dropped the moment the plot stops being ripe.
            bool warmRipe = ripe && !golden;
            if (warmRipe != _ripeTinted)
            {
                _ripeTinted = warmRipe;
                var ripeBinder = _stageBinders[2];
                if (ripeBinder != null)
                {
                    if (warmRipe) ripeBinder.Override(PaletteSlot.Sprout, Color.Lerp(Palette.Load().Sprout, Palette.Load().Crop(plot.Tier), 0.7f));
                    else ripeBinder.ClearOverride(PaletteSlot.Sprout);
                }
            }

            var stageBinder = _stageBinders[stage];
            float breathe = ripe ? 0.75f + 0.25f * Mathf.Sin(simTime * 3f + _phase) : 0f;
            float pulse = Mathf.Clamp01(_ripePunch) * 0.8f;
            if (stageBinder != null && !golden)
            {
                stageBinder.SetEmission(Color.Lerp(Color.black, Palette.Load().RipeGlow, (_ripeGlow * breathe + pulse) * (1f - _stale * 0.8f)));
                stageBinder.SetTintMultiplier(Color.Lerp(Color.white, Palette.Load().StaleTint, _stale)); // a crop past its best goes dull
            }

            // Stones sink as the ring works them loose, shiver under it, and are gone once the plot is cleared.
            bool stony = plot.IsStony;
            _rockShow = Prims.Damp(_rockShow, stony ? 1f - 0.45f * plot.Progress : 0f, stony ? 10f : 14f, dt);
            bool showRocks = stony || _rockShow > 0.02f;
            if (_rocks.gameObject.activeSelf != showRocks) _rocks.gameObject.SetActive(showRocks);
            if (showRocks)
            {
                float shiver = stony && underRing && SettingsStore.MotionAllowed ? 0.025f * Mathf.Sin(simTime * 47f + _phase) : 0f;
                _rocks.localPosition = new Vector3(shiver, 0.12f, 0f);
                _rocks.localScale = Vector3.one * Mathf.Max(0.0001f, _rockShow);
            }

            // Soil: Dry -> Wet -> winter white, all through the binder. The ring shows only as its round decal:
            // no per-tile tint or lift, which drew square highlights under a round ring.
            _wetBlend = Prims.Damp(_wetBlend, dry || winter ? 0f : 1f, 10f, dt);
            _winterBlend = Prims.Damp(_winterBlend, winter ? 1f : 0f, 4f, dt);
            var palette = Palette.Load();
            var soil = Color.Lerp(palette.SoilDry, palette.SoilWet, _wetBlend);
            if (plot.Kind == PlotKind.Fertile)
            {
                // Rich, dark earth: darker than any wet plot, so it reads at a glance in every phase.
                float a = soil.a;
                soil = Color.Lerp(soil, soil * 0.5f, 0.85f);
                soil.a = a;
            }
            _soilBinder.Override(PaletteSlot.SoilDry, soil);
            // Wet soil takes a faint cool sheen; the rain cloud's sweep adds more.
            _soilBinder.SetTintMultiplier(Color.Lerp(Color.white, Palette.Load().WetSheen, sheen * 0.6f + _wetBlend * (1f - _winterBlend) * 0.3f));
            bool showCracks = _wetBlend < 0.5f && _winterBlend < 0.5f;
            if (_cracks != null && _cracks.activeSelf != showCracks) _cracks.SetActive(showCracks);
            // Ready marker: pops in over a ripe bed, bobs and turns; hidden under the ring while it is being harvested.
            if (_ripeMark != null)
            {
                _markShow = Prims.Damp(_markShow, ripe && !underRing ? 1f : 0f, 12f, dt);
                bool showMark = _markShow > 0.02f;
                if (_ripeMark.gameObject.activeSelf != showMark) _ripeMark.gameObject.SetActive(showMark);
                if (showMark)
                {
                    bool moving = SettingsStore.MotionAllowed; // Reduce motion: the gem simply stands there
                    float bob = moving ? 0.05f * Mathf.Sin(simTime * 3.2f + _phase) * (1f - _stale * 0.7f) : 0f;
                    _ripeMark.localPosition = new Vector3(0f, _ripeTop + bob, 0f);
                    _ripeMark.localRotation = Quaternion.Euler(0f, moving ? simTime * 90f + _phase * 40f : 45f, 0f);
                    _ripeMark.localScale = Vector3.one * Prims.EaseOutBack(_markShow);
                }
            }
            bool showDrops = wet && !winter; // the whole wet phase, not just its first half
            if (_droplets != null && _droplets.activeSelf != showDrops) _droplets.SetActive(showDrops);
        }
    }
}
