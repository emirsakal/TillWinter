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
        private RectTransform _seasonBand;
        private CanvasGroup _seasonGroup;
        private RectTransform _coinIcon;
        private Image _coinGlow, _seedChipFace, _seedGlow;
        private double _displayCoins;
        private const float CoinSuffixScale = 0.64f;
        /// <summary>Every corner button's size: the barn button used to be wider than the seed bag under it.</summary>
        private Vector2 SideButton => new Vector2(_theme.SideButtonWidth, _theme.SideButtonHeight);
        private const float CoinIconDiameter = 76f;
        /// <summary>The counter is centred this far right of the coin group's middle; the icon sits to its left.</summary>
        private const float CoinTextOffset = 50f;
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
            top.gameObject.AddComponent<RaycastBlocker>(); // a finger resting on the HUD never strikes the field

            _coinGroup = UiKit.Rect("Coins", top);
            UiKit.Box(_coinGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 90f), new Vector2(700f, 130f));
            // Icon and number are centred as one unit: the number's centre stays put and the icon rides its left
            // edge, so a short "0" no longer sat far from its coin.
            _coinGlow = UiKit.CircleImage(_coinGroup, "Glow", _theme.Coin, Vector2.zero, 170f);
            _coinGlow.sprite = GlowSprite;
            var icon = UiKit.CircleImage(_coinGroup, "Icon", _theme.Coin, Vector2.zero, CoinIconDiameter);
            _coinIcon = icon.rectTransform;
            UiKit.CircleImage(icon.transform, "Inner", _theme.CoinInner, Vector2.zero, 48f);
            _coinText = UiKit.Label(_coinGroup, "Value", "0", (int)_theme.CoinFontSize, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            // TMP grows its character arrays the first time a longer string arrives, an allocation in the middle of
            // play. The longest coin string (rich-text suffix included) is set once here instead.
            _coinText.SetText(new string('8', 96));
            _coinText.SetText("0");
            UiKit.Stretch(_coinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(CoinTextOffset, 0f), new Vector2(CoinTextOffset, 0f));
            UiKit.Outline(_coinText);
            _coinText.richText = true; // the K/M suffix is set smaller, in the coin colour
            _coinRich = new RichNumber(CoinSuffixScale, _theme.Coin);
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
            // Light text on a pale sky needs a plate behind it: the goal line sits on a soft dark pill sized to its text.
            _goalPlate = UiKit.Panel(top, "GoalPlate", _theme.BarBackground, true, false);
            _goalPlateRt = _goalPlate.rectTransform;
            UiKit.Box(_goalPlateRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 332f), new Vector2(960f, 52f));
            _goalPlate.gameObject.SetActive(false); // shown with the goal line
            _goalLine = UiKit.Label(top, "Goal", "", UiType.Caption, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_goalLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 336f), new Vector2(960f, 44f));
            UiKit.Outline(_goalLine, 0.14f);
            _goalLine.gameObject.SetActive(false);
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

            // v3.6: the year's clock sits at the top, under the coins and above the island. Under the island it shared
            // a crowded bottom with the stamina bar, and the two thin bars read as one. Its own CanvasGroup, faded with
            // the top band outside the Year phase.
            _seasonBand = UiKit.Rect("SeasonBand", _safe);
            float bandTop = _theme.TopPadding + _theme.SeasonBandTop;
            UiKit.Stretch(_seasonBand, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -bandTop - _theme.SeasonBandHeight), new Vector2(0f, -bandTop));
            _seasonGroup = _seasonBand.gameObject.AddComponent<CanvasGroup>();

            BuildSeasonBar(_seasonBand);

            // No plate behind the name: a strong outline and a soft shadow carry it over any sky, in any season.
            _seasonName = UiKit.Label(_seasonBand, "SeasonName", "", UiType.Body, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_seasonName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _theme.SeasonNameYInBand), new Vector2(520f, 50f));
            UiKit.OutlineStrong(_seasonName);

            // Ending the year early (GDD §3 v1.5): a field that is finished should not mean watching the clock. It
            // costs the standing crop exactly as frost would, so it sits out at the edge of the band rather than
            // anywhere a thumb rests during play.
            var endYear = UiKit.Button(_safe, "EndYear", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, OpenEndYearConfirm);
            UiKit.Box(endYear.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f - _theme.SideButtonWidth - 20f, 54f), SideButton);
            UiKit.ButtonCaption(endYear, UiIcons.Time, Strings.Get("ui.end_year")); // beside Inspect, the same shape as its neighbours

            // A long streak pays out: the milestone says so over the ring.
            _milestone = UiKit.Label(_safe, "ComboMilestone", "", UiType.Heading, _theme.Combo, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_milestone.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 80f));
            UiKit.OutlineStrong(_milestone);
            _milestone.alpha = 0f;

            BuildStamina();
            BuildSeedBag();
            BuildHelperButtons();
            BuildEventUi();
            BuildStoreButton();
            BuildInspect();
            BuildChecklist();
            _star.transform.SetAsLastSibling(); // the star crosses over the checklist card, never behind it

            BuildEndYearConfirm(canvas);

            _fxLayer = UiKit.Rect("CoinFx", canvas);
            UiKit.Stretch(_fxLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _comboRt = _combo.rectTransform;
            _comboRt.SetParent(_fxLayer, false); // floats near the ring in canvas space, like the coins
            _comboRt.anchorMin = _comboRt.anchorMax = new Vector2(0.5f, 0.5f); // WorldToCanvas is centre-relative, like the coins
            _comboRt.pivot = new Vector2(0f, 0.5f);
            // Frost creeping in from the screen edges (UI overlay under the coin FX), and the golden-harvest flash.
            _frostEdge = UiKit.Panel(canvas, "FrostEdge", Transparent(_theme.FrostEdge), false, false);
            _frostEdge.sprite = Prims.EdgeFadeSprite(256, 0.45f);
            _frostEdge.type = Image.Type.Simple;
            UiKit.Stretch(_frostEdge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _frostEdge.transform.SetSiblingIndex(_fxLayer.GetSiblingIndex());
            _flash = UiKit.Panel(canvas, "Flash", Transparent(_theme.Flash), false, false);
            UiKit.Stretch(_flash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Pre-warm the coin flight pool so a harvest burst never creates UI objects mid-play.
            for (int i = 0; i < CoinPoolWarm; i++)
            {
                var rt = GetCoin();
                rt.gameObject.SetActive(false);
                _pool.Push(rt);
                _coinObjPool.Push(new Coin());
            }
            for (int i = 0; i < NumberPoolWarm; i++)
            {
                var n = MakeNumber();
                n.Rt.gameObject.SetActive(false);
                _numberPool.Push(n);
            }
            _game.Sim.Harvested += OnHarvested;
            _game.Sim.PlotBroken += OnBroken;
            _game.Sim.ComboMilestone += OnComboMilestone;
            _game.Sim.CrowScared += OnCrowScared;
            _game.Sim.YearStarted += OnYearStarted;
            _game.TappedGrowing += OnTappedGrowing;
            _game.Sim.WinterStarted += OnWinter;
            _game.Sim.FrostWarningStarted += OnFrostWarning;
            _game.Sim.GoalCompleted += OnGoalCompleted;
            _game.Sim.WeatherChanged += OnWeatherChanged;
            _game.ApprenticeRoleToggled += OnRoleToggled;
            _game.Sim.PestArrived += OnPestArrived;
            _game.Sim.PestStruck += OnPestStruck;
            _game.Sim.HensAte += OnHensAte;
            _game.Sim.LuckyAppeared += OnLuckyAppeared;
            _game.Sim.LuckyFound += OnLuckyFound;
            _game.Sim.TraderArrived += OnTraderArrived;
            _game.Sim.AchievementUnlocked += OnAchievement;
        }

        private static Color Transparent(Color c) => new Color(c.r, c.g, c.b, 0f);

        private void OnFrostWarning()
        {
            Haptics.Play(HapticKind.Light);
            // GDD §3.1 (v1.9): the last seconds before frost pay extra, and the banner says so.
            double rush = _game.Sim.Config.FrostRushValue;
            if (rush > 1 && !_game.Sim.IsSimulatingOffline)
                Banner(Strings.Format("ui.frost_rush", ("percent", Mathf.RoundToInt((float)((rush - 1) * 100)))));
        }

        private void OnGoalCompleted(YearGoal goal)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Format("goal.met", ("coins", NumberFormat.Short(goal.Reward))));
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.GoalMet);
            _goalKey = -1;
        }

        private void OnWeatherChanged(Weather weather)
        {
            if (weather == Weather.Clear || _game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Get("weather." + weather));
            Haptics.Play(HapticKind.Light);
        }

        // ------------------------------------------------------------------ scarecrows, tractor, roles (GDD §4/§5.1 v2.0)

        private Button _scarecrowButton, _tractorButton;
        private RectTransform _tractorFill;
        private bool _placingScarecrow;

        private void BuildHelperButtons()
        {
            _scarecrowButton = UiKit.Button(_safe, "Scarecrow", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, ToggleScarecrowMode);
            UiKit.Box(_scarecrowButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 300f), SideButton);
            UiKit.ButtonCaption(_scarecrowButton, "warning", Strings.Get("hud.cap_scarecrow"));
            _scarecrowButton.gameObject.SetActive(false);

            _tractorButton = UiKit.Button(_safe, "TractorGo", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, SendTractor);
            UiKit.Box(_tractorButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 420f), SideButton);
            UiKit.ButtonCaption(_tractorButton, "gear", Strings.Get("hud.cap_tractor"));
            var face = _tractorButton.targetGraphic.transform;
            var track = UiKit.Panel(face, "Charge", _theme.BarBackground, true, false);
            UiKit.Box(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(96f, 7f)); // under the caption, along the near edge
            _tractorFill = UiKit.Panel(track.transform, "Fill", _theme.Gold, true, false).rectTransform;
            UiKit.Stretch(_tractorFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            _tractorButton.gameObject.SetActive(false);
        }

        private void ToggleScarecrowMode()
        {
            _placingScarecrow = !_placingScarecrow;
            _game.FieldTapOverride = _placingScarecrow ? (System.Func<Vector2, bool>)PlaceScarecrow : null;
            _scarecrowButton.targetGraphic.color = _placingScarecrow ? _theme.SheetButton : _theme.SheetIdle;
            if (_placingScarecrow)
            {
                CloseSeedBag();
                Banner(Strings.Get("ui.scarecrow_move"));
            }
            Haptics.Play(HapticKind.Selection);
        }

        /// <summary>The tap's nearest plot corner takes the nearest scarecrow; one move, then the mode ends.</summary>
        private bool PlaceScarecrow(Vector2 plot)
        {
            var corner = new GridPos(Mathf.RoundToInt(plot.x + 0.5f), Mathf.RoundToInt(plot.y + 0.5f));
            var list = _game.State.Scarecrows;
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                float dx = list[i].X - corner.X, dy = list[i].Y - corner.Y, d = dx * dx + dy * dy;
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest >= 0 && _game.Sim.MoveScarecrow(nearest, corner))
            {
                Haptics.Play(HapticKind.Light);
                _audio?.Play(SfxId.ScarecrowPlace);
            }
            _placingScarecrow = false;
            _game.FieldTapOverride = null;
            _scarecrowButton.targetGraphic.color = _theme.SheetIdle;
            return true;
        }

        private void SendTractor()
        {
            if (_game.Sim.TriggerTractor()) Haptics.Play(HapticKind.Medium);
            else _audio?.Play(SfxId.Denied, 0.8f);
        }

        // ------------------------------------------------------------------ events and threats (GDD §5.5–§5.7 v2.1)

        private Button _star;
        private RectTransform _starRt;
        private GameObject _traderCard;
        private Button _traderSeed, _traderRare;
        private RectTransform _traderFill;
        private int _traderKey = -1;

        private void BuildEventUi()
        {
            // The shooting star crosses the evening sky over the field; a tap catches it.
            _star = UiKit.Button(_safe, "ShootingStar", "", UiType.Label, _theme.Gold, _theme.SheetButtonText, () =>
            {
                if (_game.Sim.TapStar()) Haptics.Play(HapticKind.Medium);
            });
            _starRt = _star.GetComponent<RectTransform>();
            UiKit.Box(_starRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -_theme.TopPadding - 440f), new Vector2(120f, 120f));
            UiKit.ButtonIcon(_star, "star");
            _star.gameObject.SetActive(false);

            // The trader's card: two offers and how long it stays.
            var card = UiKit.Card(_safe, "TraderCard", _theme.YearCard, false);
            _traderCard = card.gameObject;
            UiKit.Box(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 345f), new Vector2(640f, 200f));
            var title = UiKit.Label(card.transform, "Title", Strings.Get("trader.title"), UiType.Label, _theme.YearCardText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(600f, 44f));
            _traderSeed = UiKit.Button(card.transform, "TraderSeed", "", UiType.Caption, _theme.Seed, _theme.SheetButtonText, () => BuyFromTrader(TraderOffer.Seed));
            UiKit.Box(_traderSeed.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(280f, 96f));
            _traderRare = UiKit.Button(card.transform, "TraderRare", "", UiType.Caption, _theme.SheetButton, _theme.SheetButtonText, () => BuyFromTrader(TraderOffer.RareSeed));
            UiKit.Box(_traderRare.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(280f, 96f));
            var track = UiKit.Panel(card.transform, "Time", _theme.BarBackground, true, false);
            UiKit.Box(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(560f, 10f));
            _traderFill = UiKit.Panel(track.transform, "Fill", _theme.Gold, true, false).rectTransform;
            UiKit.Stretch(_traderFill, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _traderCard.SetActive(false);
        }

        private void BuyFromTrader(TraderOffer offer)
        {
            if (_game.Sim.TraderBuy(offer))
            {
                Haptics.Play(HapticKind.Medium);
                _audio?.Play(SfxId.Purchase);
                _traderKey = -1;
            }
            else _audio?.Play(SfxId.Denied, 0.8f);
        }

        private void RefreshEventUi(FarmState state, float dt)
        {
            bool year = state.Phase == Phase.Year;
            var luck = state.Luck;
            bool star = year && luck.StarLeft > 0f;
            if (_star.gameObject.activeSelf != star) _star.gameObject.SetActive(star);
            if (star)
            {
                float k = 1f - luck.StarLeft / Mathf.Max(0.01f, _game.Sim.Config.ShootingStarSeconds);
                _starRt.anchoredPosition = new Vector2(Mathf.Lerp(-420f, 420f, k), -_theme.TopPadding - 440f + Mathf.Sin(k * Mathf.PI) * 60f);
                _starRt.localRotation = Quaternion.Euler(0f, 0f, SettingsStore.MotionAllowed ? -k * 360f : 0f);
            }

            var t = state.Trader;
            // The trader's card covers the open seed bag; it waits (its clock still runs) until the bag closes.
            bool trader = year && t.Active && !_bagRow.gameObject.activeSelf;
            if (_traderCard.activeSelf != trader) _traderCard.SetActive(trader);
            if (!trader) { _traderKey = -1; return; }
            _traderFill.anchorMax = new Vector2(Mathf.Clamp01(t.TimeLeft / Mathf.Max(0.01f, _game.Sim.Config.TraderSeconds)), 1f);
            int key = (t.SeedSold ? 1 : 0) + (t.RareSold ? 2 : 0) + (int)System.Math.Min(t.SeedPrice, 1e8) * 4;
            if (key == _traderKey) return;
            _traderKey = key;
            UiKit.ButtonLabel(_traderSeed).text = t.SeedSold ? Strings.Get("trader.sold") : Strings.Format("trader.seed", ("price", NumberFormat.Short(t.SeedPrice)));
            UiKit.ButtonLabel(_traderRare).text = t.RareSold ? Strings.Get("trader.sold") : Strings.Format("trader.rare", ("price", NumberFormat.Short(t.RarePrice)));
            _traderSeed.interactable = !t.SeedSold;
            _traderRare.interactable = !t.RareSold;
        }

        // ------------------------------------------------------------------ plot card (what a bed is worth, and why)

        private Button _inspectButton;
        private GameObject _inspectCard;
        private TMP_Text _inspectTitle, _inspectState, _inspectGround, _inspectValue;
        private TMP_Text[] _inspectBonus;
        private bool _inspecting;

        /// <summary>
        /// Five multipliers decide what a bed pays (GDD §2.3-§3.1) and none of them were ever visible. The magnifier
        /// arms one tap: the plot it lands on says what it grows, how far along it is, what ground it stands on, what
        /// it pays right now, and which bonuses are stacked on it.
        /// </summary>
        private void BuildInspect()
        {
            _inspectButton = UiKit.Button(_safe, "Inspect", "", UiType.Label, _theme.SheetIdle, _theme.SheetButtonText, ToggleInspect);
            // Bottom right, where the pause button used to be; End year sits to its left. The bottom band's corner used
            // to hold it alone at the top of the left column, which read as a stray.
            UiKit.Box(_inspectButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 54f), SideButton);
            UiKit.ButtonCaption(_inspectButton, "zoomIn", Strings.Get("hud.cap_inspect"));

            var card = UiKit.Card(_safe, "PlotCard", _theme.YearCard, false);
            _inspectCard = card.gameObject;
            UiKit.Box(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 600f), new Vector2(720f, 420f));
            _inspectTitle = UiKit.Label(card.transform, "Title", "", UiType.Heading, _theme.YearCardText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_inspectTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -14f), new Vector2(664f, 56f));
            // One line: "Carrot . likes Spring" wrapped onto the state line underneath it.
            _inspectTitle.enableWordWrapping = false;
            _inspectTitle.enableAutoSizing = true;
            _inspectTitle.fontSizeMin = 26f;
            _inspectState = UiKit.Label(card.transform, "State", "", UiType.Label, _theme.YearCardMuted, TextAnchor.MiddleLeft);
            UiKit.Box(_inspectState.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -66f), new Vector2(640f, 44f));
            _inspectGround = UiKit.Label(card.transform, "Ground", "", UiType.Label, _theme.YearCardMuted, TextAnchor.MiddleLeft);
            UiKit.Box(_inspectGround.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -110f), new Vector2(640f, 44f));
            _inspectValue = UiKit.Label(card.transform, "Value", "", UiType.Body, _theme.YearCardText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_inspectValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -158f), new Vector2(640f, 48f));
            _inspectBonus = new TMP_Text[5];
            for (int i = 0; i < _inspectBonus.Length; i++)
            {
                _inspectBonus[i] = UiKit.Label(card.transform, "Bonus" + i, "", UiType.Caption, _theme.YearCardText, TextAnchor.MiddleLeft);
                UiKit.Box(_inspectBonus[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -212f - i * 38f), new Vector2(640f, 36f));
            }
            var close = UiKit.Button(card.transform, "Close", Strings.Get("ui.close"), UiType.Caption, _theme.SheetIdle, _theme.SheetButtonText, CloseInspect);
            UiKit.Box(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(200f, 72f));
            _inspectCard.SetActive(false);
        }

        private void ToggleInspect()
        {
            _inspecting = !_inspecting;
            CloseSeedBag();
            _game.PlotTapOverride = _inspecting ? (System.Func<GridPos, bool>)InspectAt : null;
            _inspectButton.targetGraphic.color = _inspecting ? _theme.SheetButton : _theme.SheetIdle;
            if (_inspecting) Banner(Strings.Get("plot.pick"));
            Haptics.Play(HapticKind.Selection);
        }

        private void CloseInspect()
        {
            if (_inspectCard.activeSelf) _inspectCard.SetActive(false);
            _inspecting = false;
            _inspectButton.targetGraphic.color = _theme.SheetIdle;
            if (_game.PlotTapOverride == (System.Func<GridPos, bool>)InspectAt) _game.PlotTapOverride = null;
        }

        /// <summary>Fills the card for one plot. Read-only: looking at a bed never changes it.</summary>
        private bool InspectAt(GridPos pos)
        {
            var sim = _game.Sim;
            var state = _game.State;
            if (!state.InBounds(pos)) return false;
            var plot = state.GetPlot(pos);
            var cfg = sim.Config;
            var crop = cfg.Crops[Mathf.Clamp(plot.Tier, 0, cfg.Crops.Length - 1)];

            _inspectTitle.text = Strings.Format("plot.title",
                ("crop", Strings.Get(crop.Key)), ("season", Strings.Get("season." + crop.Likes)));
            if (plot.IsHard)
                _inspectState.text = Strings.Format("plot.state_hard", ("hits", sim.HitsLeft(plot)), ("percent", Mathf.RoundToInt(plot.Cracks * 100f)));
            else if (plot.IsRipe)
                _inspectState.text = Strings.Format("plot.state_ripe", ("percent", Mathf.RoundToInt((float)sim.Freshness(plot) * 100f)));
            else
                _inspectState.text = Strings.Format(plot.Watering ? "plot.state_watering" : "plot.state_growing", ("percent", Mathf.RoundToInt(plot.Progress * 100f)));
            string ground = Strings.Get(plot.Hardpan ? "ground.hardpan" : "ground." + plot.Ground);
            _inspectGround.text = Strings.Format(plot.Kind == PlotKind.Fertile ? "plot.ground_fertile" : "plot.ground_layer", ("layer", plot.Layer + 1), ("ground", ground));

            double value = crop.Value * state.Stats.CropValueMult * sim.PlotValueMultiplier(plot) * sim.Freshness(plot);
            if (state.FrostWarning) value *= cfg.FrostRushValue;
            _inspectValue.text = Strings.Format("plot.value", ("coins", NumberFormat.Short(value)));

            int line = 0;
            void Bonus(string key, double mult)
            {
                if (line >= _inspectBonus.Length) return;
                _inspectBonus[line++].text = Strings.Format(key, ("percent", Mathf.RoundToInt((float)(mult - 1) * 100f)));
            }
            if (sim.InSeason(plot.Tier)) Bonus("plot.bonus_season", cfg.InSeasonValue);
            if (plot.Kind == PlotKind.Fertile) Bonus("plot.bonus_fertile", cfg.FertileValue);
            if (plot.IsRotated) Bonus("plot.bonus_rotation", cfg.RotationBonus);
            int variety = sim.DifferentNeighbours(plot);
            if (variety > 0) Bonus("plot.bonus_neighbours", 1 + cfg.NeighbourVarietyBonus * variety);
            if (state.FrostWarning) Bonus("plot.bonus_frost", cfg.FrostRushValue);
            int depth = plot.IsHard ? plot.Layer : plot.CropLayer;
            if (depth > 0 && cfg.DepthValue > 0) Bonus("plot.bonus_depth", 1 + cfg.DepthValue * depth);
            if (line == 0) _inspectBonus[line++].text = Strings.Get("plot.bonus_none");
            for (int i = line; i < _inspectBonus.Length; i++) _inspectBonus[i].text = "";

            _inspectCard.SetActive(true);
            _inspectCard.transform.SetAsLastSibling();
            _inspecting = false;
            _inspectButton.targetGraphic.color = _theme.SheetIdle;
            _game.PlotTapOverride = null;
            Haptics.Play(HapticKind.Light);
            return true;
        }

        // ------------------------------------------------------------------ barn share (GDD §3.5 v2.2)

        private Button _storeButton;
        private float _storeShown = -1f;

        private void BuildStoreButton()
        {
            _storeButton = UiKit.Button(_safe, "StoreShare", "", UiType.Caption, _theme.SheetIdle, _theme.SheetButtonText, CycleStoreShare);
            UiKit.Box(_storeButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 300f), SideButton);
            UiKit.ButtonCaption(_storeButton, "home", Strings.Format("hud.cap_barn", ("percent", 0)));
            _storeButton.gameObject.SetActive(false);
        }

        private void CycleStoreShare()
        {
            var shares = _game.Sim.Config.StoreShares;
            int i = System.Array.IndexOf(shares, _game.State.Barn.StoreShare);
            var next = shares[(i + 1) % shares.Length];
            if (_game.Sim.SetStoreShare(next))
            {
                Haptics.Play(HapticKind.Selection);
                Banner(Strings.Format("ui.store_share", ("percent", Mathf.RoundToInt(next * 100f))));
            }
        }

        private void RefreshStoreButton(FarmState state)
        {
            bool show = state.Phase == Phase.Year && state.Stats.BarnCapacity > 0;
            if (_storeButton.gameObject.activeSelf != show) _storeButton.gameObject.SetActive(show);
            if (!show) return;
            float share = state.Barn.StoreShare;
            if (share == _storeShown) return;
            _storeShown = share;
            UiKit.CaptionLabel(_storeButton).text = Strings.Format("hud.cap_barn", ("percent", Mathf.RoundToInt(share * 100f)));
            _storeButton.targetGraphic.color = share > 0f ? _theme.SheetButton : _theme.SheetIdle;
        }

        private void OnPestArrived(PestKind kind, GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Get("pest." + kind));
            Haptics.Play(HapticKind.Light);
        }

        private void OnPestStruck(PestKind kind, GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Get("pest.struck." + kind));
            _audio?.Play(SfxId.Denied, 0.7f);
        }

        private void OnHensAte(GridPos pos)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Get("ui.hens_ate"));
            _audio?.Play(SfxId.HensFlutter);
        }

        private void OnLuckyAppeared(LuckyKind kind)
        {
            if (_game.Sim.IsSimulatingOffline || kind == LuckyKind.GoldenEgg) return;
            Banner(Strings.Get("lucky." + kind));
            Haptics.Play(HapticKind.Light);
            _audio?.Play(SfxId.Sprout, 0.9f, 1.25f); // a small bright twinkle, not the find itself
        }

        private void OnLuckyFound(LuckyKind kind, double coins)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(kind == LuckyKind.ShootingStar ? Strings.Get("lucky.found.ShootingStar") : Strings.Format("lucky.found." + kind, ("coins", NumberFormat.Short(coins))));
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.GoldenHarvest, 0.7f);
        }

        // ------------------------------------------------------------------ getting started (GDD §10.5 v2.5)

        private RectTransform _checklist;
        private readonly Image[] _checkMarks = new Image[FarmSim.ChecklistSteps];
        private readonly TMP_Text[] _checkLines = new TMP_Text[FarmSim.ChecklistSteps];
        private int _checkShown = -1;

        private void BuildChecklist()
        {
            var card = UiKit.Panel(_safe, "Checklist", _theme.HintBackground, true, false);
            _checklist = card.rectTransform;
            // Top right under the pause button, in the sky: at the top left it lay across the island's corner.
            UiKit.Box(_checklist, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -136f), new Vector2(300f, 226f)); // narrow: the coin counter is centred and wide
            var title = UiKit.Label(_checklist, "Title", Strings.Get("check.title"), UiType.Label, _theme.HintAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(270f, 40f));
            for (int i = 0; i < FarmSim.ChecklistSteps; i++)
            {
                _checkMarks[i] = NodeIcons.Image(_checklist, "checkmark", _theme.HintText);
                UiKit.Box(_checkMarks[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -54f - i * 34f), new Vector2(26f, 26f));
                _checkLines[i] = UiKit.Label(_checklist, "Step" + i, Strings.Get("check." + (ChecklistStep)i), UiType.Caption, _theme.HintText, TextAnchor.MiddleLeft);
                UiKit.Box(_checkLines[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -50f - i * 34f), new Vector2(246f, 34f));
                _checkLines[i].enableWordWrapping = false;
                _checkLines[i].enableAutoSizing = true;
                _checkLines[i].fontSizeMax = _checkLines[i].fontSize;
                _checkLines[i].fontSizeMin = 15f;
            }
            card.raycastTarget = false;
            _checklist.gameObject.SetActive(false);
        }

