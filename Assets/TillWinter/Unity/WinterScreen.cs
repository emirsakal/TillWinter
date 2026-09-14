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
        private TreeTheme _theme;
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

            var overlay = UiKit.Panel(canvas, "WinterScreen", _theme.Overlay, false, true);
            _root = overlay.gameObject;
            _group = _root.AddComponent<CanvasGroup>();
            _safe = UiKit.Rect("SafeArea", overlay.transform);
            ApplySafeArea();

            // Paper page with a soft vignette.
            var page = UiKit.Panel(_safe, "Page", _theme.Paper, true, false);
            UiKit.Stretch(page.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -20f));
            var vignette = UiKit.Panel(page.transform, "Vignette", _theme.PaperVignette, true, false);
            vignette.sprite = Prims.CircleSprite(256);
            vignette.type = Image.Type.Simple;
            vignette.color = new Color(_theme.PaperVignette.r, _theme.PaperVignette.g, _theme.PaperVignette.b, 0f);
            UiKit.Stretch(vignette.rectTransform, Vector2.zero, Vector2.one, new Vector2(-400f, -700f), new Vector2(400f, 700f));
            var edge = UiKit.Panel(page.transform, "Edge", new Color(0f, 0f, 0f, 0f), true, false);
            edge.color = new Color(_theme.PaperVignette.r, _theme.PaperVignette.g, _theme.PaperVignette.b, 0.18f);
            UiKit.Stretch(edge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var inner = UiKit.Panel(page.transform, "Inner", _theme.Paper, true, false);
            UiKit.Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));

            // Top bar.
            var top = UiKit.Rect("TopBar", _safe);
            UiKit.Stretch(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -220f), new Vector2(-40f, -30f));
            _title = UiKit.Label(top, "Title", "", 56, _theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.7f, 1f), Vector2.zero, Vector2.zero);
            _coins = UiKit.Label(top, "Coins", "0", 52, _theme.Ink, TextAnchor.UpperRight, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            UiKit.Stretch(_coinsRt, new Vector2(0.55f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _greenhouse = UiKit.Label(top, "Greenhouse", "", 26, _theme.InkMuted, TextAnchor.LowerLeft);
            UiKit.Stretch(_greenhouse.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 0.5f), Vector2.zero, Vector2.zero);
            _retireHint = UiKit.Label(top, "RetireHint", "", 26, _theme.Seed, TextAnchor.LowerRight, FontStyle.Bold);
            UiKit.Stretch(_retireHint.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            _treeToggle = UiKit.Button(top, "TreeToggle", Strings.Get("ui.heritage"), 24, _theme.Seed, _theme.Paper, ToggleTree);
            UiKit.Box(_treeToggle.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -8f), new Vector2(220f, 56f));

            // Tree canvases.
            var almanacRt = UiKit.Rect("AlmanacTree", _safe);
            UiKit.Stretch(almanacRt, Vector2.zero, Vector2.one, new Vector2(34f, 330f), new Vector2(-34f, -240f));
            _almanac = almanacRt.gameObject.AddComponent<SkillTreeView>();
            _almanac.Init(game, game.Sim.Almanac, _theme, SkillTreeLayout.Compute(game.Sim.Almanac.Nodes));
            _almanac.Selected += OnSelected;
            var heritageRt = UiKit.Rect("HeritageTree", _safe);
            UiKit.Stretch(heritageRt, Vector2.zero, Vector2.one, new Vector2(34f, 330f), new Vector2(-34f, -240f));
            _heritage = heritageRt.gameObject.AddComponent<SkillTreeView>();
            _heritage.Init(game, game.Sim.Heritage, _theme, SkillTreeLayout.Compute(game.Sim.Heritage.Nodes));
            _heritage.Selected += OnSelected;

            BuildSheet();

            // Bottom buttons.
            _nextYear = UiKit.Button(_safe, "NextYear", Strings.Get("ui.next_year") + "  ▶", 40, _theme.Accent, _theme.Ink, OnNextYear);
            UiKit.Box(_nextYear.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 40f), new Vector2(490f, 120f));
            _retire = UiKit.Button(_safe, "Retire", Strings.Get("ui.pass_on"), 30, _theme.Seed, _theme.Paper, OnRetirePressed);
            UiKit.Box(_retire.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 40f), new Vector2(490f, 120f));
            _startGen = UiKit.Button(_safe, "StartGeneration", Strings.Get("ui.start_generation") + "  ▶", 40, _theme.Accent, _theme.Ink, OnStartGeneration);
            UiKit.Box(_startGen.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 120f));

            BuildConfirm();

            _root.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += OnPurchased;
            _game.Sim.Retired += _ => { _showingHeritage = true; RefreshAll(); };
        }

        private void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            var min = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            var max = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            if (float.IsNaN(min.x) || Screen.width == 0) { min = Vector2.zero; max = Vector2.one; }
            UiKit.Stretch(_safe, min, max, Vector2.zero, Vector2.zero);
        }

        private void BuildSheet()
        {
            _sheet = UiKit.Rect("Sheet", _safe);
            UiKit.Stretch(_sheet, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 180f), new Vector2(-34f, 560f));
            var bg = UiKit.Panel(_sheet, "Bg", _theme.Paper, true, true);
            var shade = UiKit.Panel(_sheet, "Shade", new Color(0f, 0f, 0f, 0.06f), true, false);
            UiKit.Stretch(shade.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -6f), new Vector2(0f, -6f));
            shade.transform.SetAsFirstSibling();

            _sheetTag = UiKit.Panel(_sheet, "Tag", _theme.Hand, true, false);
            UiKit.Box(_sheetTag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(190f, 44f));
            _sheetBranch = UiKit.Label(_sheetTag.transform, "BranchName", "", 24, _theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            _sheetName = UiKit.Label(_sheet, "Name", "", 42, _theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(_sheetName.rectTransform, new Vector2(0f, 1f), new Vector2(0.66f, 1f), new Vector2(230f, -70f), new Vector2(0f, -14f));
            _sheetLevel = UiKit.Label(_sheet, "Level", "", 26, _theme.InkMuted, TextAnchor.UpperRight);
            UiKit.Stretch(_sheetLevel.rectTransform, new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-24f, -18f));
            _sheetDesc = UiKit.Label(_sheet, "Desc", "", 30, _theme.Ink, TextAnchor.UpperLeft);
            UiKit.Stretch(_sheetDesc.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(24f, 24f), new Vector2(0f, -84f));

            _buy = UiKit.Button(_sheet, "Buy", Strings.Get("ui.buy"), 34, _theme.Accent, _theme.Ink, OnBuy);
            UiKit.Box(_buy.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(330f, 96f));
            _sheetCoin = UiKit.CircleImage(_sheet, "Currency", _theme.Coin, Vector2.zero, 36f);
            UiKit.Box(_sheetCoin.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 150f), new Vector2(36f, 36f));
            _sheetCost = UiKit.Label(_sheet, "Cost", "", 34, _theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Box(_sheetCost.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 150f), new Vector2(280f, 44f));
            _sheetReason = UiKit.Label(_sheet, "Reason", "", 22, _theme.Danger, TextAnchor.MiddleRight);
            UiKit.Box(_sheetReason.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 120f), new Vector2(330f, 30f));
            _sheet.gameObject.SetActive(false);
        }

        private void BuildConfirm()
        {
            var dim = UiKit.Panel(_safe, "ConfirmDim", new Color(0f, 0f, 0f, 0.6f), false, true);
            _confirm = dim.gameObject;
            var box = UiKit.Panel(dim.transform, "ConfirmBox", _theme.Paper, true, true);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            var title = UiKit.Label(box.transform, "Title", Strings.Get("ui.confirm_title"), 48, _theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(860f, 70f));
            _confirmText = UiKit.Label(box.transform, "Text", "", 30, _theme.Ink, TextAnchor.UpperLeft);
            UiKit.Stretch(_confirmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 170f), new Vector2(-50f, -120f));
            var yes = UiKit.Button(box.transform, "Yes", Strings.Get("ui.retire"), 34, _theme.Seed, _theme.Paper, OnRetireConfirmed);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 40f), new Vector2(380f, 110f));
            var no = UiKit.Button(box.transform, "No", Strings.Get("ui.cancel"), 34, _theme.InkMuted, _theme.Paper, () => { _audio.Play(SfxId.UiClick); _confirm.SetActive(false); });
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 40f), new Vector2(380f, 110f));
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
            _visible = true;
            _open = 0f;
            _root.SetActive(true);
            _game.InputBlocked = true;
            _showingHeritage = _game.State.Phase == Phase.Heritage;
            _selectedId = null;
            _sheetShown = 0f;
            _coinsShown = _game.State.Coins;
            ApplySafeArea();
            RefreshAll();
            ActiveView.OnOpened();
        }

        private void Close()
        {
            ActiveView.OnClosed();
            _visible = false;
            _root.SetActive(false);
            _confirm.SetActive(false);
            _game.InputBlocked = false;
        }

        private SkillTreeView ActiveView => _showingHeritage ? _heritage : _almanac;

        private void ToggleTree()
        {
            if (_game.State.Phase == Phase.Heritage) return;
            _audio.Play(SfxId.UiClick);
            ActiveView.OnClosed();
            _showingHeritage = !_showingHeritage;
            _selectedId = null;
            RefreshAll();
            ActiveView.OnOpened();
        }

        private void OnSelected(string id)
        {
            _audio.Play(SfxId.UiClick);
            _selectedId = id;
            RefreshSheet();
        }

        private void OnNextYear()
        {
            _audio.Play(SfxId.UiClick);
            _game.Sim.StartNextYear();
            Close();
        }

        private void OnStartGeneration()
        {
            _audio.Play(SfxId.UiClick);
            _game.Sim.StartNewGeneration();
            Close();
        }

        private void OnRetirePressed()
        {
            _audio.Play(SfxId.UiClick);
            if (!_game.Sim.CanRetire) return;
            var s = _game.State;
            _confirmText.text = Strings.Format("ui.confirm_body",
                ("seeds", _game.Sim.SeedsIfRetiredNow), ("gen", s.Generation.Generation + 1),
                ("coins", NumberFormat.Short(s.Coins)), ("field", s.GridSize + "x" + s.GridSize));
            _confirm.SetActive(true);
        }

        private void OnRetireConfirmed()
        {
            _confirm.SetActive(false);
            if (_game.Sim.Retire())
            {
                _audio.Play(SfxId.WinterChime);
                _showingHeritage = true;
                _selectedId = null;
                ActiveView.OnOpened();
            }
            RefreshAll();
        }

        private void OnBuy()
        {
            if (_selectedId == null) return;
            if (_game.Sim.TryBuy(_selectedId))
            {
                _audio.Play(SfxId.Purchase);
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
            _title.text = heritagePhase
                ? Strings.Format("ui.heritage_title", ("gen", s.Generation.Generation))
                : Strings.Format("ui.winter_title", ("year", s.Year)) + "  ·  " + Strings.Get("ui.heritage").ToLowerInvariant() + " " + s.Generation.Generation;
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
                UiKit.ButtonLabel(_retire).fontSize = 26;
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
            _sheetBranch.text = Strings.Branch(node.Branch);
            _sheetName.text = Strings.Name(node);
            _sheetDesc.text = Strings.Description(sim, node);
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
            else if (!can) reason = Strings.Format("ui.not_enough", ("currency", Strings.Get(heritage ? "ui.seeds" : "ui.coins")));
            _sheetReason.text = reason;
            UiKit.ButtonLabel(_buy).text = maxed ? Strings.Get("ui.maxed") : !available ? Strings.Get("ui.locked") : Strings.Get("ui.buy");
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
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;
            _open = Mathf.Min(1f, _open + dt / 0.3f);
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
            _coins.text = NumberFormat.Short(_coinsShown) + " " + Strings.Get(_showingHeritage ? "ui.seeds" : "ui.coins");
            _coinPunch = Mathf.Max(0f, _coinPunch - dt * 5f);
            _coinsRt.localScale = Vector3.one * (1f + 0.15f * Prims.EaseOutQuad(_coinPunch));

            bool gh = s.Phase == Phase.Winter && s.Stats.GreenhouseLevel > 0;
            _greenhouse.text = gh ? Strings.Format("ui.greenhouse", ("coins", NumberFormat.Short(s.Greenhouse.CoinsThisWinter)), ("seconds", Mathf.CeilToInt(s.Greenhouse.SecondsLeftThisWinter))) : "";
            _retireHint.text = s.Phase == Phase.Winter && _game.Sim.CanRetire ? Strings.Format("ui.retire_now", ("seeds", _game.Sim.SeedsIfRetiredNow)) : "";

            // Bottom sheet slide.
            bool want = _selectedId != null && _sheet.gameObject.activeSelf;
            _sheetShown = Prims.Damp(_sheetShown, want ? 1f : 0f, 12f, dt);
            _sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(-420f, 0f, Prims.EaseOutQuad(_sheetShown)));
        }
    }
}
