using System.Collections.Generic;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The Winter screen (GDD §10.2, v3.4): the Almanac as a field seen from above on a winter-day page. One line
    /// on top, the field, a line or two under it, two text links and one button; a card for the selected bed.
    /// Shown whenever the phase is not Year. In the Heritage phase (and via the Heritage link in Winter) the same
    /// canvas shows the Heritage field at dusk.
    /// </summary>
    public sealed class WinterScreen : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private TreeTheme _theme, _heritageTheme;
        private GenerationCard _card;
        private Image _overlay;
        private TMP_Text _hintCaption;
        private GameObject _retireSheet;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _safe;
        private TMP_Text _title, _coins, _tapHint, _yearSummary, _infoLine;
        private RectTransform _coinsRt;
        private Button _treeToggle, _retire, _heirLink;
        private SkillTreeView _almanac, _heritage;
        private RectTransform _sheet;
        private TMP_Text _sheetName, _sheetDesc, _sheetLevel, _sheetNote;
        private Image[] _pips;
        private Button _buy;
        private Button _nextYear, _startGen;
        private GameObject _confirm;
        private TMP_Text _confirmText, _confirmLose;

        private bool _visible;
        private bool _showingHeritage;
        private float _open;
        private float _sheetShown;
        private bool _closing;
        private RichNumber _coinRich, _seedRich;
        private double _coinsTextValue = -1;
        private readonly char[] _coinChars = new char[64];
        private bool _coinsTextHeritage;
        private long _infoKey = -2;
        private int _hintKey = -2;
        private Vector2 _sheetHome;
        private double _coinsShown;
        private float _coinPunch;
        private float _coinTick;
        private string _selectedId;
        private const int MaxPips = 12;

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

            // Top line: where we are on the left, what we have on the right.
            var top = UiKit.Rect("TopBar", _safe);
            UiKit.Stretch(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, -130f), new Vector2(-44f, -40f));
            _title = UiKit.Label(top, "Title", "", UiType.Body, _theme.InkMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_title.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            _title.enableWordWrapping = false;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 22f;
            _title.fontSizeMax = _title.fontSize;
            _coins = UiKit.Label(top, "Coins", "0", UiType.Heading, _theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            // Ends short of the pause button, which sits in the top-right corner of every screen.
            UiKit.Stretch(_coinsRt, new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-120f, 0f));
            _coins.richText = true;
            _coins.enableWordWrapping = false;
            _coins.enableAutoSizing = true;
            _coins.fontSizeMin = 24f;
            _coins.fontSizeMax = _coins.fontSize;
            _coinRich = new RichNumber(0.62f, _theme.InkMuted);
            _seedRich = new RichNumber(0.62f, _heritageTheme.InkMuted);

            // The field: fitted whole to this canvas, no panning.
            var almanacRt = UiKit.Rect("AlmanacTree", _safe);
            UiKit.Stretch(almanacRt, Vector2.zero, Vector2.one, new Vector2(34f, 660f), new Vector2(-34f, -270f));
            _almanac = almanacRt.gameObject.AddComponent<SkillTreeView>();
            _almanac.Init(game, game.Sim.Almanac, _theme, SkillTreeLayout.Compute(game.Sim.Almanac.Nodes));
            _almanac.Selected += OnSelected;
            var heritageRt = UiKit.Rect("HeritageTree", _safe);
            UiKit.Stretch(heritageRt, Vector2.zero, Vector2.one, new Vector2(34f, 660f), new Vector2(-34f, -270f));
            _heritage = heritageRt.gameObject.AddComponent<SkillTreeView>();
            _heritage.Init(game, game.Sim.Heritage, _heritageTheme, SkillTreeLayout.Compute(game.Sim.Heritage.Nodes));
            _heritage.Selected += OnSelected;

            // Under the field: what to do, what the year brought, what is running.
            _tapHint = UiKit.Label(_safe, "TapHint", Strings.Get("ui.tap_a_bed"), UiType.Caption, _theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Stretch(_tapHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(60f, 648f), new Vector2(-60f, 696f));
            _yearSummary = UiKit.Label(_safe, "YearSummary", "", UiType.Body, _theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_yearSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(60f, 588f), new Vector2(-60f, 640f));
            _yearSummary.enableWordWrapping = false;
            _yearSummary.enableAutoSizing = true;
            _yearSummary.fontSizeMin = 20f;
            _yearSummary.fontSizeMax = _yearSummary.fontSize;
            _infoLine = UiKit.Label(_safe, "Info", "", UiType.Caption, _theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Stretch(_infoLine.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(60f, 544f), new Vector2(-60f, 586f));
            _infoLine.enableWordWrapping = false;
            _infoLine.enableAutoSizing = true;
            _infoLine.fontSizeMin = 18f;
            _infoLine.fontSizeMax = _infoLine.fontSize;

            BuildBarnStrip();

            // Two text links and one button.
            var links = UiKit.Rect("Links", _safe);
            UiKit.Stretch(links, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(44f, 300f), new Vector2(-44f, 364f));
            _treeToggle = TextLink(links, "TreeToggle", Strings.Get("ui.heritage") + "  »", _theme.Link, TextAnchor.MiddleLeft, ToggleTree);
            UiKit.Stretch(_treeToggle.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.45f, 1f), Vector2.zero, Vector2.zero);
            _heirLink = TextLink(links, "HeirLink", "", _theme.Link, TextAnchor.MiddleLeft, () => _card.ShowHeirs(OnCardClosed));
            UiKit.Stretch(_heirLink.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            _retire = TextLink(links, "Retire", "", _theme.Seed, TextAnchor.MiddleRight, OnRetirePressed);
            UiKit.Stretch(_retire.GetComponent<RectTransform>(), new Vector2(0.45f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _nextYear = UiKit.Button(_safe, "NextYear", Strings.Get("ui.next_year") + "  »", UiType.Heading, _theme.Accent, _theme.Ink, OnNextYear);
            UiKit.Box(_nextYear.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(992f, 150f));
            _startGen = UiKit.Button(_safe, "StartGeneration", Strings.Get("ui.start_generation") + "  »", UiType.Heading, _theme.Accent, _theme.Ink, OnStartGeneration);
            UiKit.Box(_startGen.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(992f, 150f));

            BuildSheet();
            BuildConfirm();

            // A one-line caption over the top of the field for the first-winter and first-heritage hints.
            var cap = UiKit.Panel(_safe, "HintCaption", _theme.Dim, true, false);
            UiKit.Box(cap.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(960f, 76f));
            _hintCaption = UiKit.Label(cap.transform, "Text", "", UiType.Label, _theme.Card, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_hintCaption.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            cap.gameObject.SetActive(false);
            BuildRetireSheet();

            _card = canvas.gameObject.AddComponent<GenerationCard>();
            _card.Init(game, audio, canvas);

            _root.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += OnPurchased;
            _game.Sim.Retired += OnRetired;
        }

        /// <summary>A line of text that is a button: the links under the field.</summary>
        private static Button TextLink(Transform parent, string name, string text, Color color, TextAnchor anchor, UnityEngine.Events.UnityAction onClick)
        {
            var hit = UiKit.Panel(parent, name, new Color(0f, 0f, 0f, 0.001f), false, true);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            var label = UiKit.Label(hit.transform, "Label", text, UiType.Body, color, anchor, FontStyle.Bold);
            UiKit.Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = label.fontSize;
            hit.gameObject.AddComponent<ButtonFeedback>();
            btn.onClick.AddListener(onClick);
            return btn;
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

        /// <summary>The card for the selected bed: name, one line of effect, the level as pips, a note, and the price inside the button.</summary>
        private void BuildSheet()
        {
            _sheet = UiKit.Rect("Sheet", _safe);
            UiKit.Stretch(_sheet, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 222f), new Vector2(-34f, 658f));
            UiKit.Card(_sheet, "Bg", _theme.Card);

            _sheetName = UiKit.Label(_sheet, "Name", "", UiType.Title, _theme.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(_sheetName.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(40f, -96f), new Vector2(0f, -24f));
            _sheetName.enableWordWrapping = false;
            _sheetName.enableAutoSizing = true;
            _sheetName.fontSizeMin = 26f;
            _sheetName.fontSizeMax = _sheetName.fontSize;
            _sheetLevel = UiKit.Label(_sheet, "Level", "", UiType.Caption, _theme.InkMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch(_sheetLevel.rectTransform, new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(-40f, -24f));
            _sheetDesc = UiKit.Label(_sheet, "Desc", "", UiType.Body, _theme.Ink, TextAnchor.UpperLeft);
            UiKit.Stretch(_sheetDesc.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -196f), new Vector2(-40f, -100f));
            _pips = new Image[MaxPips];
            for (int i = 0; i < MaxPips; i++)
            {
                _pips[i] = UiKit.Panel(_sheet, "Pip" + i, _theme.BedGrown, true, false);
                UiKit.Box(_pips[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f + i * 48f, -216f), new Vector2(40f, 12f));
            }
            _sheetNote = UiKit.Label(_sheet, "Note", "", UiType.Caption, _theme.InkMuted, TextAnchor.MiddleLeft);
            UiKit.Stretch(_sheetNote.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -280f), new Vector2(-40f, -234f));
            _sheetNote.enableWordWrapping = false;
            _sheetNote.overflowMode = TextOverflowModes.Ellipsis;

            _buy = UiKit.Button(_sheet, "Buy", Strings.Get("ui.buy"), UiType.Body, _theme.Accent, _theme.Ink, OnBuy);
            UiKit.Stretch(_buy.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 28f), new Vector2(-40f, 138f));
            _sheetHome = _sheet.anchoredPosition;
            _sheet.gameObject.SetActive(false);
        }

        /// <summary>One-time sheet at the first Winter with CanRetire (GDD §10.5).</summary>
        private void BuildRetireSheet()
        {
            var dim = UiKit.Panel(_safe, "RetireHintDim", _theme.DimLight, false, true);
            _retireSheet = dim.gameObject;
            var box = UiKit.Card(dim.transform, "Box", _theme.Card);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 420f));
            var text = UiKit.Label(box.transform, "Text", Strings.Get("hint.first_can_retire"), UiType.Body, _theme.Ink, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 150f), new Vector2(-50f, -40f));
            var ok = UiKit.Button(box.transform, "Ok", Strings.Get("hint.got_it"), UiType.Body, _theme.Seed, _theme.Card, () => { _retireSheet.SetActive(false); });
            UiKit.Box(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 96f));
            _retireSheet.AddComponent<SheetTransition>().Page = box.rectTransform;
            _retireSheet.SetActive(false);
        }

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
            var box = UiKit.Card(dim.transform, "ConfirmBox", _theme.Card);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f));
            var title = UiKit.Label(box.transform, "Title", Strings.Get("ui.confirm_title"), UiType.Title, _theme.Ink, TextAnchor.MiddleCenter);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 70f));
            _confirmText = UiKit.Label(box.transform, "Text", "", UiType.Label, _theme.Ink, TextAnchor.UpperCenter);
            UiKit.Box(_confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(800f, 70f));
            // What stays and what goes, side by side, each under its own mark.
            _confirmLose = ConfirmColumn(box.rectTransform, -1f, "ui.confirm_lose_title", "warning", _theme.Danger);
            ConfirmColumn(box.rectTransform, 1f, "ui.confirm_keep_title", "checkmark", _theme.BedGrown).text = Strings.Get("ui.confirm_keep");
            var yes = UiKit.Button(box.transform, "Yes", Strings.Get("ui.retire"), UiType.Body, _theme.Seed, _theme.Card, OnRetireConfirmed);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 40f), new Vector2(380f, 110f));
            var no = UiKit.Button(box.transform, "No", Strings.Get("ui.cancel"), UiType.Body, _theme.Paper, _theme.Ink, () => { _confirm.SetActive(false); });
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 40f), new Vector2(380f, 110f));
            _confirm.AddComponent<SheetTransition>().Page = box.rectTransform;
            _confirm.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.WinterStarted -= Open;
            _game.Sim.Purchased -= OnPurchased;
            _game.Sim.Retired -= OnRetired;
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            if (_game.State.IsDaily) return; // the daily farm ends on its own card
            _visible = true;
            _closing = false;
            _group.blocksRaycasts = true;
            _open = 0f;
            _root.SetActive(true);
            _game.InputBlocked = true;
            PauseMenu.Instance?.PlaceButton(true);
            _showingHeritage = _game.State.Phase == Phase.Heritage;
            _selectedId = null;
            _almanac.Deselect();
            _heritage.Deselect();
            _sheetShown = 0f;
            _coinsShown = _game.State.Coins;
            _coinsTextValue = -1;
            _infoKey = -2;
            _hintKey = -2;
            ApplySafeArea();
            RefreshAll();
            ActiveView.OnOpened();

            var sim = _game.Sim;
            if (_game.State.Phase == Phase.Winter && sim.MarkHint(Hint.FirstWinter))
            {
                _almanac.Highlight("hoe_damage", "growth");
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
            _game.InputBlocked = false;
            PauseMenu.Instance?.PlaceButton(false);
        }

        /// <summary>Android back: closes the topmost sheet (confirm, retire sheet, node sheet), otherwise does nothing (never quits).</summary>
        public bool HandleBack()
        {
            if (!_visible) return false;
            if (_confirm.activeSelf) { _confirm.SetActive(false); return true; }
            if (_retireSheet.activeSelf) { _retireSheet.SetActive(false); return true; }
            if (_selectedId != null) { _selectedId = null; ActiveView.Deselect(); RefreshSheet(); return true; }
            return false;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; // the Android back button arrives as Escape
            // While the pause menu is up it owns the back button (its sheets close first).
            if (_visible && !_game.Paused && kb != null && kb.escapeKey.wasPressedThisFrame) HandleBack();
        }

        private SkillTreeView ActiveView => _showingHeritage ? _heritage : _almanac;
        private TreeTheme ActiveTheme => _showingHeritage ? _heritageTheme : _theme;

        private void ToggleTree()
        {
            if (_game.State.Phase == Phase.Heritage) return;
            ActiveView.OnClosed();
            _showingHeritage = !_showingHeritage;
            _selectedId = null;
            _almanac.Deselect();
            _heritage.Deselect();
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
            if (_game.Sim.Retire())
            {
                _audio.Duck(1f, -6f);
                _audio.Play(SfxId.RetireSwell);
                Haptics.Play(HapticKind.Heavy);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.06f, 0.35f);
                VfxPlayer.Fire(VfxId.RetireSnow, Vector3.zero);
            }
        }

        /// <summary>The farm was handed on (by the button or by a debug hook): the album page, then the Heritage field.</summary>
        private void OnRetired(RetireEvent e)
        {
            _showingHeritage = true;
            _selectedId = null;
            _almanac.Deselect();
            _heritage.Deselect();
            _retireSheet.SetActive(false);
            _confirm.SetActive(false);
            ShowCaption("", false);
            if (!_visible) Open();
            _group.alpha = 0f;
            var sim = _game.Sim;
            _card.Show(e, () =>
            {
                OnCardClosed();
                if (sim.MarkHint(Hint.FirstHeritage)) ShowCaption("hint.first_heritage", true);
            });
            RefreshAll();
        }

        /// <summary>Back from the album page: the Heritage field fades in.</summary>
        private void OnCardClosed()
        {
            _open = 0f;
            _game.InputBlocked = true;
            RefreshAll();
            ActiveView.OnOpened();
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
            var t = ActiveTheme;
            _overlay.color = t.Overlay;
            _title.color = t.InkMuted;
            _coins.color = t.Ink;
            _tapHint.color = t.InkMuted;
            _yearSummary.color = t.Ink;
            _infoLine.color = t.InkMuted;
            _title.text = heritagePhase
                ? Strings.Format("ui.heritage_title", ("gen", s.Generation.Generation)) + (_game.Sim.HeritageComplete ? "  ·  " + Strings.Get("heritage.complete") : "")
                : Strings.Format("ui.winter_title", ("year", s.Year)) + "  ·  " + Strings.Format("gen.title", ("gen", s.Generation.Generation));
            _almanac.gameObject.SetActive(!_showingHeritage);
            _heritage.gameObject.SetActive(_showingHeritage);
            _almanac.Refresh(false);
            _heritage.Refresh(false);
            _treeToggle.gameObject.SetActive(!heritagePhase);
            UiKit.ButtonLabel(_treeToggle).text = _showingHeritage ? "«  " + Strings.Get("ui.almanac") : Strings.Get("ui.heritage") + "  »";
            UiKit.ButtonLabel(_treeToggle).color = t.Link;
            _heirLink.gameObject.SetActive(heritagePhase && s.Generation.HeirOffer[0] != HeirTrait.None);
            if (_heirLink.gameObject.activeSelf)
            {
                UiKit.ButtonLabel(_heirLink).text = Strings.Format("ui.heir_link", ("heir", Strings.Get("heir." + s.Generation.Trait)));
                UiKit.ButtonLabel(_heirLink).color = t.Link;
            }
            _nextYear.gameObject.SetActive(!heritagePhase);
            _retire.gameObject.SetActive(!heritagePhase);
            _startGen.gameObject.SetActive(heritagePhase);
            if (!heritagePhase)
            {
                bool can = _game.Sim.CanRetire;
                _retire.interactable = can;
                var label = UiKit.ButtonLabel(_retire);
                label.text = can
                    ? Strings.Get("ui.pass_on") + "  ·  " + Strings.Format("ui.pass_on_seeds", ("seeds", _game.Sim.SeedsIfRetiredNow))
                    : Strings.Get("ui.pass_on") + "  ·  " + Strings.Format("ui.pass_on_need", ("coins", NumberFormat.Short(_game.Sim.Config.HeritageThreshold - s.Generation.LifetimeCoinsThisGeneration)));
                label.color = can ? t.Seed : t.InkMuted;
            }
            _infoKey = -2;
            _hintKey = -2;
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
            var t = ActiveTheme;
            var node = sim.GetNode(_selectedId);
            bool heritage = sim.Heritage.Contains(_selectedId);
            _sheet.gameObject.SetActive(true);
            _sheetName.text = Strings.Name(node);
            _sheetName.color = t.Ink;
            _sheetDesc.text = Strings.Description(sim, node);
            _sheetDesc.color = t.Ink;
            int level = _game.State.GetLevel(_selectedId);
            int max = sim.GetMaxLevel(_selectedId);
            bool maxed = sim.IsMaxed(_selectedId);
            bool available = sim.IsAvailable(_selectedId);
            bool can = sim.CanBuy(_selectedId);
            // The level as a row of pips where the node has a ceiling; as a number where it has none.
            bool pips = max > 0 && max <= MaxPips;
            for (int i = 0; i < MaxPips; i++)
            {
                bool on = pips && i < max;
                if (_pips[i].gameObject.activeSelf != on) _pips[i].gameObject.SetActive(on);
                if (on) _pips[i].color = i < level ? (maxed ? t.CropMaxed : t.BedGrown) : t.InkMuted * new Color(1f, 1f, 1f, 0.25f);
            }
            _sheetLevel.text = pips ? Strings.Format("ui.level", ("level", level), ("max", max)) : Strings.Format("ui.level_open", ("level", level));
            _sheetLevel.color = t.InkMuted;
            // One note under the pips: what this opens, or why it cannot be bought yet.
            string note = "";
            var noteColor = t.InkMuted;
            if (maxed) note = Strings.Get("ui.maxed");
            else if (!available) { note = Strings.Format("ui.needs", ("prereqs", string.Join(" / ", Strings.Names(Prereqs(node))))); noteColor = t.Danger; }
            else if (!can)
            {
                // "Not enough coins" leaves the player counting. Say how many are missing.
                double missing = sim.CostOf(_selectedId) - (heritage ? _game.State.Seeds : _game.State.Coins);
                note = Strings.Format("ui.short_by",
                    ("amount", NumberFormat.Short(System.Math.Max(1d, System.Math.Ceiling(missing)))),
                    ("currency", Strings.Get(heritage ? "ui.seeds" : "ui.coins")));
                noteColor = t.Danger;
            }
            else note = UnlocksText(node.Id);
            _sheetNote.text = note;
            _sheetNote.color = noteColor;
            _buy.interactable = can;
            UiKit.ButtonLabel(_buy).text = maxed ? Strings.Get("ui.maxed")
                : !available ? Strings.Get("ui.locked")
                : Strings.Get("ui.buy") + "  ·  " + NumberFormat.Short(sim.CostOf(_selectedId)) + " " + Strings.Get(heritage ? "ui.seeds" : "ui.coins");
        }

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
            UiKit.Stretch(_barnStrip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 440f), new Vector2(-34f, 530f));
            _barnLine = UiKit.Label(_barnStrip, "Barn", "", UiType.Caption, _theme.Ink, TextAnchor.MiddleLeft);
            UiKit.Box(_barnLine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(330f, 92f));
            _sell = UiKit.Button(_barnStrip, "SellBarn", "", UiType.Caption, _theme.Accent, _theme.Ink, () => { if (_game.Sim.SellBarn()) { Haptics.Play(HapticKind.Medium); _audio?.Play(SfxId.MarketSale); _barnKey = -1; } });
            UiKit.Box(_sell.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(200f, 84f));
            _preserve = UiKit.Button(_barnStrip, "Preserves", "", UiType.Caption, _theme.Seed, _theme.Card, () => { if (_game.Sim.MakePreserves()) { Haptics.Play(HapticKind.Medium); _barnKey = -1; } });
            UiKit.Box(_preserve.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(562f, 0f), new Vector2(210f, 84f));
            _respec = UiKit.Button(_barnStrip, "Respec", "", UiType.Caption, _theme.Paper, _theme.Ink, () =>
            {
                if (_game.Sim.RespecAlmanac()) { Haptics.Play(HapticKind.Heavy); _barnKey = -1; _suggestKey = -1; RefreshAll(); }
            });
            UiKit.Box(_respec.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(210f, 84f));
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

        private void RefreshSuggestion(FarmState s)
        {
            long key = s.Phase == Phase.Winter ? (long)System.Math.Min(s.Coins, 1e15) * 64 + _game.Sim.Almanac.Levels.Count : -2;
            if (key == _suggestKey) return;
            _suggestKey = key;
            _suggestedId = AlmanacAdvisor.Suggest(_game.Sim);
            _almanac.SetSuggested(_suggestedId);
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

            RefreshBarnStrip(s);
            RefreshSuggestion(s);

            // The lines under the field. Winter: what the year brought and how it was graded; Heritage: how far the tree is.
            bool winter = s.Phase == Phase.Winter;
            bool gh = winter && s.Stats.GreenhouseLevel > 0;
            int ghSeconds = gh ? Mathf.CeilToInt(s.Greenhouse.SecondsLeftThisWinter) : -1;
            long infoKey = winter
                ? ((long)s.CoinsThisYear * 1000 + s.HarvestsThisYear) * 4096 + (gh ? (long)s.Greenhouse.CoinsThisWinter * 8 + ghSeconds % 8 : 0) + s.LastGrade * 16 + (s.Goal.Active ? (s.Goal.Done ? 2 : 1) : 0)
                : -(_game.Sim.HeritageDone + 1);
            if (infoKey != _infoKey)
            {
                _infoKey = infoKey;
                if (winter)
                {
                    _yearSummary.text = Strings.Format("ui.year_summary", ("harvests", s.HarvestsThisYear), ("coins", NumberFormat.Short(s.CoinsThisYear)));
                    string grade = "";
                    if (s.LastGrade > 0)
                    {
                        grade = Strings.Format("ui.grade_stars", ("stars", s.LastGrade));
                        if (s.LastGradeBonus > 0) grade += "  " + Strings.Format("ui.grade_bonus", ("coins", NumberFormat.Short(s.LastGradeBonus)));
                        if (s.Goal.Active) grade += "  ·  " + Strings.Get(s.Goal.Done ? "ui.goal_result_met" : "ui.goal_result_missed");
                    }
                    if (gh) grade += (grade.Length > 0 ? "  ·  " : "") + Strings.Format("ui.greenhouse", ("coins", NumberFormat.Short(s.Greenhouse.CoinsThisWinter)), ("seconds", ghSeconds));
                    _infoLine.text = grade;
                }
                else
                {
                    _yearSummary.text = Strings.Format("heritage.progress", ("done", _game.Sim.HeritageDone), ("total", _game.Sim.HeritageTotal));
                    _infoLine.text = "";
                }
            }
            // The first line says what to do: tap a bed, or, when nothing is affordable, move on.
            bool broke = winter && !_game.Sim.CanRetire && _suggestedId == null;
            int hintKey = (_selectedId != null ? 1 : 0) + (broke ? 2 : 0);
            if (hintKey != _hintKey)
            {
                _hintKey = hintKey;
                _tapHint.text = broke ? Strings.Get("ui.nothing_affordable") : Strings.Get("ui.tap_a_bed");
                _tapHint.gameObject.SetActive(_selectedId == null);
            }

            // Bottom sheet slide.
            bool want = _selectedId != null && _sheet.gameObject.activeSelf;
            _sheetShown = Prims.Damp(_sheetShown, want ? 1f : 0f, 12f, dt);
            _sheet.anchoredPosition = _sheetHome + new Vector2(0f, Mathf.Lerp(-460f, 0f, Prims.EaseOutQuad(_sheetShown)));
        }
    }
}
