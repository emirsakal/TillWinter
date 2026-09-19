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
        private RectTransform _coinIcon;
        private Image _coinGlow, _seedChipFace, _seedGlow;
        private double _displayCoins;
        private int _coinTextLength = -1;
        // The "this year" card: numbers are written into char buffers so nothing is allocated when they change.
        private CanvasGroup _yearGroup;
        private TMP_Text _yearLine, _nextGenLabel;
        private RectTransform _nextGenFill;
        private readonly char[] _cardChars = new char[128];
        private readonly char[] _nextChars = new char[64];
        private readonly char[] _numChars = new char[32];
        private string _ySeg0 = "", _ySeg1 = "", _ySeg2 = "", _nSeg0 = "", _nSeg1 = "";
        private int _cardHarvests = -1, _cardPercent = -1;
        private double _cardCoins = -1;
        private static Sprite _glowSprite;
        private static Sprite GlowSprite => _glowSprite != null ? _glowSprite
            : (_glowSprite = Sprite.Create(Prims.RadialGradient(64, 0f, 1f), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f));
        private RectTransform _bar, _elapsed, _frostSpan;
        private Image _elapsedImage;
        private RectTransform _fxLayer;

        private readonly List<Coin> _coins = new List<Coin>(96);
        private readonly char[] _coinChars = new char[32];
        private RichNumber _coinRich;
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
            // Icon and number are centred as one unit: the number's centre stays put and the icon rides its left
            // edge, so a short "0" no longer sat far from its coin.
            _coinGlow = UiKit.CircleImage(_coinGroup, "Glow", _theme.Coin, Vector2.zero, 170f);
            _coinGlow.sprite = GlowSprite;
            var icon = UiKit.CircleImage(_coinGroup, "Icon", _theme.Coin, Vector2.zero, 76f);
            _coinIcon = icon.rectTransform;
            UiKit.CircleImage(icon.transform, "Inner", _theme.CoinInner, Vector2.zero, 48f);
            _coinText = UiKit.Label(_coinGroup, "Value", "0", (int)_theme.CoinFontSize, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_coinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 0f), new Vector2(50f, 0f));
            UiKit.Outline(_coinText);
            _coinText.richText = true; // the K/M suffix is set smaller, in the coin colour
            _coinRich = new RichNumber(0.64f, _theme.Coin);
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
            var chip = UiKit.Panel(_safe, "SeedChip", UiKit.LipColor(_theme.SeedChip), true, false);
            _seedChipRt = chip.rectTransform;
            UiKit.Box(_seedChipRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 54f), new Vector2(300f, 88f));
            _seedChipFace = UiKit.Panel(_seedChipRt, "Face", _theme.SeedChip, true, false);
            UiKit.Stretch(_seedChipFace.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 7f), Vector2.zero);
            _seedGlow = UiKit.CircleImage(_seedChipFace.transform, "Glow", _theme.Seed, new Vector2(-104f, 0f), 96f);
            _seedGlow.sprite = GlowSprite;
            UiKit.CircleImage(_seedChipFace.transform, "Seed", _theme.Seed, new Vector2(-104f, 0f), 44f);
            _rate = UiKit.Label(top, "Rate", "", UiType.Caption, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_rate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 290f), new Vector2(500f, 40f)); // under the Year line: beside the counter a long number ran into it
            UiKit.Outline(_rate, 0.12f);
            _seedChip = UiKit.Label(_seedChipFace.transform, "Text", "", UiType.Label, _theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_seedChip.rectTransform, Vector2.zero, Vector2.one, new Vector2(78f, 0f), new Vector2(-10f, 0f));
            _seedChipRt.gameObject.SetActive(false);

            // The bottom of the screen: what this year has brought, and how far the farm is from the next generation.
            var yearCard = UiKit.Card(_safe, "YearCard", _theme.YearCard, false);
            UiKit.Box(yearCard.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(620f, 136f));
            _yearGroup = yearCard.gameObject.AddComponent<CanvasGroup>();
            _yearGroup.blocksRaycasts = false;
            _yearLine = UiKit.Label(yearCard.transform, "Line", "", UiType.Label, _theme.YearCardText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_yearLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(580f, 44f));
            _nextGenLabel = UiKit.Label(yearCard.transform, "NextGen", "", UiType.Caption, _theme.YearCardMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_nextGenLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(580f, 32f));
            var track = UiKit.Panel(yearCard.transform, "NextGenTrack", _theme.BarBackground, true, false);
            UiKit.Box(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(540f, 16f));
            _nextGenFill = UiKit.Panel(track.transform, "Fill", _theme.Seed, true, false).rectTransform;
            UiKit.Stretch(_nextGenFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            SplitTemplate(Strings.Get("ui.year_summary"), "{harvests}", "{coins}", out _ySeg0, out _ySeg1, out _ySeg2);
            SplitTemplate(Strings.Get("hud.next_generation"), "{percent}", null, out _nSeg0, out _nSeg1, out _);

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
                _theme.SheetIdle, _theme.SheetButtonText, OpenEndYearConfirm);
            UiKit.Box(endYear.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, _theme.SeasonNameYInBand - 20f), new Vector2(220f, 64f)); // below the bar, not touching its end

            // A long streak pays out: the milestone says so over the ring.
            _milestone = UiKit.Label(_safe, "ComboMilestone", "", UiType.Heading, _theme.Combo, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_milestone.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 80f));
            UiKit.OutlineStrong(_milestone);
            _milestone.alpha = 0f;

            // The ring's shape, once `ring_shape` is bought: round, rake, cross.
            _shapeButton = UiKit.Button(_safe, "RingShape", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, CycleRingShape);
            UiKit.Box(_shapeButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 180f), new Vector2(130f, 110f));
            _shapeIcon = UiKit.ButtonIcon(_shapeButton, ShapeIcon(RingShape.Round));
            _shapeButton.gameObject.SetActive(false);

            BuildSeedBag();

            BuildEndYearConfirm(canvas);

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
            _game.Sim.ComboMilestone += OnComboMilestone;
            _game.Sim.CrowScared += OnCrowScared;
            _game.Sim.YearStarted += OnYearStarted;
            _game.Sim.WinterStarted += OnWinter;
            _game.Sim.FrostWarningStarted += OnFrostWarning;
        }

        private void OnFrostWarning() => Haptics.Play(HapticKind.Light);

        /// <summary>Splits a localized template around its placeholders once, so the parts can be written into a char buffer.</summary>
        private static void SplitTemplate(string template, string first, string second, out string s0, out string s1, out string s2)
        {
            s1 = s2 = "";
            int a = template.IndexOf(first, System.StringComparison.Ordinal);
            if (a < 0) { s0 = template; return; }
            s0 = template.Substring(0, a);
            string rest = template.Substring(a + first.Length);
            int b = second == null ? -1 : rest.IndexOf(second, System.StringComparison.Ordinal);
            if (b < 0) { s1 = rest; return; }
            s1 = rest.Substring(0, b);
            s2 = rest.Substring(b + second.Length);
        }

        private static int Put(char[] buf, int at, string s)
        {
            for (int i = 0; i < s.Length && at < buf.Length; i++) buf[at++] = s[i];
            return at;
        }

        private int PutNumber(char[] buf, int at, double value)
        {
            int n = NumberFormat.Short(value, _numChars);
            for (int i = 0; i < n && at < buf.Length; i++) buf[at++] = _numChars[i];
            return at;
        }

        private static int PutInt(char[] buf, int at, int value)
        {
            if (value < 0) value = 0;
            int start = at;
            do { if (at < buf.Length) buf[at++] = (char)('0' + value % 10); value /= 10; } while (value > 0);
            System.Array.Reverse(buf, start, at - start);
            return at;
        }

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
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta = new Vector2(34f, 34f); // big enough to read at a glance on a phone
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.Harvested -= OnHarvested;
            _game.Sim.ComboMilestone -= OnComboMilestone;
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

        private GameObject _endYearConfirm;
        private TMP_Text _milestone;
        private float _milestoneLeft;
        private Button _shapeButton;
        private Image _shapeIcon;

        private static string ShapeIcon(RingShape shape) =>
            shape == RingShape.Rake ? "barsVertical" : shape == RingShape.Cross ? "plus" : "target";

        /// <summary>Cycles through the shapes the player has unlocked (GDD §2.1 v1.6).</summary>
        private void CycleRingShape()
        {
            int level = _game.State.Stats.RingShapeLevel;
            if (level <= 0) return;
            int count = level >= 2 ? 3 : 2;
            var next = (RingShape)(((int)_game.State.RingShape + 1) % count);
            if (!_game.Sim.SetRingShape(next)) return;
            _shapeIcon.sprite = NodeIcons.Get(ShapeIcon(next)) ?? _shapeIcon.sprite;
            Haptics.Play(HapticKind.Selection);
        }

        // ------------------------------------------------------------------ seed bag (GDD §2.3 v1.7)

        private Button _bagButton;
        private RectTransform _bagRow;
        private TMP_Text _bagHint;
        private Button[] _seedChips;
        private TMP_Text[] _seedChipText;
        /// <summary>The seed in hand: -1 = the bed's best, 0.. = a crop; <see cref="NoSeed"/> while nothing is picked.</summary>
        private int _bagTier = NoSeed;
        private const int NoSeed = int.MinValue;
        private int _bagShownTiers = -1;
        private Season _bagSeason = (Season)(-1);

        private void BuildSeedBag()
        {
            _bagButton = UiKit.Button(_safe, "SeedBag", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, ToggleSeedBag);
            UiKit.Box(_bagButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 180f), new Vector2(130f, 110f));
            UiKit.ButtonIcon(_bagButton, "basket");
            _bagButton.gameObject.SetActive(false);

            _bagRow = UiKit.Rect("SeedRow", _safe);
            UiKit.Box(_bagRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 350f), new Vector2(1000f, 170f));
            _bagHint = UiKit.Label(_bagRow, "Hint", Strings.Get("ui.seed_hint"), UiType.Caption, _theme.Text, TextAnchor.MiddleLeft);
            UiKit.Box(_bagHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(990f, 44f));
            UiKit.Outline(_bagHint, 0.14f);

            int count = _game.Sim.Config.Crops.Length + 1; // "best" first, then every crop
            _seedChips = new Button[count];
            _seedChipText = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                int tier = i - 1;
                var chip = UiKit.Button(_bagRow, "Seed" + i, "", UiType.Caption, _theme.SheetIdle, _theme.SheetButtonText, () => PickSeed(tier));
                UiKit.Box(chip.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * 141f, 0f), new Vector2(133f, 116f));
                var label = UiKit.ButtonLabel(chip);
                label.richText = true;
                label.enableWordWrapping = true; // crop name over its season
                label.lineSpacing = -12f;
                _seedChips[i] = chip;
                _seedChipText[i] = label;
            }
            _bagRow.gameObject.SetActive(false);
        }

        private void ToggleSeedBag()
        {
            bool open = !_bagRow.gameObject.activeSelf;
            _bagRow.gameObject.SetActive(open);
            _bagTier = NoSeed;
            _game.PlotTapOverride = null;
            if (open) RefreshSeedChips(true);
            PaintSeedChips();
            Haptics.Play(HapticKind.Selection);
        }

        private void CloseSeedBag()
        {
            if (_bagRow.gameObject.activeSelf) _bagRow.gameObject.SetActive(false);
            _bagTier = NoSeed;
            _game.PlotTapOverride = null;
        }

        private void PickSeed(int tier)
        {
            _bagTier = _bagTier == tier ? NoSeed : tier; // a second tap puts the seed back
            _game.PlotTapOverride = _bagTier == NoSeed ? null : (System.Func<GridPos, bool>)PlantAt;
            PaintSeedChips();
        }

        private bool PlantAt(GridPos pos)
        {
            if (_bagTier == NoSeed) return false;
            if (_game.Sim.SetPlotCrop(pos, _bagTier))
            {
                Haptics.Play(HapticKind.Light);
                _audio?.Play(SfxId.Sprout, 0.8f);
                return true;
            }
            _milestone.text = Strings.Get("ui.seed_bed_low");
            _milestoneLeft = 1.6f;
            _audio?.Play(SfxId.Denied, 0.8f);
            return false;
        }

        /// <summary>Shows one chip per unlocked crop, each with the season it likes; the liked season in play stands out.</summary>
        private void RefreshSeedChips(bool force)
        {
            var state = _game.State;
            int tiers = state.Stats.MaxTierUnlocked;
            if (!force && tiers == _bagShownTiers && state.Season == _bagSeason) return;
            _bagShownTiers = tiers;
            _bagSeason = state.Season;
            var crops = _game.Sim.Config.Crops;
            for (int i = 0; i < _seedChips.Length; i++)
            {
                int tier = i - 1;
                bool show = tier <= tiers;
                _seedChips[i].gameObject.SetActive(show);
                if (!show) continue;
                if (tier < 0)
                {
                    _seedChipText[i].text = Strings.Get("ui.seed_auto");
                    continue;
                }
                var likes = crops[tier].Likes;
                bool now = _game.Sim.InSeason(tier);
                string hex = ColorUtility.ToHtmlStringRGB(now ? _theme.Gold : _theme.SheetButtonText);
                string season = Strings.Get("season." + likes);
                _seedChipText[i].text = Strings.Get(crops[tier].Key) + "\n<size=72%><color=#" + hex + ">" + (now ? "<b>" + season + "</b>" : season) + "</color></size>";
            }
        }

        private void PaintSeedChips()
        {
            for (int i = 0; i < _seedChips.Length; i++)
                _seedChips[i].targetGraphic.color = i - 1 == _bagTier ? _theme.SheetButton : _theme.SheetIdle;
        }

        private void OnComboMilestone(int combo, double coins)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            _milestone.text = Strings.Format("ui.combo_milestone", ("combo", combo), ("coins", NumberFormat.Short(coins)));
            _milestoneLeft = 1.6f;
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.GoldenHarvest, 0.7f);
        }

        /// <summary>Ending the year early throws away the standing crop, so it asks first (the sim pauses meanwhile).</summary>
        private void BuildEndYearConfirm(RectTransform canvas)
        {
            var overlay = UiKit.Panel(canvas, "EndYearConfirm", _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _endYearConfirm = overlay.gameObject;
            var page = UiKit.Card(overlay.transform, "Page", _theme.SheetPaper);
            UiKit.Box(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
            UiKit.SheetDecor(page, 128f);
            var title = UiKit.Label(page.transform, "Title", Strings.Get("ui.end_year_title"), UiType.Title, _theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(820f, 90f));
            var body = UiKit.Label(page.transform, "Body", Strings.Get("ui.end_year_body"), UiType.Body, _theme.SheetMuted, TextAnchor.MiddleCenter);
            UiKit.Box(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(780f, 180f));
            var no = UiKit.Button(page.transform, "EndYearNo", Strings.Get("ui.cancel"), UiType.Body, _theme.SheetIdle, _theme.SheetButtonText, () => CloseEndYearConfirm(false));
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 50f), new Vector2(360f, 104f));
            var yes = UiKit.Button(page.transform, "EndYearYes", Strings.Get("ui.end_year_yes"), UiType.Body, _theme.SheetDanger, _theme.SheetButtonText, () => CloseEndYearConfirm(true));
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 50f), new Vector2(360f, 104f));
            overlay.gameObject.AddComponent<SheetTransition>().Page = page.rectTransform;
            _endYearConfirm.SetActive(false);
        }

        private void OpenEndYearConfirm()
        {
            if (_game.State.Phase != Phase.Year || _game.Paused) return;
            _game.SetPaused(true);
            _endYearConfirm.transform.SetAsLastSibling();
            _endYearConfirm.SetActive(true);
        }

        private void CloseEndYearConfirm(bool endYear)
        {
            _endYearConfirm.SetActive(false);
            _game.SetPaused(false);
            if (endYear) _game.Sim.EndYearNow();
        }

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
            Vector2 to = _canvas.InverseTransformPoint(_coinIcon.position); // the icon moves with the number now
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
            // The counter counts up to its value instead of jumping; spending snaps straight down.
            if (shown < _displayCoins || shown - _displayCoins < 0.5) _displayCoins = shown;
            else _displayCoins += (shown - _displayCoins) * (1.0 - System.Math.Exp(-14.0 * dt));
            double whole = System.Math.Floor(_displayCoins);
            if (whole != _coinTextValue)
            {
                _coinTextValue = whole;
                int len = NumberFormat.Short(whole, _coinChars);
                int richLen = _coinRich.Write(_coinChars, len);
                _coinText.SetText(_coinRich.Buffer, 0, richLen);
                if (len != _coinTextLength)
                {
                    // The icon sits against the number's left edge; only a change of length can move that edge much.
                    _coinTextLength = len;
                    _coinIcon.anchoredPosition = new Vector2(-_coinText.preferredWidth * 0.5f - 12f, 0f);
                    _coinGlow.rectTransform.anchoredPosition = _coinIcon.anchoredPosition;
                }
            }
            _counterPunch = Mathf.Max(0f, _counterPunch - dt * 5f);
            _coinGroup.localScale = Vector3.one * (1f + 0.22f * Prims.EaseOutQuad(_counterPunch));
            var glow = _theme.Coin;
            glow.a = 0.22f + 0.1f * Mathf.Sin(Time.unscaledTime * 1.6f) + 0.4f * _counterPunch;
            _coinGlow.color = glow;
            // Landing coins flash the number warm for a moment.
            _coinText.color = Color.Lerp(_theme.Text, _theme.Coin, _counterPunch * 0.85f);

            // Year card: follows the HUD in and out, and rewrites its text only when a number on it changes.
            _yearGroup.alpha = _topGroup.alpha;
            int harvests = state.HarvestsThisYear;
            double yearCoins = state.CoinsThisYear;
            if (harvests != _cardHarvests || yearCoins != _cardCoins)
            {
                _cardHarvests = harvests;
                _cardCoins = yearCoins;
                int n = Put(_cardChars, 0, _ySeg0);
                n = PutNumber(_cardChars, n, harvests);
                n = Put(_cardChars, n, _ySeg1);
                n = PutNumber(_cardChars, n, yearCoins);
                n = Put(_cardChars, n, _ySeg2);
                _yearLine.SetText(_cardChars, 0, n);
            }
            double threshold = _game.Sim.Config.HeritageThreshold;
            float nextGen = threshold > 0 ? Mathf.Clamp01((float)(state.Generation.LifetimeCoinsThisGeneration / threshold)) : 0f;
            _nextGenFill.anchorMax = new Vector2(Prims.Damp(_nextGenFill.anchorMax.x, nextGen, 6f, dt), 1f);
            int percent = Mathf.FloorToInt(nextGen * 100f);
            if (percent != _cardPercent)
            {
                _cardPercent = percent;
                int n = Put(_nextChars, 0, _nSeg0);
                n = PutInt(_nextChars, n, percent);
                n = Put(_nextChars, n, _nSeg1);
                _nextGenLabel.SetText(_nextChars, 0, n);
            }
            MCoinText.End();

            if (state.Year != _subYear || state.Generation.Generation != _subGen) { _subYear = state.Year; _subGen = state.Generation.Generation; _subText.SetText(_yearGenFormat, state.Year, state.Generation.Generation); }
            // Milestone banner fades; the shape button appears with its node.
            if (_milestoneLeft > 0f)
            {
                _milestoneLeft -= dt;
                float k = Mathf.Clamp01(_milestoneLeft / 1.6f);
                _milestone.alpha = Mathf.Min(1f, k * 3f);
                _milestone.rectTransform.anchoredPosition = new Vector2(0f, 120f + (1f - k) * 60f);
            }
            else if (_milestone.alpha > 0f) _milestone.alpha = 0f;
            bool shapes = state.Stats.RingShapeLevel > 0 && !state.IsWinter;
            if (_shapeButton.gameObject.activeSelf != shapes) _shapeButton.gameObject.SetActive(shapes);
            // The seed bag: once a second crop is unlocked, during the year, never on the Golden Year's field.
            bool bag = state.Stats.MaxTierUnlocked > 0 && !state.IsWinter && !state.GoldenYearActive;
            if (_bagButton.gameObject.activeSelf != bag)
            {
                _bagButton.gameObject.SetActive(bag);
                if (!bag) CloseSeedBag();
            }
            if (_bagRow.gameObject.activeSelf) RefreshSeedChips(false);

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
            float heat = Mathf.Clamp01((state.Combo - 2) / 12f); // a long streak should look like one
            _comboRt.localScale = Vector3.one * Mathf.Max(0.0001f, Prims.Damp(_comboRt.localScale.x, _comboFade > 0f ? 1f + 0.4f * heat : 0.6f, 10f, dt));
            var comboColor = state.Combo >= 2 ? Color.Lerp(_theme.Combo, _theme.ComboHot, heat) : _combo.color;
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
            _seedChipFace.color = chipColor;
            if (canRetire)
            {
                var sg = _theme.Seed;
                sg.a = 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.4f) + 0.35f * _chipPunch; // ready, and saying so
                _seedGlow.color = sg;
            }

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
