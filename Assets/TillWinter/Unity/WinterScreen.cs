using System.Collections.Generic;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The Winter screen (GDD §10.2): almanac page over the frozen field. Top bar, the skill-tree canvas,
    /// a bottom sheet for the selected node, Next Year / Pass on the farm. Shown whenever the phase is not Year.
    /// In the Heritage phase (and via the small toggle in Winter) the same canvas shows the Heritage tree.
    /// </summary>
    public sealed class WinterScreen : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private TreeTheme _theme, _heritageTheme;
        private GenerationCard _card;
        private Image _overlay, _page;
        private TMP_Text _hintCaption;
        private GameObject _retireSheet;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _safe;
        private TMP_Text _title, _coins, _greenhouse, _retireHint;
        private RectTransform _coinsRt;
        private Button _treeToggle;
        private SkillTreeView _almanac, _heritage;
        private RectTransform _sheet;
        private TMP_Text _sheetName, _sheetBranch, _sheetDesc, _sheetLevel, _sheetCost, _sheetReason;
        private Image _sheetTag, _sheetCoin;
        private Button _buy;
        private Button _nextYear, _retire, _startGen;
        private GameObject _confirm;
        private TMP_Text _confirmText;

        private bool _visible;
        private bool _showingHeritage;
        private float _open;
        private float _sheetShown;
        private const int FlakeCount = 36;
        private const int StarCount = 28;
        private RectTransform[] _stars;
        private Image[] _starImages;
        private Image _frost, _aged;
        private float _frostT;
        private RectTransform _snowLayer;
        private RectTransform[] _flakes;
        private float[] _flakeSpeed;
        private bool _closing;
        private RichNumber _coinRich, _seedRich;
        private Image _sheetBadge, _sheetIcon;
        private TMP_Text _sheetUnlocks;

        /// <summary>"Unlocks: …" — the nodes in the open tree that need this one (empty when none do).</summary>
        private string UnlocksText(string id)
        {
            var names = new System.Text.StringBuilder();
            foreach (var n in ActiveView.Tree.Nodes)
            {
                if (System.Array.IndexOf(n.Prerequisites, id) < 0) continue;
                if (names.Length > 0) names.Append(", ");
                names.Append(Strings.Name(n));
            }
            return names.Length == 0 ? "" : Strings.Format("ui.unlocks", ("names", names.ToString()));
        }
        private double _coinsTextValue = -1;
        private readonly char[] _coinChars = new char[64];
        private bool _coinsTextHeritage;
        private long _ghCoinsKey = -2;
        private int _ghSecondsKey = -2, _retireSeedsKey = -2;
        private Vector2 _sheetHome;
        private double _coinsShown;
        private float _coinPunch;
        private float _coinTick;
        private string _selectedId;

        public bool IsOpen => _visible;
        public SkillTreeView AlmanacView => _almanac;
        public SkillTreeView HeritageView => _heritage;
        public string SelectedId => _selectedId;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            _theme = TreeTheme.Load();
            _heritageTheme = TreeTheme.Load("HeritageTheme");

            var overlay = UiKit.Panel(canvas, "WinterScreen", _theme.Overlay, false, true);
            _overlay = overlay;
            _root = overlay.gameObject;
            _group = _root.AddComponent<CanvasGroup>();
            _safe = UiKit.Rect("SafeArea", overlay.transform);
            ApplySafeArea();

            // Paper page with a soft vignette.
            var page = UiKit.Panel(_safe, "Page", _theme.Paper, false, false);
            _page = page;
            UiKit.Stretch(page.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); // full screen: no sky around the page
            var vignette = UiKit.Panel(page.transform, "Vignette", _theme.PaperVignette, true, false);
            vignette.sprite = Prims.CircleSprite(256);
            vignette.type = Image.Type.Simple;
            vignette.color = new Color(_theme.PaperVignette.r, _theme.PaperVignette.g, _theme.PaperVignette.b, 0f);
            UiKit.Stretch(vignette.rectTransform, Vector2.zero, Vector2.one, new Vector2(-400f, -700f), new Vector2(400f, 700f));
            var edge = UiKit.Panel(page.transform, "Edge", new Color(0f, 0f, 0f, 0f), true, false);
            edge.color = new Color(_theme.PaperVignette.r, _theme.PaperVignette.g, _theme.PaperVignette.b, 0.18f);
            UiKit.Stretch(edge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // A warm lamp over the page and a slow snowfall behind the tree: winter outside, a lit room inside.
            var lamp = UiKit.Panel(page.transform, "Lamp", _theme.Lamp, false, false);
            lamp.sprite = Sprite.Create(Prims.RadialGradient(128, 0f, 1f), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
            lamp.type = Image.Type.Simple;
            UiKit.Box(lamp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 1100f));
            // The Almanac is paper too: a faint grain keeps the page from reading as flat dark space.
            var grain = UiKit.Panel(page.transform, "Grain", new Color(_theme.Ink.r, _theme.Ink.g, _theme.Ink.b, 0.05f), false, false);
            grain.sprite = UiKit.Grain;
            grain.type = Image.Type.Tiled;
            UiKit.Stretch(grain.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _aged = UiKit.Panel(page.transform, "Aged", _heritageTheme.Aged, false, false);
            _aged.sprite = UiKit.Grain;
            _aged.type = Image.Type.Tiled;
            _aged.gameObject.SetActive(false);
            _stars = new RectTransform[StarCount];
            _starImages = new Image[StarCount];
            var starRnd = new System.Random(23);
            for (int i = 0; i < StarCount; i++)
            {
                float size = 3f + (float)starRnd.NextDouble() * 4f;
                var star = UiKit.CircleImage(page.transform, "Star", _theme.Stars, Vector2.zero, size);
                star.raycastTarget = false;
                _starImages[i] = star;
                _stars[i] = star.rectTransform;
                _stars[i].anchorMin = _stars[i].anchorMax = new Vector2((float)starRnd.NextDouble(), 0.62f + (float)starRnd.NextDouble() * 0.36f);
            }
            _frost = UiKit.Panel(page.transform, "Frost", _theme.Frost, false, false);
            _frost.sprite = Prims.EdgeFadeSprite(256, 0.84f); // a thin rim of frost, not a fog over the page
            _frost.type = Image.Type.Simple;
            _snowLayer = UiKit.Rect("Snowfall", page.transform);
            UiKit.Stretch(_snowLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _flakes = new RectTransform[FlakeCount];
            _flakeSpeed = new float[FlakeCount];
            var rnd = new System.Random(11);
            for (int i = 0; i < FlakeCount; i++)
            {
                float size = 3f + (float)rnd.NextDouble() * 6f;
                var flake = UiKit.CircleImage(_snowLayer, "Flake", _theme.Snowfall, Vector2.zero, size);
                flake.raycastTarget = false;
                _flakes[i] = flake.rectTransform;
                _flakes[i].anchorMin = _flakes[i].anchorMax = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());
                _flakeSpeed[i] = 0.015f + size * 0.004f;
            }

            // Top bar.
            var top = UiKit.Rect("TopBar", _safe);
            UiKit.Stretch(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -220f), new Vector2(-40f, -30f));
            _title = UiKit.Label(top, "Title", "", UiType.Title, _theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);
            _title.fontSize = 44;
            // One line, always: wrapped, "Winter - Year 2 . Generation 3" fell onto the grade stars and the year summary.
            _title.enableWordWrapping = false;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 26f;
            _title.fontSizeMax = 44f;
            _coins = UiKit.Label(top, "Coins", "0", UiType.Title, _theme.Ink, TextAnchor.UpperRight, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            UiKit.Stretch(_coinsRt, new Vector2(0.58f, 0.5f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-200f, 0f));
            _coins.fontSize = 42;
            _coins.richText = true;
            // One line always: "10K coins" in Turkish wrapped onto the retire hint below it. Long values shrink instead.
            _coins.enableWordWrapping = false;
            _coins.enableAutoSizing = true;
            _coins.fontSizeMin = 26f;
            _coins.fontSizeMax = 42f;
            _coinRich = new RichNumber(0.72f, _theme.Accent);
            _seedRich = new RichNumber(0.72f, _theme.Seed);
            var strip = UiKit.Gradient(top, "YearStrip", new Color(_theme.Accent.r, _theme.Accent.g, _theme.Accent.b, 0.16f), true);
            UiKit.Box(strip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -8f), new Vector2(1080f, 84f));
            _yearSummary = UiKit.Label(top, "YearSummary", "", UiType.Label, _theme.Ink, TextAnchor.LowerLeft);
            UiKit.Box(_yearSummary.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 44f), new Vector2(720f, 40f));
            // The year's grade (GDD §3.4 v1.9): three stars, the bonus they paid, and whether the goal was met.
            _gradeStars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                _gradeStars[i] = NodeIcons.Image(top, "star", _theme.Accent);
                UiKit.Box(_gradeStars[i].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f + i * 44f, 92f), new Vector2(40f, 40f));
            }
            _gradeLine = UiKit.Label(top, "Grade", "", UiType.Label, _theme.InkMuted, TextAnchor.LowerLeft);
            UiKit.Box(_gradeLine.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(148f, 92f), new Vector2(700f, 40f));
            _greenhouse = UiKit.Label(top, "Greenhouse", "", UiType.Label, _theme.InkMuted, TextAnchor.LowerLeft);
            UiKit.Stretch(_greenhouse.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 0.5f), Vector2.zero, Vector2.zero);
            _retireHint = UiKit.Label(top, "RetireHint", "", UiType.Label, _theme.Seed, TextAnchor.LowerRight, FontStyle.Bold);
            UiKit.Stretch(_retireHint.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-210f, 0f));
            _treeToggle = UiKit.Button(top, "TreeToggle", Strings.Get("ui.heritage"), UiType.Caption, _theme.Seed, _theme.Paper, ToggleTree);
            UiKit.Box(_treeToggle.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -30f), new Vector2(200f, 52f));

            // Tree canvases.
            var almanacRt = UiKit.Rect("AlmanacTree", _safe);
            UiKit.Stretch(almanacRt, Vector2.zero, Vector2.one, new Vector2(34f, 330f), new Vector2(-34f, -240f));
            _almanac = almanacRt.gameObject.AddComponent<SkillTreeView>();
            _almanac.Init(game, game.Sim.Almanac, _theme, SkillTreeLayout.Compute(game.Sim.Almanac.Nodes));
            _almanac.Selected += OnSelected;
            var heritageRt = UiKit.Rect("HeritageTree", _safe);
            UiKit.Stretch(heritageRt, Vector2.zero, Vector2.one, new Vector2(34f, 330f), new Vector2(-34f, -240f));
            _heritage = heritageRt.gameObject.AddComponent<SkillTreeView>();
            _heritage.Init(game, game.Sim.Heritage, _heritageTheme, SkillTreeLayout.Compute(game.Sim.Heritage.Nodes));
            _heritage.Selected += OnSelected;

            BuildSheet();
            BuildBarnStrip();
            BuildHeirStrip();

            // Bottom buttons.
            _nextYear = UiKit.Button(_safe, "NextYear", Strings.Get("ui.next_year") + "  »", UiType.Heading, _theme.Accent, _theme.Ink, OnNextYear);
            UiKit.Box(_nextYear.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 40f), new Vector2(490f, 120f));
            _retire = UiKit.Button(_safe, "Retire", Strings.Get("ui.pass_on"), UiType.Label, _theme.Seed, _theme.Paper, OnRetirePressed);
            UiKit.Box(_retire.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 40f), new Vector2(490f, 120f));
            _startGen = UiKit.Button(_safe, "StartGeneration", Strings.Get("ui.start_generation") + "  »", UiType.Heading, _theme.Accent, _theme.Ink, OnStartGeneration);
            UiKit.Box(_startGen.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 120f));

            BuildConfirm();

            // At the top of the tree, not the bottom: down there it sat under the node sheet. Light text, because
            // Paper is the dark page colour now and read as dark on dark.
            var cap = UiKit.Panel(_safe, "HintCaption", _theme.Dim, true, false);
            UiKit.Box(cap.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(960f, 76f));
            _hintCaption = UiKit.Label(cap.transform, "Text", "", UiType.Label, _theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_hintCaption.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            cap.gameObject.SetActive(false);
            BuildRetireSheet();

            _card = canvas.gameObject.AddComponent<GenerationCard>();
            _card.Init(game, audio, canvas);

            _root.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += OnPurchased;
            _game.Sim.Retired += _ => { _showingHeritage = true; RefreshAll(); };
        }

        /// <summary>Stars twinkle over the page; the frost frame eases from a freeze to a thin rim.</summary>
        private void TickSky(float dt)
        {
            _frostT = Mathf.Min(1f, _frostT + dt / 1.1f);
            var fc = _theme.Frost;
            fc.a = Mathf.Lerp(0.7f, _theme.Frost.a * 0.18f, UiMotion.EaseInOut(_frostT));
            _frost.color = fc;
            if (_starImages == null) return;
            float t = Time.unscaledTime;
            bool moving = SettingsStore.MotionAllowed;
            for (int i = 0; i < _starImages.Length; i++)
            {
                var c = _theme.Stars;
                c.a *= moving ? 0.55f + 0.45f * Mathf.Sin(t * (1.1f + i * 0.13f) + i) : 0.8f;
                _starImages[i].color = c;
            }
        }

        /// <summary>Flakes drift down the page and sway; each wraps to the top when it leaves the bottom.</summary>
        private void TickSnow(float dt)
        {
            if (_flakes == null || !SettingsStore.MotionAllowed) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < _flakes.Length; i++)
            {
                var f = _flakes[i];
                var a = f.anchorMin;
                a.y -= _flakeSpeed[i] * dt;
                a.x += Mathf.Sin(t * 0.8f + i * 1.7f) * 0.004f * dt * 6f;
                if (a.y < -0.02f) { a.y = 1.02f; a.x = Mathf.Repeat(a.x + 0.37f, 1f); }
                f.anchorMin = f.anchorMax = a;
            }
        }

        private void ApplySafeArea()
        {
            // Device notches/home bars only; the editor Game view reports window-sized safe areas that would shift the page.
            var min = Vector2.zero;
            var max = Vector2.one;
            if (Application.isMobilePlatform && Screen.width > 0 && Screen.height > 0)
            {
                var sa = Screen.safeArea;
                min = new Vector2(Mathf.Clamp01(sa.xMin / Screen.width), Mathf.Clamp01(sa.yMin / Screen.height));
                max = new Vector2(Mathf.Clamp01(sa.xMax / Screen.width), Mathf.Clamp01(sa.yMax / Screen.height));
            }
            UiKit.Stretch(_safe, min, max, Vector2.zero, Vector2.zero);
        }

        private void BuildSheet()
        {
            _sheet = UiKit.Rect("Sheet", _safe);
            UiKit.Stretch(_sheet, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 180f), new Vector2(-34f, 560f));
            UiKit.Card(_sheet, "Bg", _theme.Paper); // its own shadow replaces the hand-made shade

            _sheetTag = UiKit.Panel(_sheet, "Tag", _theme.Hand, true, false);
            UiKit.Box(_sheetTag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(190f, 44f));
            _sheetBranch = UiKit.Label(_sheetTag.transform, "BranchName", "", UiType.Caption, _theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            _sheetName = UiKit.Label(_sheet, "Name", "", UiType.Heading, _theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(_sheetName.rectTransform, new Vector2(0f, 1f), new Vector2(0.66f, 1f), new Vector2(230f, -70f), new Vector2(0f, -14f));
            // The node itself, large, beside its name: the sheet is about that circle on the tree.
            _sheetBadge = UiKit.CircleImage(_sheet, "Badge", _theme.Hand, Vector2.zero, 112f);
            UiKit.Box(_sheetBadge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-80f, -108f), Vector2.one * 86f);
            var badgeInner = UiKit.CircleImage(_sheetBadge.transform, "Inner", _theme.Paper, Vector2.zero, 64f);
            _sheetIcon = NodeIcons.Image(badgeInner.transform, "", _theme.Ink);
            UiKit.Box(_sheetIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 40f);
            _sheetLevel = UiKit.Label(_sheet, "Level", "", UiType.Label, _theme.InkMuted, TextAnchor.UpperRight);
            UiKit.Stretch(_sheetLevel.rectTransform, new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-24f, -18f));
            _sheetDesc = UiKit.Label(_sheet, "Desc", "", UiType.Label, _theme.Ink, TextAnchor.UpperLeft);
            _effectNow = UiKit.Label(_sheet, "EffectNow", "", UiType.Body, _theme.InkMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Box(_effectNow.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 96f), new Vector2(200f, 44f)); // its right edge used to sit at x = 24, so it grew out of the sheet
            _effectArrow = UiKit.Label(_sheet, "EffectArrow", "»", UiType.Body, _theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_effectArrow.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(228f, 96f), new Vector2(50f, 44f));
            _effectNext = UiKit.Label(_sheet, "EffectNext", "", UiType.Body, _theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_effectNext.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(282f, 96f), new Vector2(240f, 44f));
            var gainTrack = UiKit.Panel(_sheet, "GainTrack", _theme.EdgeDim, true, false);
            UiKit.Box(gainTrack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 74f), new Vector2(520f, 8f));
            _gainFill = UiKit.Panel(gainTrack.transform, "Fill", _theme.Accent, true, false);
            UiKit.Stretch(_gainFill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            UiKit.Stretch(_sheetDesc.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(24f, 172f), new Vector2(0f, -84f)); // stays above the effect row
            _sheetUnlocks = UiKit.Label(_sheet, "Unlocks", "", UiType.Caption, _theme.InkMuted, TextAnchor.MiddleLeft);
            UiKit.Box(_sheetUnlocks.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 128f), new Vector2(560f, 38f));
            _sheetUnlocks.enableWordWrapping = false;
            _sheetUnlocks.overflowMode = TextOverflowModes.Ellipsis;

            _buy = UiKit.Button(_sheet, "Buy", Strings.Get("ui.buy"), UiType.Body, _theme.Accent, _theme.Ink, OnBuy);
            UiKit.Box(_buy.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(330f, 96f));
            _sheetCoin = UiKit.CircleImage(_sheet, "Currency", _theme.Coin, Vector2.zero, 36f);
            UiKit.Box(_sheetCoin.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 168f), new Vector2(36f, 36f)); // against the price, not adrift
            _sheetCost = UiKit.Label(_sheet, "Cost", "", UiType.Body, _theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Box(_sheetCost.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 164f), new Vector2(260f, 44f));
            _sheetReason = UiKit.Label(_sheet, "Reason", "", UiType.Caption, _theme.Danger, TextAnchor.MiddleRight);
            UiKit.Box(_sheetReason.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 128f), new Vector2(360f, 32f));
            _sheetHome = _sheet.anchoredPosition;
            _sheet.gameObject.SetActive(false);
        }

        /// <summary>One-time sheet at the first Winter with CanRetire (GDD §10.5).</summary>
        private void BuildRetireSheet()
        {
            var dim = UiKit.Panel(_safe, "RetireHintDim", _theme.DimLight, false, true);
            _retireSheet = dim.gameObject;
            var box = UiKit.Card(dim.transform, "Box", _theme.Paper);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 420f));
            var text = UiKit.Label(box.transform, "Text", Strings.Get("hint.first_can_retire"), UiType.Body, _theme.Ink, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 150f), new Vector2(-50f, -40f));
            var ok = UiKit.Button(box.transform, "Ok", Strings.Get("hint.got_it"), UiType.Body, _theme.Seed, _theme.Paper, () => { _retireSheet.SetActive(false); });
            UiKit.Box(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 96f));
            _retireSheet.AddComponent<SheetTransition>().Page = box.rectTransform;
            _retireSheet.SetActive(false);
        }

        private TMP_Text _confirmLose;

        /// <summary>One column of the retire confirmation: an icon, a heading and a list; returns the list label.</summary>
        private TMP_Text ConfirmColumn(RectTransform box, float side, string titleKey, string icon, Color accent)
        {
            float x = side * 205f;
            var mark = NodeIcons.Image(box, icon, accent);
            UiKit.Box(mark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x - 120f, -250f), Vector2.one * 40f);
            var head = UiKit.Label(box, titleKey, Strings.Get(titleKey), UiType.Body, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + 30f, -250f), new Vector2(260f, 48f));
            var list = UiKit.Label(box, titleKey + "List", "", UiType.Label, _theme.InkMuted, TextAnchor.UpperLeft);
            UiKit.Box(list.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -285f), new Vector2(380f, 170f));
            list.lineSpacing = 14f;
            return list;
        }

        private void ShowCaption(string key, bool show)
        {
            var go = _hintCaption.transform.parent.gameObject;
            if (show) _hintCaption.text = Strings.Get(key);
            if (go.activeSelf != show) go.SetActive(show);
        }

        private void BuildConfirm()
        {
            var dim = UiKit.Panel(_safe, "ConfirmDim", _theme.Dim, false, true);
            _confirm = dim.gameObject;
            var box = UiKit.Card(dim.transform, "ConfirmBox", _theme.Paper);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f));
            UiKit.SheetDecor(box, 118f);
            var title = UiKit.Label(box.transform, "Title", Strings.Get("ui.confirm_title"), UiType.Title, _theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(860f, 70f));
            _confirmText = UiKit.Label(box.transform, "Text", "", UiType.Label, _theme.Ink, TextAnchor.UpperCenter);
            UiKit.Box(_confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(800f, 70f));
            // What stays and what goes, side by side, each under its own mark.
            _confirmLose = ConfirmColumn(box.rectTransform, -1f, "ui.confirm_lose_title", "warning", _theme.Danger);
            ConfirmColumn(box.rectTransform, 1f, "ui.confirm_keep_title", "checkmark", _theme.Field).text = Strings.Get("ui.confirm_keep");
            var yes = UiKit.Button(box.transform, "Yes", Strings.Get("ui.retire"), UiType.Body, _theme.Seed, _theme.Paper, OnRetireConfirmed);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 40f), new Vector2(380f, 110f));
            var no = UiKit.Button(box.transform, "No", Strings.Get("ui.cancel"), UiType.Body, _theme.InkMuted, _theme.Paper, () => { _confirm.SetActive(false); });
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 40f), new Vector2(380f, 110f));
            _confirm.AddComponent<SheetTransition>().Page = box.rectTransform;
            _confirm.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.WinterStarted -= Open;
            _game.Sim.Purchased -= OnPurchased;
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            if (_game.State.IsDaily) return; // the daily farm ends on its own card
            _visible = true;
            _closing = false;
            _group.blocksRaycasts = true;
            _open = 0f;
            _frostT = 0f; // the window freezes over as Winter opens, then clears to a frame
            _root.SetActive(true);
            _game.InputBlocked = true;
            PauseMenu.Instance?.PlaceButton(true);
            _showingHeritage = _game.State.Phase == Phase.Heritage;
            _selectedId = null;
            _sheetShown = 0f;
            _coinsShown = _game.State.Coins;
            _coinsTextValue = -1;
            _ghCoinsKey = -2;
            _retireSeedsKey = -2;
            ApplySafeArea();
            RefreshAll();
            ActiveView.OnOpened();

            var sim = _game.Sim;
            if (_game.State.Phase == Phase.Winter && sim.MarkHint(Hint.FirstWinter))
            {
                _almanac.CenterOn("ring_radius", "irrigation");
                _almanac.Highlight("ring_radius", "irrigation");
                ShowCaption("hint.first_winter", true);
            }
            if (_game.State.Phase == Phase.Winter && sim.CanRetire && sim.MarkHint(Hint.FirstCanRetire)) _retireSheet.SetActive(true);
            if (_game.State.Phase == Phase.Heritage && sim.MarkHint(Hint.FirstHeritage)) ShowCaption("hint.first_heritage", true);
        }

        private void Close()
        {
            ShowCaption("", false);
            _retireSheet.SetActive(false);
            _confirm.SetActive(false);
            _almanac.ClearHighlight();
            ActiveView.OnClosed();
            _visible = false;
            // Thaw: the page fades off the field instead of vanishing (LateUpdate finishes it).
            _closing = _root.activeSelf && !_card.IsOpen;
            if (_closing) _group.blocksRaycasts = false; // the fading page must not swallow the first taps of the year
            else _root.SetActive(false);
            _confirm.SetActive(false);
            _game.InputBlocked = false;
            PauseMenu.Instance?.PlaceButton(false);
        }

        /// <summary>Android back: closes the topmost sheet (confirm, retire sheet, node sheet), otherwise does nothing (never quits).</summary>
        public bool HandleBack()
        {
            if (!_visible) return false;
            if (_confirm.activeSelf) { _confirm.SetActive(false); return true; }
            if (_retireSheet.activeSelf) { _retireSheet.SetActive(false); return true; }
            if (_selectedId != null) { _selectedId = null; RefreshSheet(); return true; }
            return false;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; // the Android back button arrives as Escape
            if (_visible && kb != null && kb.escapeKey.wasPressedThisFrame) HandleBack();
        }

        private SkillTreeView ActiveView => _showingHeritage ? _heritage : _almanac;

        private void ToggleTree()
        {
            if (_game.State.Phase == Phase.Heritage) return;
            ActiveView.OnClosed();
            _showingHeritage = !_showingHeritage;
            _selectedId = null;
            RefreshAll();
            ActiveView.OnOpened();
        }

        private void OnSelected(string id)
        {
            _selectedId = id;
            RefreshSheet();
        }

        private void OnNextYear()
        {
            _game.Sim.StartNextYear();
            Close();
        }

        private void OnStartGeneration()
        {
            _audio.Play(SfxId.NewGeneration);
            _game.Sim.StartNewGeneration();
            Close();
        }

        private void OnRetirePressed()
        {
            if (!_game.Sim.CanRetire) return;
            var s = _game.State;
            _confirmText.text = Strings.Format("ui.confirm_head", ("seeds", _game.Sim.SeedsIfRetiredNow), ("gen", s.Generation.Generation + 1));
            _confirmLose.text = Strings.Format("ui.confirm_lose", ("coins", NumberFormat.Short(s.Coins)), ("field", s.GridSize + "x" + s.GridSize));
            _confirm.SetActive(true);
        }

        private void OnRetireConfirmed()
        {
            _confirm.SetActive(false);
            _retireSheet.SetActive(false);
            var sim = _game.Sim;
            int gen = sim.State.Generation.Generation;
            int seeds = sim.SeedsIfRetiredNow;
            if (sim.Retire())
            {
                _audio.Duck(1f, -6f);
                _audio.Play(SfxId.RetireSwell);
                Haptics.Play(HapticKind.Heavy);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.06f, 0.35f);
                VfxPlayer.Fire(VfxId.RetireSnow, Vector3.zero);
                _showingHeritage = true;
                _selectedId = null;
                _group.alpha = 0f;
                _card.Show(new RetireEvent(seeds, gen + 1), () =>
                {
                    _open = 0f;
                    _game.InputBlocked = true;
                    RefreshAll();
                    ActiveView.OnOpened();
                    if (sim.MarkHint(Hint.FirstHeritage)) ShowCaption("hint.first_heritage", true);
                });
            }
            RefreshAll();
        }

        private void OnBuy()
        {
            if (_selectedId == null) return;
            if (_game.Sim.TryBuy(_selectedId))
            {
                _audio.Play(SfxId.Purchase);
                Haptics.Play(HapticKind.Medium);
            }
            else
            {
                _audio.Play(SfxId.Denied);
                RefreshSheet();
            }
        }

        private void OnPurchased(PurchaseEvent e)
        {
            (e.Tree == TreeKind.Almanac ? _almanac : _heritage).OnPurchased(e.NodeId);
            if (_almanac.HasHighlight) { _almanac.ClearHighlight(); ShowCaption("hint.first_winter", false); }
            if (e.Tree == TreeKind.Almanac) _coinPunch = 1f;
            RefreshAll();
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshAll()
        {
            if (!_visible) return;
            var s = _game.State;
            bool heritagePhase = s.Phase == Phase.Heritage;
            if (heritagePhase) _showingHeritage = true;
            _overlay.color = heritagePhase ? _heritageTheme.Overlay : _theme.Overlay;
            _page.color = _showingHeritage ? _heritageTheme.Paper : _theme.Paper;
            if (_aged != null) _aged.gameObject.SetActive(_showingHeritage);
            _title.color = _showingHeritage ? _heritageTheme.Ink : _theme.Ink;
            _coins.color = _showingHeritage ? _heritageTheme.Seed : _theme.Ink;
            _title.text = heritagePhase
                ? Strings.Format("ui.heritage_title", ("gen", s.Generation.Generation)) + (_game.Sim.HeritageComplete ? "  ·  " + Strings.Get("heritage.complete") : "")
                : Strings.Format("ui.winter_title", ("year", s.Year)) + "  ·  " + Strings.Format("gen.title", ("gen", s.Generation.Generation));
            _almanac.gameObject.SetActive(!_showingHeritage);
            _heritage.gameObject.SetActive(_showingHeritage);
            _almanac.Refresh(false);
            _heritage.Refresh(false);
            _treeToggle.gameObject.SetActive(!heritagePhase);
            UiKit.ButtonLabel(_treeToggle).text = _showingHeritage ? Strings.Get("ui.almanac") : Strings.Get("ui.heritage");
            _nextYear.gameObject.SetActive(!heritagePhase);
            _retire.gameObject.SetActive(!heritagePhase);
            _startGen.gameObject.SetActive(heritagePhase);
            if (!heritagePhase)
            {
                bool can = _game.Sim.CanRetire;
                _retire.interactable = can;
                UiKit.ButtonLabel(_retire).text = Strings.Get("ui.pass_on") + "\n" + (can
                    ? Strings.Format("ui.pass_on_seeds", ("seeds", _game.Sim.SeedsIfRetiredNow))
                    : Strings.Format("ui.pass_on_need", ("coins", NumberFormat.Short(_game.Sim.Config.HeritageThreshold - s.Generation.LifetimeCoinsThisGeneration))));
                UiKit.ButtonLabel(_retire).fontSizeMax = UiType.Size(26);
            }
            RefreshSheet();
        }

        private void RefreshSheet()
        {
            if (_selectedId == null || _game.Sim.GetNode(_selectedId) == null)
            {
                _sheet.gameObject.SetActive(false);
                return;
            }
            var sim = _game.Sim;
            var node = sim.GetNode(_selectedId);
            bool heritage = sim.Heritage.Contains(_selectedId);
            _sheet.gameObject.SetActive(true);
            var col = _theme.BranchColor(node.Branch);
            _sheetTag.color = col;
            _sheetBadge.color = col;
            var iconSprite = NodeIcons.Get(node.IconKey);
            if (iconSprite != null) _sheetIcon.sprite = iconSprite;
            _sheetBranch.text = Strings.Branch(node.Branch);
            _sheetName.text = Strings.Name(node);
            _sheetDesc.text = Strings.Description(sim, node);
            _sheetUnlocks.text = UnlocksText(node.Id);
            ShowEffect(sim, node);
            int level = _game.State.GetLevel(_selectedId);
            int max = sim.GetMaxLevel(_selectedId);
            _sheetLevel.text = Strings.Format("ui.level", ("level", level), ("max", max));
            bool maxed = sim.IsMaxed(_selectedId);
            bool available = sim.IsAvailable(_selectedId);
            bool can = sim.CanBuy(_selectedId);
            _sheetCoin.color = heritage ? _theme.Seed : _theme.Coin;
            _sheetCost.text = maxed ? Strings.Get("ui.maxed") : NumberFormat.Short(sim.CostOf(_selectedId));
            _sheetCost.color = !maxed && available && !can ? _theme.Danger : _theme.Ink;
            _buy.interactable = can;
            string reason = "";
            if (maxed) reason = Strings.Get("ui.maxed");
            else if (!available) reason = Strings.Format("ui.needs", ("prereqs", string.Join(" / ", Strings.Names(Prereqs(node)))));
            else if (!can)
            {
                // "Not enough coins" leaves the player counting. Say how many are missing.
                double missing = sim.CostOf(_selectedId) - (heritage ? _game.State.Seeds : _game.State.Coins);
                reason = Strings.Format("ui.short_by",
                    ("amount", NumberFormat.Short(System.Math.Max(1d, System.Math.Ceiling(missing)))),
                    ("currency", Strings.Get(heritage ? "ui.seeds" : "ui.coins")));
            }
            _sheetReason.text = reason;
            UiKit.ButtonLabel(_buy).text = maxed ? Strings.Get("ui.maxed") : !available ? Strings.Get("ui.locked") : Strings.Get("ui.buy");
        }

        private TMP_Text _effectNow, _effectNext, _effectArrow, _yearSummary;
        private long _yearSummaryKey = -1;
        // ------------------------------------------------------------------ barn, respec, suggestion (GDD §3.5/§6.3 v2.2)

        private RectTransform _barnStrip;
        private TMP_Text _barnLine;
        private Button _sell, _preserve, _respec;
        private long _barnKey = -1;
        private long _suggestKey = -1;
        private string _suggestedId;

        private void BuildBarnStrip()
        {
            _barnStrip = UiKit.Rect("BarnStrip", _safe);
            UiKit.Stretch(_barnStrip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 176f), new Vector2(0f, 316f));
            _barnLine = UiKit.Label(_barnStrip, "Barn", "", UiType.Caption, _theme.Ink, TextAnchor.MiddleLeft);
            UiKit.Box(_barnLine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(400f, 130f));
            _sell = UiKit.Button(_barnStrip, "SellBarn", "", UiType.Caption, _theme.Accent, _theme.Ink, () => { if (_game.Sim.SellBarn()) { Haptics.Play(HapticKind.Medium); _audio?.Play(SfxId.MarketSale); _barnKey = -1; } });
            UiKit.Box(_sell.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(430f, 0f), new Vector2(200f, 96f));
            _preserve = UiKit.Button(_barnStrip, "Preserves", "", UiType.Caption, _theme.Seed, _theme.Paper, () => { if (_game.Sim.MakePreserves()) { Haptics.Play(HapticKind.Medium); _barnKey = -1; } });
            UiKit.Box(_preserve.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(640f, 0f), new Vector2(210f, 96f));
            _respec = UiKit.Button(_barnStrip, "Respec", "", UiType.Caption, _theme.InkMuted, _theme.Paper, () =>
            {
                if (_game.Sim.RespecAlmanac()) { Haptics.Play(HapticKind.Heavy); _barnKey = -1; _suggestKey = -1; RefreshAll(); }
            });
            UiKit.Box(_respec.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(200f, 96f));
            _barnStrip.gameObject.SetActive(false);
        }

        private void RefreshBarnStrip(FarmState s)
        {
            bool winter = s.Phase == Phase.Winter && !_showingHeritage;
            var barn = s.Barn;
            bool hasBarn = s.Stats.BarnCapacity > 0 || barn.Stock > 0 || barn.Jars > 0;
            bool respec = _game.Sim.CanRespec;
            bool show = winter && (hasBarn || respec);
            if (_barnStrip.gameObject.activeSelf != show) _barnStrip.gameObject.SetActive(show);
            if (!show) { _barnKey = -1; return; }
            long key = (long)System.Math.Min(barn.Stock * 100, 1e15) + barn.Count * 7L + (long)(barn.Jars * 13) + (respec ? 1L << 50 : 0) + (long)(s.Generation.AlmanacSpent * 3);
            if (key == _barnKey) return;
            _barnKey = key;
            string line = "";
            if (hasBarn)
            {
                line = Strings.Format("barn.line", ("count", barn.Count), ("coins", NumberFormat.Short(barn.Stock)));
                line += "\n" + (barn.Jars > 0
                    ? Strings.Format("barn.jars", ("coins", NumberFormat.Short(barn.Jars)))
                    : Strings.Format("barn.price", ("price", barn.MarketPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
            }
            _barnLine.text = line;
            bool stock = barn.Stock > 0;
            _sell.gameObject.SetActive(hasBarn);
            _preserve.gameObject.SetActive(hasBarn);
            _sell.interactable = stock;
            _preserve.interactable = stock;
            UiKit.ButtonLabel(_sell).text = Strings.Format("barn.sell", ("coins", NumberFormat.Short(barn.Stock * barn.MarketPrice)));
            UiKit.ButtonLabel(_preserve).text = Strings.Format("barn.preserve", ("coins", NumberFormat.Short(barn.Stock * _game.Sim.Config.PreserveValue)));
            _respec.gameObject.SetActive(respec);
            UiKit.ButtonLabel(_respec).text = Strings.Format("ui.respec", ("coins", NumberFormat.Short(s.Generation.AlmanacSpent)));
        }

        // ------------------------------------------------------------------ heirs and challenge (GDD §7.4/§7.5 v2.3)

        private RectTransform _heirStrip;
        private readonly Button[] _heirs = new Button[3];
        private Button _challenge;
        private int _heirKey = -1;

        private void BuildHeirStrip()
        {
            _heirStrip = UiKit.Rect("HeirStrip", _safe);
            UiKit.Stretch(_heirStrip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 176f), new Vector2(0f, 316f));
            for (int i = 0; i < _heirs.Length; i++)
            {
                int index = i;
                _heirs[i] = UiKit.Button(_heirStrip, "Heir" + i, "", UiType.Caption, _theme.InkMuted, _theme.Paper, () =>
                {
                    if (_game.Sim.ChooseHeir(index)) Haptics.Play(HapticKind.Selection);
                });
                UiKit.Box(_heirs[i].GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f + i * 250f, 0f), new Vector2(238f, 118f));
                UiKit.ButtonLabel(_heirs[i]).richText = true;
            }
            _challenge = UiKit.Button(_heirStrip, "Challenge", "", UiType.Caption, _theme.InkMuted, _theme.Paper, CycleChallenge);
            UiKit.Box(_challenge.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(270f, 118f));
            UiKit.ButtonLabel(_challenge).enableWordWrapping = true;
            _heirStrip.gameObject.SetActive(false);
        }

        private void CycleChallenge()
        {
            var next = (ChallengeKind)(((int)_game.State.Generation.Challenge + 1) % 3);
            if (_game.Sim.SetChallenge(next)) Haptics.Play(HapticKind.Selection);
        }

        private void RefreshHeirStrip(FarmState s)
        {
            var g = s.Generation;
            bool show = s.Phase == Phase.Heritage && g.HeirOffer[0] != HeirTrait.None;
            if (_heirStrip.gameObject.activeSelf != show) _heirStrip.gameObject.SetActive(show);
            if (!show) { _heirKey = -1; return; }
            int key = (int)g.Trait * 1000 + (int)g.Challenge * 100 + (int)g.HeirOffer[0] * 49 + (int)g.HeirOffer[1] * 7 + (int)g.HeirOffer[2];
            if (key == _heirKey) return;
            _heirKey = key;
            for (int i = 0; i < _heirs.Length; i++)
            {
                var t = g.HeirOffer[i];
                UiKit.ButtonLabel(_heirs[i]).text = "<b>" + Strings.Get("heir." + t) + "</b>\n<size=78%>" + Strings.Get("heir." + t + ".desc") + "</size>";
                _heirs[i].targetGraphic.color = t == g.Trait ? _theme.Accent : _theme.InkMuted;
            }
            UiKit.ButtonLabel(_challenge).text = Strings.Get("challenge." + g.Challenge);
            _challenge.targetGraphic.color = g.Challenge != ChallengeKind.None ? _theme.Seed : _theme.InkMuted;
        }

        private void RefreshSuggestion(FarmState s)
        {
            long key = s.Phase == Phase.Winter ? (long)System.Math.Min(s.Coins, 1e15) * 64 + _game.Sim.Almanac.Levels.Count : -2;
            if (key == _suggestKey) return;
            _suggestKey = key;
            _suggestedId = AlmanacAdvisor.Suggest(_game.Sim);
            _almanac.SetSuggested(_suggestedId);
        }

        private Image[] _gradeStars;
        private TMP_Text _gradeLine;
        private int _gradeKey = -2;
        private Image _gainFill;

        /// <summary>The effect as "now » next" with a bar for the step, so what a level buys is visible at a glance.</summary>
        private void ShowEffect(FarmSim sim, SkillNode node)
        {
            var values = NodeText.Values(sim, node.Id);
            string now = values.TryGetValue("cur", out var c) ? c : "";
            string next = values.TryGetValue("next", out var n) ? n : "";
            bool show = now.Length > 0 && next.Length > 0 && now != next && !sim.IsMaxed(node.Id);
            _effectNow.gameObject.SetActive(show);
            _effectNext.gameObject.SetActive(show);
            _effectArrow.gameObject.SetActive(show);
            _gainFill.transform.parent.gameObject.SetActive(show);
            if (!show) return;
            _effectNow.text = now;
            _effectNext.text = next;
            float ratio = 0.35f;
            if (TryNumber(now, out float a) && TryNumber(next, out float b) && b > 0f) ratio = Mathf.Clamp(a / b, 0.05f, 0.95f);
            _gainFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        }

        /// <summary>
        /// Reads the leading number out of a formatted effect value ("1.2 plots", "%15", "1,050", "1.234", "2.5K"), in
        /// either language. A separator followed by exactly three digits is a thousands separator; any other is the
        /// decimal point (effect values carry at most two decimals). A K/M/B suffix scales it.
        /// </summary>
        private static bool TryNumber(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(text)) return false;
            double whole = 0, frac = 0, scale = 1;
            bool any = false, inFrac = false;
            int i = 0;
            while (i < text.Length && !char.IsDigit(text[i])) i++;
            for (; i < text.Length; i++)
            {
                char ch = text[i];
                if (char.IsDigit(ch))
                {
                    any = true;
                    if (inFrac) { scale /= 10; frac += (ch - '0') * scale; }
                    else whole = whole * 10 + (ch - '0');
                    continue;
                }
                if ((ch == '.' || ch == ',') && !inFrac)
                {
                    int run = 0;
                    while (i + 1 + run < text.Length && char.IsDigit(text[i + 1 + run])) run++;
                    if (run == 3) continue;  // thousands
                    if (run == 0) break;
                    inFrac = true;
                    continue;
                }
                if (ch == 'K' || ch == 'k') { whole *= 1e3; frac *= 1e3; }
                else if (ch == 'M') { whole *= 1e6; frac *= 1e6; }
                else if (ch == 'B') { whole *= 1e9; frac *= 1e9; }
                break;
            }
            value = (float)(whole + frac);
            return any;
        }

        private List<SkillNode> Prereqs(SkillNode node)
        {
            var list = new List<SkillNode>();
            foreach (var p in node.Prerequisites)
            {
                var n = _game.Sim.GetNode(p);
                if (n != null) list.Add(n);
            }
            return list;
        }

        private void LateUpdate()
        {
            if (_closing)
            {
                _open = Mathf.Max(0f, _open - Time.unscaledDeltaTime / UiMotion.Slow);
                _group.alpha = Prims.EaseOutQuad(_open);
                if (_open <= 0f)
                {
                    _closing = false;
                    _root.SetActive(false);
                }
                return;
            }
            if (!_visible) return;
            if (_card.IsOpen) { _group.alpha = 0f; return; }
            float dt = Time.unscaledDeltaTime;
            TickSnow(dt);
            TickSky(dt);
            _open = Mathf.Min(1f, _open + dt / UiMotion.Slow);
            _group.alpha = Prims.EaseOutQuad(_open);

            var s = _game.State;
            // Coin counter drains with ticks toward the real value.
            double target = _showingHeritage ? s.Seeds : s.Coins;
            if (System.Math.Abs(_coinsShown - target) > 0.5)
            {
                double step = System.Math.Max(1, System.Math.Abs(_coinsShown - target) * 8 * dt);
                _coinsShown += _coinsShown < target ? step : -step;
                if (System.Math.Abs(_coinsShown - target) <= step) _coinsShown = target;
                _coinTick += dt;
                if (_coinTick > 0.06f) { _coinTick = 0f; _audio.Play(SfxId.CoinArrive, 0.5f); }
            }
            else _coinsShown = target;
            if (_coinsShown != _coinsTextValue || _showingHeritage != _coinsTextHeritage)
            {
                _coinsTextValue = _coinsShown;
                _coinsTextHeritage = _showingHeritage;
                int n = NumberFormat.Short(_coinsShown, _coinChars);
                var rich = _showingHeritage ? _seedRich : _coinRich;
                n = rich.Write(_coinChars, n);
                var buffer = rich.Buffer;
                string unit = Strings.Get(_showingHeritage ? "ui.seeds" : "ui.coins");
                buffer[n++] = ' ';
                for (int i = 0; i < unit.Length && n < buffer.Length; i++) buffer[n++] = unit[i];
                _coins.SetText(buffer, 0, n);
            }
            _coinPunch = Mathf.Max(0f, _coinPunch - dt * 5f);
            _coinsRt.localScale = Vector3.one * (1f + 0.15f * Prims.EaseOutQuad(_coinPunch));

            bool gh = s.Phase == Phase.Winter && s.Stats.GreenhouseLevel > 0;
            long ghCoins = gh ? (long)s.Greenhouse.CoinsThisWinter : -1;
            int ghSeconds = gh ? Mathf.CeilToInt(s.Greenhouse.SecondsLeftThisWinter) : -1;
            if (ghCoins != _ghCoinsKey || ghSeconds != _ghSecondsKey)
            {
                _ghCoinsKey = ghCoins;
                _ghSecondsKey = ghSeconds;
                _greenhouse.text = gh ? Strings.Format("ui.greenhouse", ("coins", NumberFormat.Short(s.Greenhouse.CoinsThisWinter)), ("seconds", ghSeconds)) : "";
            }
            int gradeKey = s.Phase == Phase.Winter ? s.LastGrade * 10 + (s.Goal.Active ? (s.Goal.Done ? 2 : 1) : 0) : -1;
            if (gradeKey != _gradeKey)
            {
                _gradeKey = gradeKey;
                bool graded = gradeKey >= 10;
                for (int i = 0; i < 3; i++)
                {
                    _gradeStars[i].gameObject.SetActive(graded);
                    _gradeStars[i].color = i < s.LastGrade ? _theme.Accent : _theme.InkMuted * new Color(1f, 1f, 1f, 0.35f);
                }
                string line = graded && s.LastGradeBonus > 0 ? Strings.Format("ui.grade_bonus", ("coins", NumberFormat.Short(s.LastGradeBonus))) : "";
                if (graded && s.Goal.Active)
                    line += (line.Length > 0 ? "  ·  " : "") + Strings.Get(s.Goal.Done ? "ui.goal_result_met" : "ui.goal_result_missed");
                _gradeLine.text = graded ? line : "";
            }
            RefreshBarnStrip(s);
            RefreshHeirStrip(s);
            RefreshSuggestion(s);
            // What the year that just ended brought in (Core counts it from Spring; v5 save).
            long summaryKey = (long)s.CoinsThisYear * 1000 + s.HarvestsThisYear;
            if (summaryKey != _yearSummaryKey)
            {
                _yearSummaryKey = summaryKey;
                _yearSummary.text = s.Phase == Phase.Winter
                    ? Strings.Format("ui.year_summary", ("harvests", s.HarvestsThisYear), ("coins", NumberFormat.Short(s.CoinsThisYear)))
                    : "";
            }
            int retireSeeds = s.Phase == Phase.Winter && _game.Sim.CanRetire ? _game.Sim.SeedsIfRetiredNow : -1;
            // The same line is empty in the Heritage phase, so there it says how far the tree is from the ending:
            // "finish Heritage" is the whole goal and nothing else counted it out loud.
            int heritageDone = s.Phase == Phase.Heritage ? _game.Sim.HeritageDone : -1;
            // Nothing affordable is its own state: the tree says "locked" everywhere and never says what to do about it.
            bool broke = s.Phase == Phase.Winter && retireSeeds < 0 && _suggestedId == null;
            int hintKey = ((retireSeeds + 2) * 1000 + heritageDone + 2) * 2 + (broke ? 1 : 0);
            if (hintKey != _retireSeedsKey)
            {
                _retireSeedsKey = hintKey;
                _retireHint.text = retireSeeds >= 0
                    ? Strings.Format("ui.retire_now", ("seeds", retireSeeds))
                    : heritageDone >= 0
                        ? Strings.Format("heritage.progress", ("done", heritageDone), ("total", _game.Sim.HeritageTotal))
                        : broke
                            ? Strings.Get("ui.nothing_affordable")
                            : "";
            }

            // Bottom sheet slide.
            bool want = _selectedId != null && _sheet.gameObject.activeSelf;
            _sheetShown = Prims.Damp(_sheetShown, want ? 1f : 0f, 12f, dt);
            _sheet.anchoredPosition = _sheetHome + new Vector2(0f, Mathf.Lerp(-420f, 0f, Prims.EaseOutQuad(_sheetShown)));
        }
    }
}
