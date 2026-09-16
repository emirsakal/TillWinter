using System.Collections.Generic;
using Unity.Profiling;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Year-screen HUD (GDD §10.1): coins big and centred, year · generation, a four-segment season bar with
    /// the frost span, the season name fading in at boundaries, combo counter, a seed chip once CanRetire.
    /// Also runs the coin flight FX. Everything themed by <see cref="HudTheme"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private sealed class Coin
        {
            public RectTransform Rt;
            public Vector2 From, Ctrl, To;
            public float Delay, T, Duration;
            public double Value;
            public bool Punch, Rich, Golden;
        }

        private GameController _game;
        private AudioManager _audio;
        private HudTheme _theme;
        private RectTransform _canvas, _safe;
        private CanvasGroup _topGroup;
        private RectTransform _coinGroup;
        private TMP_Text _coinText, _subText, _seasonName, _combo, _seedChip;
        private RectTransform _seedChipRt;
        private RectTransform _bottomBand;
        private CanvasGroup _bottomGroup;
        private RectTransform _bar, _elapsed, _frostSpan;
        private Image _elapsedImage;
        private RectTransform _fxLayer;

        private readonly List<Coin> _coins = new List<Coin>(96);
        private readonly char[] _coinChars = new char[32];
        /// <summary>Pre-warmed coin pool size and the cap on coins in flight (a burst never creates UI objects).</summary>
        private const int CoinPoolWarm = 96;
        private readonly Stack<RectTransform> _pool = new Stack<RectTransform>();
        private double _pending;
        /// <summary>Coins earned offline that the away card has not released yet (kept out of the counter).</summary>
        public double HeldCoins;
        private float _counterPunch;
        private float _lastFrostSecond = -1f;
        private Season _shownSeason = Season.Winter;
        private float _seasonFade;
        private Image _flash, _frostEdge;
        private float _flashUntil, _flashAlpha, _frostAlpha;
        private RectTransform _comboRt;
        private float _comboFade;
        private int _lastCombo;
        public static HudView Instance { get; private set; }
        public bool Flashing => Time.unscaledTime < _flashUntil;
        // Localized once at boot (a language change reloads the scene); TMP SetText formats without allocating.
        private string _yearGenFormat = "{0} {1}", _retireFormat = "{0}";
        private readonly string[] _seasonNames = new string[4];
        private static readonly ProfilerMarker MCoinFlight = new ProfilerMarker("Hud.CoinFlight");
        private static readonly ProfilerMarker MCoinText = new ProfilerMarker("Hud.CoinText");
        private static readonly ProfilerMarker MCombo = new ProfilerMarker("Hud.Combo");
        private static readonly ProfilerMarker MFlashFrost = new ProfilerMarker("Hud.FlashFrost");
        private static readonly ProfilerMarker MSeedsBar = new ProfilerMarker("Hud.SeedsBar");
        private double _coinTextValue = -1;
        private int _subYear = -1, _subGen = -1, _chipSeeds = -1;
        private TMP_Text _rate;
        private double _rateCoins = -1;
        private float _rateAt;
        private bool _chipShown;
        private float _chipPunch;
        private readonly Stack<Coin> _coinObjPool = new Stack<Coin>();

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            Instance = this;
            _game = game;
            _audio = audio;
            _canvas = canvas;
            _theme = HudTheme.Load();
            _yearGenFormat = Strings.Get("hud.year_gen");
            _retireFormat = Strings.Get("hud.retire_chip");
            for (int i = 0; i < _seasonNames.Length; i++) _seasonNames[i] = Strings.Get("season." + (Season)i);

            _safe = UiKit.Rect("HudSafe", canvas);
            SafeArea.Apply(_safe);
            var top = UiKit.Rect("TopBand", _safe);
            UiKit.Stretch(top, new Vector2(0f, 0.78f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _topGroup = top.gameObject.AddComponent<CanvasGroup>();
            if (top.GetComponent<CanvasRenderer>() == null) top.gameObject.AddComponent<CanvasRenderer>();
            top.gameObject.AddComponent<RaycastBlocker>(); // a finger resting on the HUD never becomes ring input

            _coinGroup = UiKit.Rect("Coins", top);
            UiKit.Box(_coinGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 90f), new Vector2(700f, 130f));
            var icon = UiKit.CircleImage(_coinGroup, "Icon", _theme.Coin, new Vector2(-190f, 0f), 76f);
            UiKit.CircleImage(icon.transform, "Inner", _theme.CoinInner, Vector2.zero, 48f);
            _coinText = UiKit.Label(_coinGroup, "Value", "0", (int)_theme.CoinFontSize, _theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_coinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(240f, 0f), Vector2.zero);
            UiKit.Outline(_coinText);
            // Size TMP's buffers for the longest counter once, so growing numbers never resize them mid-play.
            _coinText.SetText("-999.9Qi");
            _coinText.ForceMeshUpdate(true);
            _coinText.SetText("0");

            _subText = UiKit.Label(top, "Sub", "", (int)_theme.SubFontSize, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_subText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 236f), new Vector2(800f, 50f)); // below the coin row: it used to start 30 units inside it
            UiKit.Outline(_subText, 0.12f);

            _combo = UiKit.Label(top, "Combo", "", UiType.Heading, _theme.Combo, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_combo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(260f, -_theme.TopPadding - 90f), new Vector2(300f, 60f));
            UiKit.Outline(_combo);

            // The bottom of a tall phone was empty while the top carried everything: the retire chip moves down,
            // into thumb reach, and hangs off the safe area rather than the fading top band.
            var chip = UiKit.Panel(_safe, "SeedChip", _theme.SeedChip, true, false);
            _seedChipRt = chip.rectTransform;
            UiKit.Box(_seedChipRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 54f), new Vector2(280f, 76f));
            UiKit.CircleImage(_seedChipRt, "Seed", _theme.Seed, new Vector2(-104f, 0f), 34f);
            _rate = UiKit.Label(top, "Rate", "", UiType.Caption, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_rate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 290f), new Vector2(500f, 40f)); // under the Year line: beside the counter a long number ran into it
            UiKit.Outline(_rate, 0.12f);
            _seedChip = UiKit.Label(_seedChipRt, "Text", "", UiType.Label, _theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_seedChip.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 0f), new Vector2(-10f, 0f));
            _seedChipRt.gameObject.SetActive(false);

            // The year's progress belongs next to the farm it measures, so the bar and the season's name sit in their
            // own band under the island; the top keeps the coins. Its own CanvasGroup, because it no longer hangs off
            // the top band that fades outside the Year phase.
            _bottomBand = UiKit.Rect("SeasonBand", _safe);
            UiKit.Stretch(_bottomBand, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, _theme.SeasonBandY), new Vector2(0f, _theme.SeasonBandY + _theme.SeasonBandHeight));
            _bottomGroup = _bottomBand.gameObject.AddComponent<CanvasGroup>();

            BuildSeasonBar(_bottomBand);

            // No plate behind the name: a strong outline and a soft shadow carry it over any sky, in any season.
            _seasonName = UiKit.Label(_bottomBand, "SeasonName", "", UiType.Heading, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_seasonName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _theme.SeasonNameYInBand), new Vector2(520f, 50f));
            UiKit.OutlineStrong(_seasonName);

            // Ending the year early (GDD §3 v1.5): a field that is finished should not mean watching the clock. It
            // costs the standing crop exactly as frost would, so it sits out at the edge of the band rather than
            // anywhere a thumb rests during play.
            var endYear = UiKit.Button(_bottomBand, "EndYear", Strings.Get("ui.end_year"), UiType.Caption,
                _theme.SheetIdle, _theme.SheetButtonText, () => { _audio.Play(SfxId.UiClick); _game.Sim.EndYearNow(); });
            UiKit.Box(endYear.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, _theme.SeasonNameYInBand - 20f), new Vector2(220f, 64f)); // below the bar, not touching its end

            _fxLayer = UiKit.Rect("CoinFx", canvas);
            UiKit.Stretch(_fxLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _comboRt = _combo.rectTransform;
            _comboRt.SetParent(_fxLayer, false); // floats near the ring in canvas space, like the coins
            _comboRt.anchorMin = _comboRt.anchorMax = new Vector2(0.5f, 0.5f); // WorldToCanvas is centre-relative, like the coins
            _comboRt.pivot = new Vector2(0f, 0.5f);
            // Frost creeping in from the screen edges (UI overlay under the coin FX), and the golden-harvest flash.
            _frostEdge = UiKit.Panel(canvas, "FrostEdge", new Color(0.8f, 0.9f, 1f, 0f), false, false);
            _frostEdge.sprite = Prims.EdgeFadeSprite(256, 0.45f);
            _frostEdge.type = Image.Type.Simple;
            UiKit.Stretch(_frostEdge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _frostEdge.transform.SetSiblingIndex(_fxLayer.GetSiblingIndex());
            _flash = UiKit.Panel(canvas, "Flash", new Color(1f, 1f, 1f, 0f), false, false);
            UiKit.Stretch(_flash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Pre-warm the coin flight pool so a harvest burst never creates UI objects mid-play.
            for (int i = 0; i < CoinPoolWarm; i++)
            {
                var rt = GetCoin();
                rt.gameObject.SetActive(false);
                _pool.Push(rt);
                _coinObjPool.Push(new Coin());
            }
            _game.Sim.Harvested += OnHarvested;
            _game.Sim.CrowScared += OnCrowScared;
            _game.Sim.YearStarted += OnYearStarted;
            _game.Sim.WinterStarted += OnWinter;
            _game.Sim.FrostWarningStarted += OnFrostWarning;
        }

        private void OnFrostWarning() => Haptics.Play(HapticKind.Light);

        /// <summary>Brief white screen flash (golden harvest: 60 ms at 10 %).</summary>
        public void Flash(float alpha, float seconds)
        {
            if (!SettingsStore.MotionAllowed) return; // reduce motion
            _flashAlpha = alpha;
            _flashUntil = Time.unscaledTime + seconds;
        }

        private void BuildSeasonBar(RectTransform top)
        {
            _bar = UiKit.Rect("SeasonBar", top);
            UiKit.Box(_bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _theme.BarYInBand), new Vector2(_theme.BarWidth, _theme.BarHeight));
            var bg = UiKit.Panel(_bar, "Bg", _theme.BarBackground, true, false);
            UiKit.Stretch(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            float w = 0.3f;
            Segment("Spring", 0f, w, _theme.Spring, 0);
            Segment("Summer", w, 2f * w, _theme.Summer, 1);
            Segment("Autumn", 2f * w, 3f * w, _theme.Autumn, 2);
            Segment("Winter", 3f * w, 1f, _theme.Winter, 3);
            var frost = UiKit.Panel(_bar, "Frost", _theme.Frost, false, false);
            frost.sprite = Prims.HatchSprite(); // hatched, so the frost span reads without relying on colour alone
            frost.type = Image.Type.Tiled;
            _frostSpan = frost.rectTransform;
            UiKit.Stretch(_frostSpan, new Vector2(3f * w - 0.1f, 0f), new Vector2(3f * w, 1f), new Vector2(0f, -3f), new Vector2(0f, 3f));
            _elapsedImage = UiKit.Panel(_bar, "Elapsed", _theme.BarElapsed, true, false);
            _elapsed = _elapsedImage.rectTransform;
            UiKit.Stretch(_elapsed, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var marker = UiKit.Panel(_elapsed, "Marker", _theme.BarMarker, false, false);
            UiKit.Box(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, _theme.BarHeight + 14f));
            var knob = UiKit.CircleImage(_elapsed, "MarkerKnob", _theme.BarMarker, Vector2.zero, _theme.BarHeight + 16f);
            var knobRt = knob.rectTransform;
            knobRt.anchorMin = knobRt.anchorMax = new Vector2(1f, 0.5f);
            knobRt.anchoredPosition = Vector2.zero;
        }

        private void Segment(string name, float from, float to, Color color, int season)
        {
            var img = UiKit.Panel(_bar, name, color, false, false);
            UiKit.Stretch(img.rectTransform, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(from > 0f ? 2f : 0f, 0f), new Vector2(to < 1f ? -2f : 0f, 0f));
            // A glyph above each segment: the seasons must read without relying on their colour.
            var glyph = UiKit.Panel(_bar, name + "Glyph", _theme.Text, false, false);
            glyph.sprite = Prims.SeasonGlyphSprite(season);
            glyph.type = Image.Type.Simple;
            glyph.preserveAspect = true;
            var rt = glyph.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2((from + to) * 0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta = new Vector2(26f, 26f);
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.Harvested -= OnHarvested;
            _game.Sim.CrowScared -= OnCrowScared;
            _game.Sim.YearStarted -= OnYearStarted;
            _game.Sim.WinterStarted -= OnWinter;
        }

        private void OnYearStarted()
        {
            _lastFrostSecond = -1f;
        }

        private void OnWinter()
        {
            _audio.Duck(1f, -6f);
            _audio.Play(SfxId.WinterChime);
            foreach (var c in _coins)
            {
                c.Rt.gameObject.SetActive(false);
                _pool.Push(c.Rt);
                _coinObjPool.Push(c);
            }
            _coins.Clear();
            _pending = 0;
        }

        private void OnHarvested(HarvestEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            bool ring = e.Source == HarvestSource.Ring;
            // Ring harvests: 3–8 coins by value, counter punch on arrival. Helpers: fewer coins, tick only.
            int count = ring ? Mathf.Clamp(3 + (int)(e.Coins / 4.0), 3, 8) : Mathf.Clamp(2 + e.Tier / 2, 2, 4);
            if (e.WasGolden) count = 8;
            SpawnCoins(_game.PlotToWorld(e.Pos, 0.5f), e.Coins, count, ring, e.Tier >= 3 || e.WasGolden, e.WasGolden);
        }

        private void OnCrowScared(CrowEvent e)
        {
            if (e.Coins > 0) SpawnCoins(_game.PlotToWorld(e.Pos, 0.6f), e.Coins, 4, true, false, false);
        }

        /// <summary>Counter punch without coin flight (away card OK).</summary>
        public void Punch() => _counterPunch = 1f;

        private void SpawnCoins(Vector3 world, double coins, int count, bool punch, bool rich, bool golden) =>
            SpawnCoinsFrom(WorldToCanvas(world), coins, count, punch, rich, golden);

        /// <summary>Coins flying into the counter from a point in canvas space (the away card paying out).</summary>
        public void FlyCoins(Vector2 fromCanvas, double coins, int count) =>
            SpawnCoinsFrom(fromCanvas, coins, count, true, coins > 200, false);

        private void SpawnCoinsFrom(Vector2 from, double coins, int count, bool punch, bool rich, bool golden)
        {
            // Past the pool size the value lands on the counter without a flight (only in extreme bursts).
            count = Mathf.Min(count, CoinPoolWarm - _coins.Count);
            if (count <= 0) return;
            Vector2 to = _canvas.InverseTransformPoint(_coinGroup.TransformPoint(new Vector3(-190f, 0f, 0f)));
            double share = coins / count;
            for (int i = 0; i < count; i++)
            {
                var coin = _coinObjPool.Count > 0 ? _coinObjPool.Pop() : new Coin();
                coin.Rt = GetCoin();
                coin.From = from + Random.insideUnitCircle * 30f;
                coin.To = to;
                coin.Delay = i * 0.045f;
                coin.T = 0f;
                coin.Duration = Random.Range(0.5f, 0.65f);
                coin.Value = share;
                coin.Punch = punch;
                coin.Rich = rich;
                coin.Golden = golden;
                coin.Rt.GetComponent<Image>().color = golden ? _theme.Gold : _theme.Coin;
                var mid = (coin.From + coin.To) * 0.5f;
                coin.Ctrl = mid + new Vector2(Random.Range(-220f, 220f), Random.Range(120f, 320f));
                coin.Rt.anchoredPosition = coin.From;
                coin.Rt.localScale = Vector3.one * 0.6f;
                coin.Rt.gameObject.SetActive(true);
                _coins.Add(coin);
                _pending += share;
            }
        }

        private RectTransform GetCoin()
        {
            if (_pool.Count > 0) return _pool.Pop();
            var img = UiKit.CircleImage(_fxLayer, "Coin", _theme.Coin, Vector2.zero, 44f);
            UiKit.CircleImage(img.transform, "Inner", _theme.CoinInner, Vector2.zero, 26f);
            return img.rectTransform;
        }

        private Vector2 WorldToCanvas(Vector3 world)
        {
            Vector2 screen = _game.Cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            return local;
        }

        private void LateUpdate()
        {
            var state = _game.State;
            float dt = Time.unscaledDeltaTime;

            MCoinFlight.Begin();
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                c.T += dt;
                float t = (c.T - c.Delay) / c.Duration;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    _pending -= c.Value;
                    if (c.Punch) _counterPunch = 1f;
                    _audio.Play(c.Rich ? SfxId.CoinArriveRich : SfxId.CoinArrive, c.Punch ? 1f : 0.7f);
                    c.Rt.gameObject.SetActive(false);
                    _pool.Push(c.Rt);
                    _coinObjPool.Push(c);
                    _coins.RemoveAt(i);
                    continue;
                }
                float e = t * t * (3f - 2f * t);
                e = Mathf.Lerp(t, e * e, 0.5f);
                c.Rt.anchoredPosition = Bezier(c.From, c.Ctrl, c.To, e);
                c.Rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, t * 3f)) * Mathf.Lerp(1f, 0.8f, t);
            }
            if (_coins.Count == 0) _pending = 0;
            MCoinFlight.End();

            _topGroup.alpha = Prims.Damp(_topGroup.alpha, state.Phase == Phase.Year ? 1f : 0f, 8f, dt);
            _bottomGroup.alpha = _topGroup.alpha; // the season band leaves with the rest of the year HUD
            // A CanvasGroup at alpha 0 still takes taps: the End year button must be untouchable once the year is over.
            bool inYear = state.Phase == Phase.Year;
            if (_bottomGroup.blocksRaycasts != inYear) _bottomGroup.blocksRaycasts = _bottomGroup.interactable = inYear;
            MCoinText.Begin();
            double shown = System.Math.Max(0, state.Coins - _pending - HeldCoins);
            if (shown != _coinTextValue) { _coinTextValue = shown; _coinText.SetText(_coinChars, 0, NumberFormat.Short(shown, _coinChars)); }
            _counterPunch = Mathf.Max(0f, _counterPunch - dt * 5f);
            _coinGroup.localScale = Vector3.one * (1f + 0.22f * Prims.EaseOutQuad(_counterPunch));
            MCoinText.End();

            if (state.Year != _subYear || state.Generation.Generation != _subGen) { _subYear = state.Year; _subGen = state.Generation.Generation; _subText.SetText(_yearGenFormat, state.Year, state.Generation.Generation); }
            MCombo.Begin();
            // Combo floats near the ring; on a break it keeps the last value and fades out.
            if (state.Combo >= 2)
            {
                if (state.Combo != _lastCombo) _combo.SetText("×{0}", state.Combo);
                _comboFade = 1f;
                if (state.Combo != _lastCombo) _comboRt.localScale = Vector3.one * 1.35f;
            }
            else _comboFade = Mathf.Max(0f, _comboFade - dt / 0.4f);
            _lastCombo = state.Combo;
            var ring = _game.CurrentRing;
            if (ring.HasValue)
            {
                var sp = WorldToCanvas(_game.PlotToWorld(ring.Value.X, ring.Value.Y, 0.3f));
                _comboRt.anchoredPosition = sp + new Vector2(state.RingRadius * 90f + 40f, 90f);
            }
            _comboRt.localScale = Vector3.one * Mathf.Max(0.0001f, Prims.Damp(_comboRt.localScale.x, _comboFade > 0f ? 1f : 0.6f, 10f, dt));
            var comboColor = _theme.Combo;
            comboColor.a = _comboFade;
            _combo.color = comboColor;
            MCombo.End();

            MFlashFrost.Begin();
            // Golden flash and frost edges.
            float flashLeft = _flashUntil - Time.unscaledTime;
            var flashColor = _flash.color;
            flashColor.a = flashLeft > 0f ? _flashAlpha : Mathf.Max(0f, flashColor.a - dt * 4f);
            _flash.color = flashColor;
            float frostTarget = state.FrostWarning && state.Phase == Phase.Year
                ? Mathf.Clamp01(1f - state.SecondsUntilWinter / Mathf.Max(0.01f, state.Stats.FrostWarningSeconds))
                : state.IsWinter ? 0.35f : 0f;
            _frostAlpha = Prims.Damp(_frostAlpha, frostTarget, 2f, dt);
            var fc = _frostEdge.color;
            fc.a = _frostAlpha * 0.55f;
            _frostEdge.color = fc;
            MFlashFrost.End();
            MSeedsBar.Begin();
            bool canRetire = _game.Sim.CanRetire && state.Phase == Phase.Year; // outside the top band, so it hides itself in Winter
            if (_seedChipRt.gameObject.activeSelf != canRetire) _seedChipRt.gameObject.SetActive(canRetire);
            if (canRetire && !_chipShown)
            {
                // The chip used to appear with no ceremony; retiring is the biggest decision in the game.
                _chipShown = true;
                _chipPunch = 1f;
                Haptics.Play(HapticKind.Light);
            }
            else if (!canRetire) _chipShown = false;
            _chipPunch = Mathf.Max(0f, _chipPunch - dt * 2f);
            _seedChipRt.localScale = Vector3.one * (1f + 0.25f * Prims.EaseOutQuad(_chipPunch));
            var chipColor = _theme.SeedChip;
            chipColor.a = Mathf.Lerp(_theme.SeedChip.a, 1f, _chipPunch);
            _seedChipRt.GetComponent<Image>().color = chipColor;

            // Earning rate: coins per second over the last second, shown while the farm is actually earning.
            double lifetime = state.Generation.LifetimeCoinsThisGeneration;
            if (_rateCoins < 0) { _rateCoins = lifetime; _rateAt = Time.unscaledTime; }
            if (Time.unscaledTime - _rateAt >= 1f)
            {
                double perSecond = (lifetime - _rateCoins) / (Time.unscaledTime - _rateAt);
                _rateCoins = lifetime;
                _rateAt = Time.unscaledTime;
                _rate.text = perSecond >= 0.5 && state.Phase == Phase.Year ? "+" + NumberFormat.Short(perSecond) + "/s" : "";
            }
            if (canRetire && _game.Sim.SeedsIfRetiredNow != _chipSeeds) { _chipSeeds = _game.Sim.SeedsIfRetiredNow; _seedChip.SetText(_retireFormat, _chipSeeds); }

            float progress = state.YearLength > 0f ? Mathf.Clamp01(state.YearTime / state.YearLength) * 0.9f : 0f;
            if (state.Phase != Phase.Year) progress = 1f;
            _elapsed.anchorMax = new Vector2(progress, 1f);
            float frostFrac = state.YearLength > 0f ? state.Stats.FrostWarningSeconds / state.YearLength * 0.9f : 0.1f;
            _frostSpan.anchorMin = new Vector2(0.9f - frostFrac, 0f);
            if (state.Season != _shownSeason)
            {
                _shownSeason = state.Season;
                _seasonFade = 0f;
                _seasonName.text = _seasonNames[(int)state.Season];
                _seasonName.color = _theme.Text; // the bar already carries the season colour; a pastel name on a pastel sky vanished
            }
            _seasonFade += dt;
            float fade = _seasonFade < 0.4f ? _seasonFade / 0.4f : _seasonFade < 2.5f ? 1f : Mathf.Max(0.85f, 1f - (_seasonFade - 2.5f)); // it settles, it does not disappear
            var sc = _seasonName.color;
            sc.a = fade;
            _seasonName.color = sc;

            MSeedsBar.End();
            if (state.FrostWarning && state.Phase == Phase.Year)
            {
                float left = state.SecondsUntilWinter;
                float f = 1f - Mathf.Clamp01(left / state.Stats.FrostWarningSeconds);
                // Minimal heartbeat: a slow colour breath toward frost blue, no scaling (the old sharp scale pulse read as jitter).
                float beat = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.Lerp(2f, 4f, f));
                _elapsedImage.color = Color.Lerp(_theme.BarElapsed, _theme.Frost, beat * (0.4f + 0.6f * f));
                float sec = Mathf.Ceil(left);
                if (sec != _lastFrostSecond)
                {
                    _lastFrostSecond = sec;
                    _audio.Play(SfxId.FrostTick, Mathf.Lerp(0.5f, 1f, f));
                }
            }
            else
            {
                _bar.localScale = Vector3.one;
                _elapsedImage.color = _theme.BarElapsed;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }

    /// <summary>Applies Screen.safeArea to a stretched RectTransform on devices; full rect in the editor.</summary>
    public static class SafeArea
    {
        public static void Apply(RectTransform rt)
        {
            var min = Vector2.zero;
            var max = Vector2.one;
            if (Application.isMobilePlatform && Screen.width > 0 && Screen.height > 0)
            {
                var sa = Screen.safeArea;
                min = new Vector2(Mathf.Clamp01(sa.xMin / Screen.width), Mathf.Clamp01(sa.yMin / Screen.height));
                max = new Vector2(Mathf.Clamp01(sa.xMax / Screen.width), Mathf.Clamp01(sa.yMax / Screen.height));
            }
            UiKit.Stretch(rt, min, max, Vector2.zero, Vector2.zero);
        }
    }
}
