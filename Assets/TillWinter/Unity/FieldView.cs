using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Creates and updates one <see cref="PlotView"/> per plot from the catalogue; rebuilds when the field changes; owns the generation decor; plays the hoe's feedback.</summary>
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
            _game.Sim.Struck += OnStruck;
            _game.Sim.PlotBroken += OnBroken;
            _game.Sim.PlotWatered += OnWatered;
            _game.Sim.PlotRipened += OnRipened;
            _game.Sim.CrowAte += OnCrowAte;
            _game.Sim.PestStruck += OnPestStruck;
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
            _game.Sim.Struck -= OnStruck;
            _game.Sim.PlotBroken -= OnBroken;
            _game.Sim.PlotWatered -= OnWatered;
            _game.Sim.PlotRipened -= OnRipened;
            _game.Sim.CrowAte -= OnCrowAte;
            _game.Sim.PestStruck -= OnPestStruck;
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
            bool hand = e.Source == HarvestSource.Hand;
            // Swipe pitch: +2 % per crop in the swipe, capped at +30 %.
            _audio.HarvestPitch = 1f + Mathf.Min(0.3f, 0.02f * Mathf.Max(0, e.Combo - 1));
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
                // A long swipe makes the burst bigger and warmer: the most repeated moment in the game pays off.
                float streak = Mathf.Clamp01((e.Combo - 1) / 8f);
                var tint = Color.Lerp(TierColors[Mathf.Clamp(e.Tier, 0, 5)], Palette.Load().Golden, streak * 0.5f);
                _fx.Play(VfxId.Harvest, at, (hand ? 1f + e.Tier * 0.15f : 0.6f) * (1f + 0.35f * streak), tint);
                _audio.Play(SfxId.HarvestPop, hand ? 1f : 0.7f);
                if (hand) Haptics.Play(HapticKind.Light);
            }
        }

        /// <summary>A strike landed (GDD §2v3.4): the plot flinches, dirt flies, the number says how hard; a crit shakes it.</summary>
        private void OnStruck(StrikeEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            var at = _game.PlotToWorld(e.Pos, 0.25f);
            bool player = e.ApprenticeIndex < 0 && !e.Splash;
            if (_plots.TryGetValue(e.Pos, out var view)) view.Hit(e.Crit && !e.Splash ? 1f : e.Splash ? 0.35f : 0.7f);
            _fx.Play(VfxId.SoilPuff, at, e.Splash ? 0.5f : e.Crit ? 1.6f : e.Tired ? 0.6f : 1f);
            if (player)
            {
                _audio.Play(e.Crit ? SfxId.StrikeCrit : e.Tired ? SfxId.StrikeTired : SfxId.Strike);
                Haptics.Play(e.Crit ? HapticKind.Medium : HapticKind.Light);
            }
            else if (!e.Splash) _audio.Play(SfxId.Strike, 0.5f);
            HudView.Instance?.ShowDamage(_game.PlotToWorld(e.Pos, 0.6f), e.Damage, e.Crit && !e.Splash, e.Tired, e.Splash);
        }

        /// <summary>The ground gave way (GDD §2v3.3): a bigger puff, the pop, and the sprout that follows.</summary>
        private void OnBroken(BreakEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            var at = _game.PlotToWorld(e.Pos, 0.2f);
            if (_plots.TryGetValue(e.Pos, out var view)) view.Broke();
            _fx.Play(VfxId.SoilPuff, at, e.Hardpan ? 2.4f : 1.8f, e.Hardpan ? Palette.Load().Golden : Palette.Load().SoilDry);
            _fx.Play(VfxId.PlotPop, at);
            _fx.Play(VfxId.Sprout, _game.PlotToWorld(e.Pos, 0.25f));
            _audio.Play(SfxId.Break, e.Hardpan || e.Chest ? 1f : 0.85f);
            _audio.Play(SfxId.Sprout, 0.6f);
            Haptics.Play(e.Hardpan || e.Chest ? HapticKind.Heavy : HapticKind.Medium);
            if (e.Chest || e.Hardpan) HudView.Instance?.Flash(0.08f, 0.05f);
        }

        private void OnPestStruck(PestKind kind, GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            if (_plots.TryGetValue(pos, out var view)) view.Vanish();
        }

        private void OnWatered(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
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

        private float _waterSplash;

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
                CameraRig.Instance.Excitement = Mathf.Clamp01((state.Combo - 1) / 8f);
                CameraRig.Instance.PulledBack = state.IsWinter;
            }
            _sheen = Mathf.Max(0f, _sheen - dt / 1.2f);
            // The whole board breathes with the field's beat (GDD §2v3.4): a hair larger on the beat, still with Reduce motion.
            float env = state.Phase == Phase.Year ? 1f - Mathf.Clamp01(Mathf.Abs(state.Pulse - 0.5f) * 2f) : 0f;
            float breathe = SettingsStore.MotionAllowed ? 1f + 0.012f * Prims.EaseOutQuad(env) : 1f;
            transform.localScale = new Vector3(breathe, 1f, breathe);
            // The finger's watering keeps a drip going on its plot.
            var watering = state.WateringPos;
            if (watering.HasValue && state.Phase == Phase.Year && state.InBounds(watering.Value) && state.GetPlot(watering.Value).Watering)
            {
                _waterSplash -= dt;
                if (_waterSplash <= 0f)
                {
                    _waterSplash = 0.35f;
                    _fx.Play(VfxId.WaterSplash, _game.PlotToWorld(watering.Value, 0.2f), 0.7f);
                }
            }
            else _waterSplash = 0f;
            foreach (var kv in _plots)
            {
                var plot = state.GetPlot(kv.Key);
                kv.Value.Tick(plot, state.IsWinter, dt, t, _sheen);
            }
        }
    }

    /// <summary>
    /// One plot from the catalogue: soil whose palette slot reads the state (hard ground cracked by damage and rocky by
    /// depth, a growing crop on dark earth with droplets while it is watered), three crop stage prefabs toggled by
    /// growth, wobble + emissive glow when Ripe, golden = Golden slot + emission.
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
        private PaletteBinder[] _rockBinders;
        private float _rockShow;
        private GroundType _rockGround = (GroundType)(-1);
        private bool _rockGold;
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
        private float _hit, _hitStrength, _crackShow;
        private Vector3 _basePos;
        private bool _basePosSet;

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
                _crackBase = _cracks.transform.localScale;
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

        private Vector3 _crackBase = Vector3.one;

        /// <summary>The ground's material (GDD §2v3.3): three rocks laid out per grid position, coloured by the layer's type, hidden on clay.</summary>
        private void BuildRocks(Plot plot)
        {
            _rocks = new GameObject("Rocks").transform;
            _rocks.SetParent(transform, false);
            _rocks.localPosition = new Vector3(0f, 0.12f, 0f);
            int h = (plot.Pos.X * 92821) ^ (plot.Pos.Y * 68917);
            if (h < 0) h = -h;
            _rockBinders = new PaletteBinder[3];
            for (int i = 0; i < 3; i++)
            {
                var rock = _catalog.Spawn(_catalog.Rock, _rocks, "Rock" + i).transform;
                float a = (h % 360 + i * 120) * Mathf.Deg2Rad;
                float r = i == 0 ? 0.08f : 0.26f;
                rock.localPosition = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                rock.localRotation = Quaternion.Euler(0f, (h / 7 % 360) + i * 97f, 0f);
                rock.localScale = Vector3.one * (i == 0 ? 0.8f : 0.6f); // the catalogue rock is a pebble at plot scale
                _rockBinders[i] = rock.GetComponent<PaletteBinder>();
            }
            _rockShow = 0f;
            _rocks.localScale = Vector3.one * 0.0001f;
            _rocks.gameObject.SetActive(false);
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

        /// <summary>A strike: the plot flinches and, for a crit, shakes.</summary>
        public void Hit(float strength)
        {
            _hit = 1f;
            _hitStrength = Mathf.Max(_hitStrength * Mathf.Clamp01(_hit), strength);
        }

        /// <summary>The layer broke: the cracks vanish with the ground that carried them.</summary>
        public void Broke()
        {
            _hit = 1f;
            _hitStrength = 1f;
            _crackShow = 0f;
        }

        public void Tick(Plot plot, bool winter, float dt, float simTime, float sheen = 0f)
        {
            if (plot.Tier != _builtTier) BuildCrop(plot.Tier);

            bool hard = plot.State == PlotState.Hard;
            bool growing = plot.State == PlotState.Growing;
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
                if (hard) target = 0f;
                else if (growing) target = 0.35f + 0.65f * Prims.EaseOutQuad(Mathf.Clamp01(plot.Progress));
                else target = 1f;
                if (_vanish)
                {
                    _visualScale = Mathf.MoveTowards(_visualScale, 0f, dt / VanishSeconds);
                    if (_visualScale <= 0.001f) _vanish = false;
                }
                else _visualScale = Prims.Damp(_visualScale, target, winter ? 3f : 16f, dt);
                scale = _visualScale;
            }

            // The ripe model is the only thing that says "reap me", so it waits for Ripe.
            int stage = ripe ? 2 : growing && plot.Progress >= 0.45f ? 1 : 0;
            ShowStage(stage);

            _ripePunch = Mathf.Max(0f, _ripePunch - dt * 4f);
            float punch = 1f + 0.18f * Mathf.Sin(_ripePunch * Mathf.PI);
            // A stage swap eases in over a fifth of a second: sprout to stalk used to jump in one frame.
            _stageSwap = Mathf.Min(1f, _stageSwap + dt / StageSwapSeconds);
            float swap = Mathf.Lerp(0.62f, 1f, Prims.EaseOutBack(_stageSwap));
            float xz = Mathf.Lerp(0.7f, 1f, scale) * punch * swap;
            _cropRoot.localScale = new Vector3(xz, Mathf.Max(0.0001f, scale * punch * swap), xz);

            // Over-ripening (GDD §2.5 v1.6): the longer a crop stands, the duller it looks and the lower it hangs.
            float fresh = ripe && _game != null ? (float)_game.Sim.Freshness(plot) : 1f;
            float stale = Mathf.Clamp01((1f - fresh) / Mathf.Max(0.01f, 1f - (float)_game.Sim.Config.OverripeMinValue));
            _stale = Prims.Damp(_stale, ripe ? stale : 0f, 6f, dt);
            _ripeGlow = Prims.Damp(_ripeGlow, ripe ? 1f : 0f, 8f, dt);
            float wobble = ripe ? Mathf.Sin(simTime * 6f + _phase) * 7f : 0f;
            float wobble2 = ripe ? Mathf.Sin(simTime * 4.3f + _phase * 1.7f) * 4f : 0f;
            _cropRoot.localRotation = Quaternion.Euler(wobble2 + _stale * 9f, 0f, wobble);
            bool golden = plot.IsGolden && !winter && !hard;
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

            // A ready plant has to read "ready" from across the board: the ripe model's foliage warms toward the crop's colour.
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

            var palette = Palette.Load();
            // The ground's material (GDD §2v3.3): clay is bare; stone, roots, gravel and rock put rocks on the plot, in
            // their own colour; golden hardpan glints. The rocks sink as the cracks spread and vanish with the break.
            bool rocky = hard && !winter && plot.Ground != GroundType.Clay;
            if (rocky && (plot.Ground != _rockGround || plot.Hardpan != _rockGold) && _rockBinders != null)
            {
                _rockGround = plot.Ground;
                _rockGold = plot.Hardpan;
                Color rock = plot.Hardpan ? palette.Golden
                    : plot.Ground == GroundType.Roots ? palette.Wood
                    : plot.Ground == GroundType.Gravel ? palette.Path
                    : plot.Ground == GroundType.Rock ? palette.SoilBlock : palette.Stone;
                for (int i = 0; i < _rockBinders.Length; i++) _rockBinders[i]?.Override(PaletteSlot.Stone, rock);
            }
            float rockSize = rocky ? (0.6f + 0.15f * (int)plot.Ground) * (1f - 0.4f * plot.Cracks) : 0f;
            _rockShow = Prims.Damp(_rockShow, rockSize, rocky ? 10f : 14f, dt);
            bool showRocks = rocky || _rockShow > 0.02f;
            if (_rocks.gameObject.activeSelf != showRocks) _rocks.gameObject.SetActive(showRocks);
            if (showRocks) _rocks.localScale = Vector3.one * Mathf.Max(0.0001f, _rockShow);

            // The strike's flinch: a quick squash, and a shake for a crit.
            _hit = Mathf.Max(0f, _hit - dt * 6f);
            float flinch = Prims.EaseOutQuad(_hit) * _hitStrength;
            if (!_basePosSet) { _basePos = transform.localPosition; _basePosSet = true; }
            bool moving = SettingsStore.MotionAllowed;
            float shake = moving && _hitStrength >= 1f ? flinch * 0.05f : 0f;
            transform.localPosition = _basePos + new Vector3(Mathf.Sin(simTime * 61f + _phase) * shake, -0.03f * flinch, Mathf.Cos(simTime * 53f) * shake);
            if (_hit <= 0f) _hitStrength = 0f;

            // Soil: hard ground dry (darker the deeper it lies), a crop on dark earth, winter white, all through the binder.
            _wetBlend = Prims.Damp(_wetBlend, hard || winter ? 0f : 1f, 10f, dt);
            _winterBlend = Prims.Damp(_winterBlend, winter ? 1f : 0f, 4f, dt);
            var dry = Color.Lerp(palette.SoilDry, palette.SoilBlock, Mathf.Clamp01(plot.Layer / 12f) * 0.55f);
            if (plot.Hardpan && hard) dry = Color.Lerp(dry, palette.Golden, 0.35f);
            var soil = Color.Lerp(dry, palette.SoilWet, _wetBlend);
            if (plot.Kind == PlotKind.Fertile)
            {
                // Rich, dark earth: darker than any plain plot, so it reads at a glance in every phase.
                float a = soil.a;
                soil = Color.Lerp(soil, soil * 0.5f, 0.85f);
                soil.a = a;
            }
            _soilBinder.Override(PaletteSlot.SoilDry, soil);
            // Wet soil takes a faint cool sheen; the rain cloud's sweep adds more.
            _soilBinder.SetTintMultiplier(Color.Lerp(Color.white, palette.WetSheen, sheen * 0.6f + _wetBlend * (1f - _winterBlend) * 0.3f));
            // Cracks are the damage (GDD §2v3.4): none on fresh ground, spreading with every strike, gone with the break.
            float crackTarget = hard && !winter ? plot.Cracks : 0f;
            _crackShow = Prims.Damp(_crackShow, crackTarget, 14f, dt);
            bool showCracks = _crackShow > 0.03f;
            if (_cracks != null)
            {
                if (_cracks.activeSelf != showCracks) _cracks.SetActive(showCracks);
                if (showCracks) _cracks.transform.localScale = _crackBase * Mathf.Lerp(0.35f, 1f, _crackShow);
            }
            // Ready marker: pops in over a ripe bed, bobs and turns.
            if (_ripeMark != null)
            {
                _markShow = Prims.Damp(_markShow, ripe ? 1f : 0f, 12f, dt);
                bool showMark = _markShow > 0.02f;
                if (_ripeMark.gameObject.activeSelf != showMark) _ripeMark.gameObject.SetActive(showMark);
                if (showMark)
                {
                    float bob = moving ? 0.05f * Mathf.Sin(simTime * 3.2f + _phase) * (1f - _stale * 0.7f) : 0f;
                    _ripeMark.localPosition = new Vector3(0f, _ripeTop + bob, 0f);
                    _ripeMark.localRotation = Quaternion.Euler(0f, moving ? simTime * 90f + _phase * 40f : 45f, 0f);
                    _ripeMark.localScale = Vector3.one * Prims.EaseOutBack(_markShow);
                }
            }
            bool showDrops = growing && !winter && plot.Watering; // the droplets say "being watered", not "wet"
            if (_droplets != null && _droplets.activeSelf != showDrops) _droplets.SetActive(showDrops);
        }
    }
}
