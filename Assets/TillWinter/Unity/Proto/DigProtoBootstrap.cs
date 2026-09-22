#if TW_DEBUG || UNITY_EDITOR
using System.Collections.Generic;
using TillWinter.Core.Dig;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TillWinter.Unity.Proto
{
    /// <summary>
    /// The playable core-loop v3 prototype (GDD §2v3.13): boxes, numbers and the real rules from <see cref="DigSim"/>,
    /// nothing else. Built in code into its own scene (Proto.unity, editor menu "Till Winter/Core v3 prototype"),
    /// never in a release build. English strings on purpose: this screen exists to answer one question by hand —
    /// does the strike feel good — and is thrown away or replaced by the real presentation afterwards.
    /// </summary>
    public sealed class DigProtoBootstrap : MonoBehaviour
    {
        private const float TileSize = 270f, TileGap = 22f, BoardY = -1120f;
        private const float HoldSeconds = 0.25f, DragPixels = 30f;

        private DigSim _sim;
        private RectTransform _canvas, _board;
        private Image[] _tileBg, _hpFill, _growFill, _crowMark;
        private TMP_Text[] _tileLabel, _layerLabel;
        private RectTransform[] _tileRt;
        private Image _pulse, _pulseCore, _staminaFill;
        private TMP_Text _coins, _year, _stamina, _hint, _stats;
        private GameObject _winter;
        private TMP_Text _winterCoins;
        private readonly List<(Button button, DigSim.Upgrade upgrade, TMP_Text label)> _shop = new List<(Button, DigSim.Upgrade, TMP_Text)>();
        private readonly List<(TMP_Text text, float born, Vector2 from)> _numbers = new List<(TMP_Text, float, Vector2)>();
        private readonly Queue<TMP_Text> _numberPool = new Queue<TMP_Text>();
        private AudioManager _audio;

        // input
        private bool _wasDown, _dragging, _watering;
        private float _downTime;
        private Vector2 _downPos;
        private DigTile _downTile, _selected;
        private readonly List<DigTile> _path = new List<DigTile>();
        private readonly float[] _shake = new float[9];
        private int _seed = 1;

        private void Awake()
        {
            Time.timeScale = 1f; // the pause menu leaves 0 behind in the editor; the prototype's clock would never move
            Application.targetFrameRate = 60;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiPalette.Night;
            _audio = new GameObject("Audio").AddComponent<AudioManager>();
            _audio.transform.SetParent(transform, false);
            _audio.Init();
            _canvas = GameBootstrap.BuildCanvas(transform);
            NewGame(_seed);
            BuildUi();
        }

        private void NewGame(int seed)
        {
            _sim = new DigSim(new DigConfig(), seed);
            _selected = _sim.Tiles[4];
            _sim.Struck += OnStruck;
            _sim.Broke += OnBroke;
            _sim.ReapedEvent += OnReaped;
            _sim.YearEnded += OnYearEnded;
            _sim.CrowLanded += OnCrowLanded;
            _sim.CrowAte += OnCrowAte;
        }

        private void OnYearEnded(int year) => ShowWinter();
        private void OnCrowLanded(DigTile t) => _audio.Play(SfxId.CrowCaw);
        private void OnCrowAte(DigTile t) => _audio.Play(SfxId.Denied);

        // ------------------------------------------------------------------ build

        private void BuildUi()
        {
            var safe = UiKit.Rect("Safe", _canvas);
            SafeArea.Apply(safe);
            _coins = UiKit.Label(safe, "Coins", "0", UiType.Hero, UiPalette.Honey, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_coins.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 130f));
            _year = UiKit.Label(safe, "Year", "", UiType.Body, UiPalette.Cream, TextAnchor.MiddleCenter);
            UiKit.Box(_year.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 50f));
            var track = UiKit.Panel(safe, "StaminaTrack", UiPalette.WithAlpha(UiPalette.Cream, 0.15f), true, false);
            UiKit.Box(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 34f));
            _staminaFill = UiKit.Panel(track.transform, "Fill", UiPalette.Sage, true, false);
            UiKit.Stretch(_staminaFill.rectTransform, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _stamina = UiKit.Label(track.transform, "Text", "", UiType.Caption, UiPalette.Night, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_stamina.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _stats = UiKit.Label(safe, "Stats", "", UiType.Caption, UiPalette.WithAlpha(UiPalette.Cream, 0.7f), TextAnchor.MiddleCenter);
            UiKit.Box(_stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1000f, 40f));

            _board = UiKit.Rect("Board", safe);
            float side = 3 * TileSize + 2 * TileGap;
            UiKit.Box(_board, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, BoardY), new Vector2(side, side));
            int n = _sim.Tiles.Length;
            _tileBg = new Image[n]; _hpFill = new Image[n]; _growFill = new Image[n]; _crowMark = new Image[n];
            _tileLabel = new TMP_Text[n]; _layerLabel = new TMP_Text[n]; _tileRt = new RectTransform[n];
            // The pulse ring sits under the tiles, so the tile it belongs to reads as lit from below.
            // A rounded halo behind the selected tile, wider than the gaps: it closes onto the tile once a second.
            _pulse = UiKit.Panel(_board, "Pulse", UiPalette.WithAlpha(UiPalette.Honey, 0.55f), true, false);
            UiKit.Box(_pulse.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize, TileSize));
            _pulseCore = UiKit.Panel(_board, "PulseCore", UiPalette.WithAlpha(UiPalette.Cream, 0f), true, false);
            UiKit.Box(_pulseCore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize * 1.08f, TileSize * 1.08f));
            for (int i = 0; i < n; i++)
            {
                var t = _sim.Tiles[i];
                var rt = UiKit.Rect("Tile " + i, _board);
                _tileRt[i] = rt;
                UiKit.Box(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TilePos(t), new Vector2(TileSize, TileSize));
                _tileBg[i] = UiKit.Panel(rt, "Bg", UiPalette.Earth, true, false);
                _tileLabel[i] = UiKit.Label(rt, "Label", "", UiType.Body, UiPalette.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(_tileLabel[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 40f), new Vector2(-8f, -40f));
                _layerLabel[i] = UiKit.Label(rt, "Layer", "", UiType.Caption, UiPalette.WithAlpha(UiPalette.Cream, 0.8f), TextAnchor.LowerLeft);
                UiKit.Box(_layerLabel[i].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 8f), new Vector2(120f, 32f));
                var hpTrack = UiKit.Panel(rt, "HpTrack", UiPalette.WithAlpha(UiPalette.Night, 0.5f), true, false);
                UiKit.Box(hpTrack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(TileSize - 32f, 18f));
                _hpFill[i] = UiKit.Panel(hpTrack.transform, "Fill", UiPalette.Brick, true, false);
                UiKit.Stretch(_hpFill[i].rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _growFill[i] = UiKit.Panel(hpTrack.transform, "Grow", UiPalette.Sage, true, false);
                UiKit.Stretch(_growFill[i].rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _crowMark[i] = UiKit.CircleImage(rt, "Crow", UiPalette.Night, new Vector2(TileSize * 0.3f, -TileSize * 0.3f), 64f);
                var crowText = UiKit.Label(_crowMark[i].transform, "T", "!", UiType.Heading, UiPalette.Honey, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(crowText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _crowMark[i].gameObject.SetActive(false);
            }

            _hint = UiKit.Label(safe, "Hint", "Tap hard ground on the beat to strike.  Hold a sprout to water it.  Swipe across ripe crops to reap.  Tap a crow.",
                UiType.Label, UiPalette.WithAlpha(UiPalette.Cream, 0.75f), TextAnchor.MiddleCenter);
            _hint.enableWordWrapping = true;
            UiKit.Box(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 120f));
            var restart = UiKit.Button(safe, "Restart", "Restart (new seed)", UiType.Caption, UiPalette.WithAlpha(UiPalette.Cream, 0.2f), UiPalette.Cream, () => Restart(_seed + 1));
            UiKit.Box(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(420f, 80f));

            BuildWinter(safe);
            for (int i = 0; i < 12; i++)
            {
                var num = UiKit.Label(_board, "Number", "", UiType.Heading, UiPalette.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Box(num.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 100f));
                num.gameObject.SetActive(false);
                _numberPool.Enqueue(num);
            }
        }

        private void BuildWinter(RectTransform safe)
        {
            var overlay = UiKit.Panel(safe, "Winter", UiPalette.WithAlpha(UiPalette.Night, 0.92f), false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _winter = overlay.gameObject;
            var title = UiKit.Label(overlay.transform, "Title", "Winter", UiType.Title, UiPalette.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 100f));
            _winterCoins = UiKit.Label(overlay.transform, "Coins", "", UiType.Heading, UiPalette.Honey, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_winterCoins.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(900f, 80f));
            var rows = new (DigSim.Upgrade u, string name)[]
            {
                (DigSim.Upgrade.Damage, "Hoe damage +1"), (DigSim.Upgrade.Crit, "Steady hand: crit window +0.03"),
                (DigSim.Upgrade.CritChance, "Lucky hoe: crit chance +3%"),
                (DigSim.Upgrade.Stamina, "Stamina depot +15"), (DigSim.Upgrade.Regen, "Stamina regen +0.25/s"),
                (DigSim.Upgrade.Growth, "Growth +10%"), (DigSim.Upgrade.Tier, "Next crop tier"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var u = rows[i].u;
                var b = UiKit.Button(overlay.transform, "Buy" + u, rows[i].name, UiType.Body, UiPalette.Sage, UiPalette.Cream, () => { if (_sim.Buy(u)) { _audio.Play(SfxId.Purchase); RefreshWinter(); } else _audio.Play(SfxId.Denied); });
                UiKit.Box(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -540f - i * 112f), new Vector2(860f, 96f));
                var cost = UiKit.Label(b.transform, "Cost", "", UiType.Body, UiPalette.Honey, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.Stretch(cost.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-24f, 0f));
                _shop.Add((b, u, cost));
            }
            var next = UiKit.Button(overlay.transform, "NextYear", "Next year  »", UiType.Title, UiPalette.Honey, UiPalette.Night, () => { _sim.StartNextYear(); _winter.SetActive(false); _audio.Play(SfxId.NewGeneration); });
            UiKit.Box(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(860f, 130f));
            _winter.SetActive(false);
        }

        private Vector2 TilePos(DigTile t)
        {
            float step = TileSize + TileGap;
            return new Vector2((t.X - 1) * step, (1 - t.Y) * step);
        }

        private void Restart(int seed)
        {
            _seed = seed;
            NewGame(seed); // the old sim is dropped with its subscriptions; the new one subscribes itself
            _winter.SetActive(false);
        }

        // ------------------------------------------------------------------ events

        private void OnStruck(DigTile t, double dmg, bool crit)
        {
            bool tired = _sim.LastStrikeTired;
            SpawnNumber(TilePos(t) + new Vector2(0f, 90f), (crit ? "CRIT " : tired ? "tired " : "") + dmg.ToString("0"),
                crit ? UiPalette.Honey : tired ? UiPalette.WithAlpha(UiPalette.Cream, 0.45f) : UiPalette.Cream, crit ? 1.6f : tired ? 0.8f : 1f);
            _shake[Index(t)] = crit ? 1f : tired ? 0.2f : 0.45f;
            _audio.Play(crit ? SfxId.PestStruck : SfxId.Step, crit ? 1f : tired ? 0.5f : 0.8f, crit ? 1.1f : tired ? 0.85f : 1f);
            if (crit) Haptics.Play(HapticKind.Medium); else if (!tired) Haptics.Play(HapticKind.Light);
        }

        private void OnBroke(DigTile t, double coins)
        {
            SpawnNumber(TilePos(t) + new Vector2(0f, 60f), "+" + coins.ToString("0") + (t.Chest ? "  CHEST" : t.Golden ? "  GOLD" : ""), UiPalette.Honey, t.Chest || t.Golden ? 1.8f : 1.2f);
            _audio.Play(t.Chest || t.Golden ? SfxId.GoldenHarvest : SfxId.Expansion);
            Haptics.Play(HapticKind.Heavy);
        }

        private void OnReaped(int n, double coins)
        {
            SpawnNumber(Vector2.zero, "+" + coins.ToString("0") + (n > 1 ? "  x" + n : ""), UiPalette.Sage, n > 1 ? 1.6f : 1.1f);
            _audio.Play(SfxId.HarvestPop, 1f, 1f + 0.05f * Mathf.Min(n, 8));
            Haptics.Play(HapticKind.Medium);
        }

        private void ShowWinter()
        {
            _winter.SetActive(true);
            _winter.transform.SetAsLastSibling();
            _audio.Play(SfxId.WinterChime);
            RefreshWinter();
        }

        private void RefreshWinter()
        {
            _winterCoins.text = _sim.Coins.ToString("0") + " coins";
            foreach (var (button, upgrade, label) in _shop)
            {
                double cost = _sim.CostOf(upgrade);
                label.text = double.IsInfinity(cost) ? "max" : cost.ToString("0");
                button.interactable = !double.IsInfinity(cost) && _sim.Coins >= cost;
            }
        }

        private int Index(DigTile t) => t.Y * _sim.Config.GridSize + t.X;

        private void SpawnNumber(Vector2 at, string text, Color color, float scale)
        {
            if (_numberPool.Count == 0) return;
            var num = _numberPool.Dequeue();
            num.text = text;
            num.color = color;
            num.rectTransform.localScale = Vector3.one * scale;
            num.rectTransform.anchoredPosition = at;
            num.gameObject.SetActive(true);
            num.transform.SetAsLastSibling();
            _numbers.Add((num, Time.time, at));
        }

        // ------------------------------------------------------------------ frame

        public int DebugFrames { get; private set; }

        private void Update()
        {
            DebugFrames++;
            float dt = Time.unscaledDeltaTime; // the prototype never pauses; unscaled keeps it honest whatever the editor left behind
            if (!_winter.activeSelf)
            {
                ReadInput();
                _sim.Tick(dt);
            }
            Refresh(dt);
        }

        private bool _debugPointerActive, _debugPointerDown;
        private Vector2 _debugPointerPos;

        /// <summary>A scripted finger for proto-shot.bat: the next frames read this instead of the device pointer.</summary>
        public void DebugPointer(bool down, Vector2 screenPos)
        {
            _debugPointerActive = true;
            _debugPointerDown = down;
            _debugPointerPos = screenPos;
        }

        public Vector2 DebugTileScreenPos(int index)
        {
            var world = _board.TransformPoint(TilePos(_sim.Tiles[index]));
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        public double DebugTileHp(int index) => _sim.Tiles[index].Hp;
        public System.Text.StringBuilder DebugLog { get; } = new System.Text.StringBuilder();
        public bool DebugScriptDone { get; private set; }

        /// <summary>
        /// The reproduction that runs at the game's own frame cadence: tap the centre tile through the real pointer path
        /// until it breaks, then tap the top-left tile twice and report whether its HP moved. Each tap is a press on one
        /// frame and a release on the next, with a few frames between taps for the cooldown.
        /// </summary>
        public System.Collections.IEnumerator DebugTapScript()
        {
            DebugScriptDone = false;
            int taps = 0;
            while (_sim.Tiles[4].State == TileState.Hard && taps < 40)
            {
                yield return TapFrames(4);
                taps++;
                DebugLog.AppendLine("centre tap " + taps + ": strikes=" + _sim.Strikes + " hp=" + _sim.Tiles[4].Hp.ToString("0.0") + " state=" + _sim.Tiles[4].State + " " + DebugInput);
            }
            DebugLog.AppendLine("centre " + (_sim.Tiles[4].State == TileState.Hard ? "NEVER BROKE" : "broke") + " after " + taps + " taps");
            double before = _sim.Tiles[0].Hp;
            for (int i = 0; i < 3; i++)
            {
                yield return TapFrames(0);
                DebugLog.AppendLine("top-left tap " + (i + 1) + ": strikes=" + _sim.Strikes + " hp=" + _sim.Tiles[0].Hp.ToString("0.0") + " " + DebugInput);
            }
            DebugLog.AppendLine((_sim.Tiles[0].Hp < before ? "OK" : "FAIL") + " top-left after the centre broke: hp " + before.ToString("0.0") + " -> " + _sim.Tiles[0].Hp.ToString("0.0"));
            DebugScriptDone = true;
        }

        private System.Collections.IEnumerator TapFrames(int index)
        {
            var at = DebugTileScreenPos(index);
            DebugPointer(true, at);
            yield return null;
            DebugPointer(false, at);
            yield return null;
            // let the cooldown pass whatever the frame rate: the sim ticks with unscaled time each Update
            float until = Time.unscaledTime + 0.45f;
            int frames = 0;
            while (Time.unscaledTime < until || frames < 2) { frames++; yield return null; }
            DebugLog.AppendLine("  (waited " + frames + " frames, dt " + Time.unscaledDeltaTime.ToString("0.00") + ")");
        }
        public int DebugStrikes => _sim.Strikes;
        public string DebugInput => "down=" + _wasDown + " drag=" + _dragging + " water=" + _watering + " downTile=" + (_downTile == null ? "none" : _downTile.X + "," + _downTile.Y) + " cooldown=" + _sim.StrikeCooldownLeft.ToString("0.00") + " tired=" + _sim.Tired;
        public TileState DebugTileState(int index) => _sim.Tiles[index].State;

        private void ReadInput()
        {
            bool down;
            Vector2 pos;
            if (_debugPointerActive)
            {
                down = _debugPointerDown;
                pos = _debugPointerPos;
            }
            else
            {
                var pointer = Pointer.current;
                if (pointer == null) return;
                down = pointer.press.isPressed;
                pos = pointer.position.ReadValue();
            }
            HandlePointer(down, pos);
        }

        private void HandlePointer(bool down, Vector2 pos)
        {
            var tile = TileUnder(pos);
            if (down && !_wasDown)
            {
                _downTime = Time.unscaledTime;
                _downPos = pos;
                _downTile = tile;
                _dragging = false;
                _watering = false;
                _path.Clear();
                if (tile != null) _path.Add(tile);
            }
            if (down)
            {
                if (!_dragging && (pos - _downPos).magnitude > DragPixels) { _dragging = true; StopWatering(); }
                if (_dragging && tile != null && !_path.Contains(tile)) _path.Add(tile);
                if (!_dragging && !_watering && _downTile != null && _downTile.State == TileState.Growing && Time.unscaledTime - _downTime >= HoldSeconds)
                {
                    _watering = true;
                    _sim.SetWatering(_downTile, true);
                    _audio.Play(SfxId.WaterSplash);
                }
            }
            if (!down && _wasDown)
            {
                if (_watering) StopWatering();
                else if (_dragging) _sim.Reap(_path);
                else if (_downTile != null) Tap(_downTile);
            }
            _wasDown = down;
        }

        private void StopWatering()
        {
            if (!_watering) return;
            _watering = false;
            if (_downTile != null) _sim.SetWatering(_downTile, false);
        }

        private void Tap(DigTile t)
        {
            _selected = t;
            if (t.Crow) { _sim.TapCrow(t); _audio.Play(SfxId.CrowScared); return; }
            switch (t.State)
            {
                case TileState.Hard:
                    if (!_sim.Strike(t)) _audio.Play(SfxId.Denied, 0.5f);
                    break;
                case TileState.Ripe:
                    _sim.Reap(new[] { t });
                    break;
            }
        }

        private DigTile TileUnder(Vector2 screen)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_board, screen, null, out var local)) return null;
            float step = TileSize + TileGap;
            int x = Mathf.RoundToInt(local.x / step) + 1;
            int y = 1 - Mathf.RoundToInt(local.y / step);
            if (x < 0 || x > 2 || y < 0 || y > 2) return null;
            var t = _sim.Tiles[y * 3 + x];
            var centre = TilePos(t);
            return Mathf.Abs(local.x - centre.x) <= TileSize * 0.5f && Mathf.Abs(local.y - centre.y) <= TileSize * 0.5f ? t : null;
        }

        private void Refresh(float dt)
        {
            var sim = _sim;
            _coins.text = sim.Coins.ToString("0");
            _year.text = "Year " + sim.Year + "  ·  " + sim.Season + "  ·  " + Mathf.CeilToInt(sim.Config.YearLength - sim.YearTime) + " s to frost";
            _staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(sim.Stamina / sim.StaminaMax), 1f);
            _stats.text = "strikes " + sim.Strikes + "  crits " + sim.Crits + "  tired " + sim.TiredStrikes + "  breaks " + sim.Breaks + "  reaped " + sim.Reaped
                + "  dmg " + sim.Damage.ToString("0") + "  window " + sim.CritWindow.ToString("0.00") + "  crit " + (sim.CritChance * 100).ToString("0") + "%";
            _staminaFill.color = sim.Tired ? UiPalette.Brick : UiPalette.Sage;
            _stamina.text = Mathf.FloorToInt(sim.Stamina) + " / " + Mathf.FloorToInt(sim.StaminaMax) + (sim.Tired ? "   TIRED: swings at 40%, no crits" : "");

            // The pulse: a ring closing on the selected tile once a second; on the beat it snaps tight and lights up.
            float phase = sim.Pulse;
            float tri = 1f - Mathf.Abs(phase - 0.5f) * 2f; // 0 at the edges of the second, 1 on the beat
            _pulse.rectTransform.anchoredPosition = TilePos(_selected);
            _pulseCore.rectTransform.anchoredPosition = TilePos(_selected);
            _pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1.06f, tri * tri);
            bool onBeat = sim.OnBeat && _selected.State == TileState.Hard;
            _pulse.color = onBeat ? UiPalette.WithAlpha(UiPalette.Cream, 0.95f) : UiPalette.WithAlpha(UiPalette.Honey, 0.35f + 0.4f * tri);
            _pulseCore.color = UiPalette.WithAlpha(UiPalette.Cream, onBeat ? 0.9f : 0f);
            _pulse.gameObject.SetActive(_selected.State == TileState.Hard);
            _pulseCore.gameObject.SetActive(_selected.State == TileState.Hard);
            // The beat is the field's, not the tile's: the whole board breathes with it, so the timing reads anywhere.
            _board.localScale = Vector3.one * (1f + 0.02f * tri * tri);

            for (int i = 0; i < sim.Tiles.Length; i++)
            {
                var t = sim.Tiles[i];
                Color bg;
                string label;
                switch (t.State)
                {
                    case TileState.Hard:
                        bg = t.Golden ? UiPalette.Honey : t.Chest ? UiPalette.Plum : GroundColor(t.Ground);
                        label = (t.Golden ? "GOLDEN\n" : t.Chest ? "CHEST\n" : "") + t.Ground.ToString().ToUpperInvariant() + "\n" + t.Hp.ToString("0") + " / " + t.MaxHp.ToString("0");
                        break;
                    case TileState.Growing:
                        bg = Color.Lerp(UiPalette.Earth, UiPalette.Sage, 0.35f + 0.5f * t.Growth);
                        label = CropName(t) + "\n" + (t.Watering ? "watering" : "growing") + " " + (t.Growth * 100f).ToString("0") + "%";
                        break;
                    default:
                        bg = Color.Lerp(UiPalette.Sage, UiPalette.Honey, 0.6f + 0.4f * Mathf.Sin(Time.time * 5f + i));
                        label = CropName(t) + "\nRIPE  " + sim.CropValue(t).ToString("0");
                        break;
                }
                _tileBg[i].color = bg;
                _tileLabel[i].text = label;
                _layerLabel[i].text = "L" + t.Layer;
                _hpFill[i].gameObject.SetActive(t.State == TileState.Hard);
                _hpFill[i].rectTransform.anchorMax = new Vector2(t.MaxHp > 0 ? Mathf.Clamp01((float)(t.Hp / t.MaxHp)) : 0f, 1f);
                _growFill[i].gameObject.SetActive(t.State != TileState.Hard);
                _growFill[i].rectTransform.anchorMax = new Vector2(t.State == TileState.Ripe ? 1f : Mathf.Clamp01(t.Growth), 1f);
                _crowMark[i].gameObject.SetActive(t.Crow);
                // A struck tile jolts; a crit jolts harder. The tile, not the camera (rule: shake is for gold and retire).
                _shake[i] = Mathf.Max(0f, _shake[i] - dt * 4f);
                float s = _shake[i];
                _tileRt[i].anchoredPosition = TilePos(t) + new Vector2(Mathf.Sin(Time.time * 60f) * 12f * s, Mathf.Cos(Time.time * 50f) * 8f * s);
                _tileRt[i].localScale = Vector3.one * (1f + 0.06f * s);
            }

            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var (text, born, from) = _numbers[i];
                float age = Time.time - born;
                if (age > 0.8f)
                {
                    text.gameObject.SetActive(false);
                    _numberPool.Enqueue(text);
                    _numbers.RemoveAt(i);
                    continue;
                }
                text.rectTransform.anchoredPosition = from + new Vector2(0f, 140f * age);
                var c = text.color;
                c.a = 1f - Mathf.Clamp01((age - 0.4f) / 0.4f);
                text.color = c;
            }
        }

        private string CropName(DigTile t) => _sim.CropOf(t).Key.Replace("crop.", "").Replace("_", " ");

        // ------------------------------------------------------------------ hooks for proto-shot.bat

        /// <summary>Strikes a tile as a tap would, ignoring the cooldown so a script can land several in one frame.</summary>
        public void DebugStrike(int index)
        {
            var t = _sim.Tiles[index];
            _selected = t;
            for (int i = 0; i < 400 && !_sim.CanStrike && t.State == TileState.Hard; i++) _sim.Tick(0.05f); // wait out the cooldown
            _sim.Strike(t);
        }

        public void DebugReapAll()
        {
            var ripe = new List<DigTile>();
            foreach (var t in _sim.Tiles) if (t.State == TileState.Ripe) ripe.Add(t);
            if (ripe.Count == 0) { foreach (var t in _sim.Tiles) if (t.State == TileState.Growing) { t.Growth = 1f; } _sim.Tick(0.05f); foreach (var t in _sim.Tiles) if (t.State == TileState.Ripe) ripe.Add(t); }
            _sim.Reap(ripe);
        }

        public void DebugEndYear()
        {
            while (!_sim.Winter) _sim.Tick(0.5f);
        }

        private static Color GroundColor(GroundType g)
        {
            switch (g)
            {
                case GroundType.Clay: return UiPalette.Earth;
                case GroundType.Stone: return new Color(0.5f, 0.5f, 0.52f);
                case GroundType.Roots: return new Color(0.42f, 0.3f, 0.2f);
                case GroundType.Gravel: return new Color(0.62f, 0.6f, 0.56f);
                default: return new Color(0.32f, 0.32f, 0.36f);
            }
        }
    }
}
#endif