#if TW_DEBUG || UNITY_EDITOR
        /// <summary>UI tour: shows the card with nothing ticked on a farm that is past it (a late generation).</summary>
        public static bool DebugPreviewChecklist;
#endif

        private void RefreshChecklist(FarmState state)
        {
            bool show = state.Phase == Phase.Year && _game.Sim.ChecklistActive;
            int bits = state.ChecklistBits;
#if TW_DEBUG || UNITY_EDITOR
            if (DebugPreviewChecklist && state.Phase == Phase.Year) { show = true; bits = 0; }
#endif
            if (_checklist.gameObject.activeSelf != show) _checklist.gameObject.SetActive(show);
            if (!show || bits == _checkShown) return;
            _checkShown = bits;
            for (int i = 0; i < FarmSim.ChecklistSteps; i++)
            {
                bool done = (bits & (1 << i)) != 0;
                _checkMarks[i].color = done ? _theme.HintAccent : new Color(_theme.HintText.r, _theme.HintText.g, _theme.HintText.b, 0.25f);
                _checkLines[i].color = done ? new Color(_theme.HintText.r, _theme.HintText.g, _theme.HintText.b, 0.55f) : _theme.HintText;
                _checkLines[i].fontStyle = done ? FontStyles.Strikethrough : FontStyles.Normal;
            }
        }

        private void OnAchievement(AchievementId id)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Format("ach.unlocked", ("name", Strings.Get("ach." + id))));
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.Achievement);
        }

        private void OnTraderArrived()
        {
            if (_game.Sim.IsSimulatingOffline) return;
            Banner(Strings.Get("trader.arrived"));
            Haptics.Play(HapticKind.Light);
            _audio?.Play(SfxId.TraderArrive);
        }

        private void OnRoleToggled(int index)
        {
            var role = _game.State.Apprentices[index].Role;
            Banner(Strings.Get("ui.role_" + role));
            Haptics.Play(HapticKind.Selection);
        }

        private void RefreshHelperButtons(FarmState state)
        {
            bool year = state.Phase == Phase.Year;
            bool scare = year && state.Scarecrows.Count > 0;
            if (_scarecrowButton.gameObject.activeSelf != scare)
            {
                _scarecrowButton.gameObject.SetActive(scare);
                if (!scare && _placingScarecrow)
                {
                    _placingScarecrow = false;
                    _game.FieldTapOverride = null;
                    _scarecrowButton.targetGraphic.color = _theme.SheetIdle;
                }
            }
            // The open seed bag's last chip sits where the tractor button is: the bag wins while it is open.
            bool tractor = year && state.Tractor.Owned && !state.GoldenYearActive && !_bagRow.gameObject.activeSelf;
            if (_tractorButton.gameObject.activeSelf != tractor) _tractorButton.gameObject.SetActive(tractor);
            if (tractor)
            {
                float charge = _game.Sim.TractorCharge;
                _tractorFill.anchorMax = new Vector2(charge, 1f);
                bool ready = charge >= _game.Sim.Config.TractorManualReady;
                var face = ready ? _theme.SheetButton : _theme.SheetIdle;
                if (_tractorButton.targetGraphic.color != face) _tractorButton.targetGraphic.color = face;
            }
        }

        /// <summary>The centre banner (combo milestones, goals, weather, the frost rush): one line that rises and fades.</summary>
        private void Banner(string text)
        {
            _milestone.text = text;
            _milestoneLeft = 1.6f;
        }

        // ------------------------------------------------------------------ yearly goal line (GDD §3.3 v1.9)

        private TMP_Text _goalLine;
        private Image _goalPlate;
        private RectTransform _goalPlateRt;
        private long _goalKey = -1;

        private void RefreshGoalLine(FarmState state)
        {
            var goal = state.Goal;
            bool show = goal.Active && state.Phase == Phase.Year;
            if (_goalLine.gameObject.activeSelf != show)
            {
                _goalLine.gameObject.SetActive(show);
                _goalPlate.gameObject.SetActive(show);
            }
            if (!show) { _goalKey = -1; return; }
            long key = (long)goal.Type * 1000000000L + (long)System.Math.Min(goal.Progress, 99999999) * 10 + (goal.Done ? 1 : 0);
            if (key == _goalKey) return;
            _goalKey = key;
            string what;
            switch (goal.Type)
            {
                case GoalType.HarvestCrop:
                    what = Strings.Format("goal.harvest", ("count", NumberFormat.Short(goal.Target)), ("crop", Strings.Get(_game.Sim.Config.Crops[goal.Tier].Key)));
                    break;
                case GoalType.Combo:
                    what = Strings.Format("goal.combo", ("count", NumberFormat.Short(goal.Target)));
                    break;
                default:
                    what = Strings.Format("goal.coins", ("coins", NumberFormat.Short(goal.Target)));
                    break;
            }
            string progress = goal.Done
                ? Strings.Get("goal.done")
                : Strings.Format("goal.progress", ("progress", NumberFormat.Short(System.Math.Min(goal.Progress, goal.Target))), ("target", NumberFormat.Short(goal.Target)));
            _goalLine.text = what + "  ·  " + progress;
            _goalLine.color = goal.Done ? _theme.Gold : _theme.Text;
            // Sized from the character count, not TMP's preferredWidth: that forces a text generation (and an
            // allocation) every time the goal's progress ticks.
            float width = Mathf.Min(1000f, _goalLine.text.Length * _goalLine.fontSize * 0.52f + 48f);
            _goalPlateRt.sizeDelta = new Vector2(width, _goalPlateRt.sizeDelta.y);
        }

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
            _game.Sim.PlotBroken -= OnBroken;
            _game.Sim.ComboMilestone -= OnComboMilestone;
            _game.Sim.GoalCompleted -= OnGoalCompleted;
            _game.Sim.WeatherChanged -= OnWeatherChanged;
            _game.ApprenticeRoleToggled -= OnRoleToggled;
            _game.Sim.PestArrived -= OnPestArrived;
            _game.Sim.PestStruck -= OnPestStruck;
            _game.Sim.HensAte -= OnHensAte;
            _game.Sim.LuckyAppeared -= OnLuckyAppeared;
            _game.Sim.LuckyFound -= OnLuckyFound;
            _game.Sim.TraderArrived -= OnTraderArrived;
            _game.Sim.AchievementUnlocked -= OnAchievement;
            _game.Sim.CrowScared -= OnCrowScared;
            _game.Sim.YearStarted -= OnYearStarted;
            _game.Sim.WinterStarted -= OnWinter;
        }

        private float _holdNudgeAt = -10f;

        /// <summary>
        /// The counter's width as TMP will lay it out: each glyph's advance from the font's character table (the
        /// K/M suffix at its smaller size), plus the label's character spacing. No allocation.
        /// </summary>
        private float CoinTextWidth(char[] chars, int len)
        {
            float size = _coinText.fontSize;
            var font = _coinText.font;
            if (font == null || font.characterLookupTable == null) return len * size * 0.62f;
            var face = font.faceInfo;
            float em = size / Mathf.Max(1f, face.pointSize) * (face.scale > 0f ? face.scale : 1f);
            float spacing = _coinText.characterSpacing * 0.01f * size;
            int digitsEnd = len;
            while (digitsEnd > 0 && char.IsLetter(chars[digitsEnd - 1])) digitsEnd--;
            var table = font.characterLookupTable;
            float width = 0f;
            for (int i = 0; i < len; i++)
            {
                float scale = i >= digitsEnd ? CoinSuffixScale : 1f;
                float advance = table.TryGetValue(chars[i], out var ch) && ch.glyph != null
                    ? ch.glyph.metrics.horizontalAdvance * em
                    : size * 0.62f;
                width += (advance + (i < len - 1 ? spacing : 0f)) * scale;
            }
            return width;
        }

        /// <summary>A tap on a growing crop does nothing by rule (a held finger waters it); after a winter the field is
        /// full of them and a silent tap reads as a dead screen, so the banner says what the crop wants, a few seconds apart.</summary>
        private void OnTappedGrowing(GridPos pos)
        {
            if (Time.unscaledTime - _holdNudgeAt < 3f) return;
            _holdNudgeAt = Time.unscaledTime;
            Banner(Strings.Get("hint.hold"));
            Haptics.Play(HapticKind.Light);
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
            bool hand = e.Source == HarvestSource.Hand;
            // Reaped by hand: 3–8 coins by value, counter punch on arrival. Helpers and the frost: fewer coins, tick only.
            int count = hand ? Mathf.Clamp(3 + (int)(e.Coins / 4.0), 3, 8) : Mathf.Clamp(2 + e.Tier / 2, 2, 4);
            if (e.WasGolden) count = 8;
            SpawnCoins(_game.PlotToWorld(e.Pos, 0.5f), e.Coins, count, hand, e.Tier >= 3 || e.WasGolden, e.WasGolden);
            if (hand) { _lastReapCanvas = WorldToCanvas(_game.PlotToWorld(e.Pos, 0.3f)); _hasReapPos = true; }
        }

        /// <summary>The layer's coins (GDD §2v3.3): a small burst from the broken ground, a big one for a chest or hardpan.</summary>
        private void OnBroken(BreakEvent e)
        {
            if (_game.Sim.IsSimulatingOffline || e.Coins <= 0) return;
            bool rich = e.Chest || e.Hardpan;
            SpawnCoins(_game.PlotToWorld(e.Pos, 0.3f), e.Coins, rich ? 8 : Mathf.Clamp(2 + (int)(e.Coins / 3.0), 2, 5), rich, rich, e.Hardpan);
            if (rich) Banner(Strings.Get(e.Chest ? "ui.chest_found" : "ui.hardpan_broken"));
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
        // ------------------------------------------------------------------ the depot and the damage numbers (GDD §2v3.4–5)

        private sealed class Number
        {
            public RectTransform Rt;
            public TMP_Text Text;
            public Vector2 From;
            public float T, Drift;
        }

        private RectTransform _staminaTrack, _staminaFill;
        private Image _staminaFillImage, _staminaTrackImage;
        private float _staminaShown = -1f;
        private readonly List<Number> _numbers = new List<Number>(32);
        private readonly Stack<Number> _numberPool = new Stack<Number>(32);
        private readonly char[] _numberChars = new char[16];
        private const int NumberPoolWarm = 32;
        private Vector2 _lastReapCanvas;
        private bool _hasReapPos;

        /// <summary>The depot (GDD §2v3.5): a slim bar under the season band, brick when the next swing would be tired.</summary>
        /// <summary>
        /// v3.6: the stamina bar alone under the island, with the Almanac's stamina icon at its left and its name
        /// above it; a bare thin bar under the season timeline read as part of the clock.
        /// </summary>
        private void BuildStamina()
        {
            _staminaTrackImage = UiKit.Panel(_safe, "StaminaTrack", _theme.BarBackground, true, false);
            _staminaTrack = _staminaTrackImage.rectTransform;
            UiKit.Box(_staminaTrack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, _theme.StaminaY), new Vector2(_theme.StaminaWidth, _theme.StaminaHeight));
            _staminaFillImage = UiKit.Panel(_staminaTrack, "Fill", _theme.Seed, true, false);
            _staminaFill = _staminaFillImage.rectTransform;
            UiKit.Stretch(_staminaFill, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            // Dark ink, like the year card below it: the pale HUD text vanished against the light ground under the island.
            var icon = NodeIcons.Image(_staminaTrack, "barsVertical", _theme.YearCardText);
            UiKit.Box(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), Vector2.one * 40f);
            var label = UiKit.Label(_staminaTrack, "StaminaLabel", Strings.Get("hud.stamina"), UiType.Caption, _theme.YearCardText, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 6f), new Vector2(0f, 40f));
            label.enableWordWrapping = false;
        }

        /// <summary>A strike's number over the plot: rising, fading; a crit bigger and in the hot colour, a tired swing grey and small.</summary>
        public void ShowDamage(Vector3 world, double damage, bool crit, bool tired, bool splash)
        {
            if (_fxLayer == null) return;
            var n = _numberPool.Count > 0 ? _numberPool.Pop() : MakeNumber();
            n.From = WorldToCanvas(world) + new Vector2(Random.Range(-24f, 24f), 0f);
            n.T = 0f;
            n.Drift = Random.Range(-30f, 30f);
            int len = NumberFormat.Short(System.Math.Round(damage * 10) / 10, _numberChars);
            n.Text.SetText(_numberChars, 0, len);
            n.Text.color = crit ? _theme.ComboHot : tired || splash ? _theme.TextMuted : _theme.Text;
            n.Rt.localScale = Vector3.one * (crit ? 1.45f : tired || splash ? 0.7f : 1f);
            n.Rt.anchoredPosition = n.From;
            n.Rt.gameObject.SetActive(true);
            _numbers.Add(n);
        }

        private Number MakeNumber()
        {
            var t = UiKit.Label(_fxLayer, "Damage", "", UiType.Heading, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.OutlineStrong(t);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 90f);
            t.raycastTarget = false;
            t.SetText("999.9");
            t.ForceMeshUpdate(true);
            return new Number { Rt = rt, Text = t };
        }

        private void TickNumbers(float dt)
        {
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var n = _numbers[i];
                n.T += dt;
                float t = n.T / 0.75f;
                if (t >= 1f)
                {
                    n.Rt.gameObject.SetActive(false);
                    _numberPool.Push(n);
                    _numbers.RemoveAt(i);
                    continue;
                }
                n.Rt.anchoredPosition = n.From + new Vector2(n.Drift * t, 90f * Prims.EaseOutQuad(t));
                var c = n.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                n.Text.color = c;
            }
        }

        private void RefreshStamina(FarmState state, float dt)
        {
            float max = Mathf.Max(1f, state.Stats.StaminaMax);
            float share = Mathf.Clamp01(state.Stamina / max);
            if (_staminaShown < 0f) _staminaShown = share;
            _staminaShown = Prims.Damp(_staminaShown, share, 14f, dt);
            _staminaFill.anchorMax = new Vector2(_staminaShown, 1f);
            var fill = state.Tired ? _theme.SheetDanger : Color.Lerp(_theme.Seed, _theme.Gold, share);
            if (state.Tired) fill = Color.Lerp(fill, _theme.Text, 0.3f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f));
            _staminaFillImage.color = fill;
            bool show = state.Phase == Phase.Year;
            if (_staminaTrack.gameObject.activeSelf != show) _staminaTrack.gameObject.SetActive(show);
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
            UiKit.Box(_bagButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 180f), SideButton);
            UiKit.ButtonCaption(_bagButton, "basket", Strings.Get("hud.cap_seeds"));
            _bagButton.gameObject.SetActive(false);

            _bagRow = UiKit.Rect("SeedRow", _safe);
            UiKit.Box(_bagRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 420f), new Vector2(1000f, 170f));
            // It sits on the pale bottom band, not on the sky: dark ink, like the cards there.
            _bagHint = UiKit.Label(_bagRow, "Hint", Strings.Get("ui.seed_hint"), UiType.Caption, _theme.YearCardText, TextAnchor.MiddleLeft);
            UiKit.Box(_bagHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(990f, 44f));

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
            Banner(Strings.Get("ui.seed_bed_low"));
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
            Banner(Strings.Format("ui.combo_milestone", ("combo", combo), ("coins", NumberFormat.Short(coins))));
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.ComboMilestone);
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
            _seasonGroup.alpha = _topGroup.alpha; // the season band leaves with the rest of the year HUD
            // A CanvasGroup at alpha 0 still takes taps: the End year button must be untouchable once the year is over.
            bool inYear = state.Phase == Phase.Year;
            if (_seasonGroup.blocksRaycasts != inYear) _seasonGroup.blocksRaycasts = _seasonGroup.interactable = inYear;
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
                // The icon sits a fixed gap left of the number's left edge. The number is centred, so every value
                // moves that edge; its width comes from the font's own glyph advances (preferredWidth would force a
                // text generation, and an allocation, on every change). A count of characters guessed too narrow
                // for the display font's wide digits and let the coin sit on the first one.
                float left = CoinTextOffset - CoinTextWidth(_coinChars, len) * 0.5f;
                _coinIcon.anchoredPosition = new Vector2(left - _theme.CoinIconGap - CoinIconDiameter * 0.5f, 0f);
                _coinGlow.rectTransform.anchoredPosition = _coinIcon.anchoredPosition;
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
            // Milestone banner fades.
            if (_milestoneLeft > 0f)
            {
                _milestoneLeft -= dt;
                float k = Mathf.Clamp01(_milestoneLeft / 1.6f);
                _milestone.alpha = Mathf.Min(1f, k * 3f);
                _milestone.rectTransform.anchoredPosition = new Vector2(0f, 120f + (1f - k) * 60f);
            }
            else if (_milestone.alpha > 0f) _milestone.alpha = 0f;
            RefreshStamina(state, dt);
            TickNumbers(dt);
            RefreshGoalLine(state);
            RefreshHelperButtons(state);
            RefreshEventUi(state, dt);
            RefreshStoreButton(state);
            // The magnifier and its card belong to the running year; winter closes them.
            bool year = state.Phase == Phase.Year;
            if (_inspectButton.gameObject.activeSelf != year) _inspectButton.gameObject.SetActive(year);
            if (!year && _inspectCard.activeSelf) CloseInspect();
            RefreshChecklist(state);
            // The seed bag: once a second crop is unlocked, during the year, never on the Golden Year's field.
            bool bag = state.Stats.MaxTierUnlocked > 0 && !state.IsWinter && !state.GoldenYearActive;
            if (_bagButton.gameObject.activeSelf != bag)
            {
                _bagButton.gameObject.SetActive(bag);
                if (!bag) CloseSeedBag();
            }
            if (_bagRow.gameObject.activeSelf) RefreshSeedChips(false);

            MCombo.Begin();
            // The swipe's count floats over the last crop it took; it keeps its value and fades out.
            if (state.Combo >= 2)
            {
                if (state.Combo != _lastCombo) _combo.SetText("×{0}", state.Combo);
                _comboFade = 1f;
                if (state.Combo != _lastCombo) _comboRt.localScale = Vector3.one * 1.35f;
            }
            else _comboFade = Mathf.Max(0f, _comboFade - dt / 0.4f);
            _lastCombo = state.Combo;
            if (_hasReapPos && state.Combo != _lastCombo && state.Combo >= 2) _comboRt.anchoredPosition = _lastReapCanvas + new Vector2(60f, 90f);
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
                _rate.text = perSecond >= 0.5 && state.Phase == Phase.Year ? Strings.Format("ui.rate", ("coins", NumberFormat.Short(perSecond))) : "";
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
