using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Placeholder winter panel: Almanac and Heritage lists (grouped by branch) as two tabs,
    /// "Next Year", "Pass on the farm" (with confirm) and "Start new generation". Shown whenever the
    /// phase is not Year. The tree canvas comes in a later session.
    /// </summary>
    public sealed class WinterShopView : MonoBehaviour
    {
        private sealed class Row
        {
            public SkillNode Node;
            public Image Box;
            public Text Name, Effect, Level, Cost;
            public Button Buy;
            public Image BuyImage;
            public Text BuyLabel;
            public RectTransform BuyRt;
            public float Punch;
        }

        private static readonly Color Available = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color Locked = new Color(0f, 0f, 0f, 0.25f);

        private GameController _game;
        private AudioManager _audio;
        private GameObject _panel;
        private CanvasGroup _group;
        private Text _title, _sub, _coins, _greenhouse;
        private RectTransform _coinsRt;
        private readonly List<Row> _rows = new List<Row>();
        private GameObject _almanacList, _heritageList;
        private Button _almanacTab, _heritageTab;
        private Button _nextYear, _retire, _startGen;
        private Text _retireLabel;
        private GameObject _confirm;
        private Text _confirmText;
        private float _coinPunch;
        private float _open;
        private bool _visible;
        private bool _showingHeritage;

        public bool IsOpen => _visible;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;

            var bg = UiKit.Panel(canvas, "WinterShop", new Color(0.05f, 0.08f, 0.14f, 0.8f), false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var rt = bg.rectTransform;

            _title = UiKit.Label(rt, "Title", "Winter", 72, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 84f));
            _sub = UiKit.Label(rt, "Sub", "", 30, new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleCenter);
            UiKit.Box(_sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(1000f, 40f));
            _coins = UiKit.Label(rt, "Coins", "0", 50, UiKit.CoinYellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            UiKit.Box(_coinsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(900f, 60f));
            _greenhouse = UiKit.Label(rt, "Greenhouse", "", 26, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter);
            UiKit.Box(_greenhouse.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -228f), new Vector2(900f, 34f));

            _almanacTab = UiKit.Button(rt, "TabAlmanac", "ALMANAC", 30, UiKit.Accent, UiKit.Ink, () => ShowTab(false));
            UiKit.Box(_almanacTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-10f, -260f), new Vector2(420f, 70f));
            _heritageTab = UiKit.Button(rt, "TabHeritage", "HERITAGE", 30, UiKit.Muted, UiKit.Paper, () => ShowTab(true));
            UiKit.Box(_heritageTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, -260f), new Vector2(420f, 70f));

            _almanacList = BuildList(rt, "AlmanacList", _game.Sim.Nodes);
            _heritageList = BuildList(rt, "HeritageList", _game.Sim.HeritageNodes);

            _nextYear = UiKit.Button(rt, "NextYear", "Next Year ▶", 44, UiKit.Accent, UiKit.Ink, OnNextYear);
            UiKit.Box(_nextYear.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 60f), new Vector2(500f, 130f));
            _retire = UiKit.Button(rt, "Retire", "Pass on the farm", 34, new Color(0.55f, 0.35f, 0.7f), UiKit.Paper, OnRetirePressed);
            UiKit.Box(_retire.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 60f), new Vector2(500f, 130f));
            _retireLabel = _retire.GetComponentInChildren<Text>();
            _startGen = UiKit.Button(rt, "StartGeneration", "Start new generation ▶", 44, UiKit.Accent, UiKit.Ink, OnStartGeneration);
            UiKit.Box(_startGen.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 130f));

            BuildConfirm(rt);

            _panel.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += _ => Refresh();
            _game.Sim.Retired += _ => { _showingHeritage = true; Refresh(); };
        }

        private GameObject BuildList(RectTransform rt, string name, IReadOnlyList<SkillNode> nodes)
        {
            var viewport = UiKit.Rect(name, rt);
            UiKit.Stretch(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 210f), new Vector2(-30f, -305f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f);
            var content = UiKit.Rect("Content", viewport);
            UiKit.Stretch(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            float y = 0f;
            const float rowH = 118f, headerH = 64f;
            Branch? current = null;
            foreach (var node in nodes)
            {
                if (current != node.Branch)
                {
                    current = node.Branch;
                    var header = UiKit.Label(content, "Branch " + node.Branch, node.Branch.ToString().ToUpperInvariant(), 30, new Color(0.75f, 0.82f, 0.92f), TextAnchor.LowerLeft, FontStyle.Bold);
                    UiKit.Stretch(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, y - headerH + 8f), new Vector2(0f, y));
                    y -= headerH;
                }
                _rows.Add(BuildRow(content, node, y, rowH));
                y -= rowH;
            }
            content.sizeDelta = new Vector2(0f, -y + 20f);
            return viewport.gameObject;
        }

        private Row BuildRow(RectTransform content, SkillNode node, float y, float rowH)
        {
            var row = new Row { Node = node };
            row.Box = UiKit.Panel(content, "Row " + node.Id, Available, true, false);
            UiKit.Stretch(row.Box.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - rowH + 8f), new Vector2(0f, y));
            var b = row.Box.rectTransform;

            row.Name = UiKit.Label(b, "Name", Localize.Name(node), 36, UiKit.Paper, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Stretch(row.Name.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(24f, 0f), new Vector2(0f, -14f));
            row.Effect = UiKit.Label(b, "Effect", Localize.Desc(node), 24, new Color(0.78f, 0.84f, 0.92f), TextAnchor.LowerLeft);
            UiKit.Stretch(row.Effect.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 1f), new Vector2(24f, 14f), new Vector2(0f, 0f));
            row.Level = UiKit.Label(b, "Level", "", 26, new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleRight);
            UiKit.Stretch(row.Level.rectTransform, new Vector2(0.62f, 0.5f), new Vector2(0.79f, 1f), Vector2.zero, new Vector2(-6f, -8f));
            row.Cost = UiKit.Label(b, "Cost", "", 32, UiKit.CoinYellow, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch(row.Cost.rectTransform, new Vector2(0.62f, 0f), new Vector2(0.79f, 0.5f), new Vector2(0f, 8f), new Vector2(-6f, 0f));

            var id = node.Id;
            row.Buy = UiKit.Button(b, "Buy", "BUY", 30, UiKit.Good, UiKit.Paper, () => OnBuy(id));
            row.BuyImage = row.Buy.GetComponent<Image>();
            row.BuyLabel = row.Buy.GetComponentInChildren<Text>();
            row.BuyRt = row.Buy.GetComponent<RectTransform>();
            UiKit.Box(row.BuyRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(170f, 82f));
            return row;
        }

        private void BuildConfirm(RectTransform rt)
        {
            var dim = UiKit.Panel(rt, "ConfirmDim", new Color(0f, 0f, 0f, 0.6f), false, true);
            _confirm = dim.gameObject;
            var box = UiKit.Panel(dim.transform, "ConfirmBox", new Color(0.12f, 0.14f, 0.2f, 0.98f), true, true);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            var title = UiKit.Label(box.transform, "Title", "Pass on the farm?", 52, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(860f, 70f));
            _confirmText = UiKit.Label(box.transform, "Text", "", 30, new Color(0.85f, 0.88f, 0.95f), TextAnchor.UpperLeft);
            UiKit.Stretch(_confirmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 170f), new Vector2(-50f, -120f));
            var yes = UiKit.Button(box.transform, "Yes", "Retire", 36, new Color(0.55f, 0.35f, 0.7f), UiKit.Paper, OnRetireConfirmed);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 40f), new Vector2(380f, 110f));
            var no = UiKit.Button(box.transform, "No", "Cancel", 36, UiKit.Muted, UiKit.Paper, () => { _audio.Play(SfxId.UiClick); _confirm.SetActive(false); });
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 40f), new Vector2(380f, 110f));
            _confirm.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.WinterStarted -= Open;
        }

        /// <summary>Also used on load when the save is not in the Year phase.</summary>
        public void Open()
        {
            _visible = true;
            _open = 0f;
            _panel.SetActive(true);
            _game.InputBlocked = true;
            _showingHeritage = _game.State.Phase == Phase.Heritage;
            Refresh();
        }

        private void Close()
        {
            _visible = false;
            _panel.SetActive(false);
            _confirm.SetActive(false);
            _game.InputBlocked = false;
        }

        private void ShowTab(bool heritage)
        {
            _audio.Play(SfxId.UiClick);
            _showingHeritage = heritage;
            Refresh();
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
            _confirmText.text =
                "You earn " + _game.Sim.SeedsIfRetiredNow + " Heritage Seeds and start generation " + (s.Generation.Generation + 1) + ".\n\n" +
                "You keep: the Heritage tree, banked seeds, statistics.\n\n" +
                "You lose: " + NumberFormat.Short(s.Coins) + " coins, every Almanac level, the field (" + s.GridSize + "x" + s.GridSize + ", crop tiers), helpers, the year counter.";
            _confirm.SetActive(true);
        }

        private void OnRetireConfirmed()
        {
            _confirm.SetActive(false);
            if (_game.Sim.Retire())
            {
                _audio.Play(SfxId.WinterChime);
                _showingHeritage = true;
            }
            Refresh();
        }

        private void OnBuy(string id)
        {
            var row = _rows.Find(r => r.Node.Id == id);
            if (_game.Sim.TryBuy(id))
            {
                _audio.Play(SfxId.Purchase);
                if (row != null) row.Punch = 1f;
                _coinPunch = 1f;
            }
            else
            {
                _audio.Play(SfxId.Denied);
            }
            Refresh();
        }

        private void Refresh()
        {
            if (!_visible) return;
            var sim = _game.Sim;
            var s = sim.State;
            bool heritagePhase = s.Phase == Phase.Heritage;
            if (heritagePhase) _showingHeritage = true;

            _title.text = heritagePhase ? "Heritage — Generation " + s.Generation.Generation : "Winter — Year " + s.Year;
            _sub.text = heritagePhase ? "The farm changes hands. Spend your seeds, then begin." : "The Almanac. Spend your coins.";
            _coins.text = _showingHeritage ? s.Seeds + " seeds" : NumberFormat.Short(s.Coins) + " coins";
            _coins.color = _showingHeritage ? new Color(0.75f, 0.55f, 0.95f) : UiKit.CoinYellow;

            _almanacList.SetActive(!_showingHeritage);
            _heritageList.SetActive(_showingHeritage);
            _almanacTab.interactable = !heritagePhase;
            _almanacTab.GetComponent<Image>().color = _showingHeritage ? UiKit.Muted : UiKit.Accent;
            _heritageTab.GetComponent<Image>().color = _showingHeritage ? new Color(0.65f, 0.45f, 0.85f) : UiKit.Muted;

            _nextYear.gameObject.SetActive(!heritagePhase);
            _retire.gameObject.SetActive(!heritagePhase);
            _startGen.gameObject.SetActive(heritagePhase);
            if (!heritagePhase)
            {
                bool can = sim.CanRetire;
                _retire.interactable = can;
                double need = sim.Config.HeritageThreshold - s.Generation.LifetimeCoinsThisGeneration;
                _retireLabel.text = can
                    ? "Pass on the farm\n+" + sim.SeedsIfRetiredNow + " seeds"
                    : "Pass on the farm\nearn " + NumberFormat.Short(need) + " more coins";
                _retireLabel.fontSize = 28;
            }

            foreach (var row in _rows)
            {
                string id = row.Node.Id;
                int level = s.GetLevel(id);
                int max = sim.GetMaxLevel(id);
                bool maxed = sim.IsMaxed(id);
                bool available = sim.IsAvailable(id);
                bool can = sim.CanBuy(id);

                row.Level.text = "Lv " + level + "/" + max;
                row.Cost.text = !available ? NumberFormat.Short(sim.CostOf(id)) : maxed ? "MAX" : NumberFormat.Short(sim.CostOf(id));
                row.Box.color = available ? Available : Locked;
                row.Name.color = available ? UiKit.Paper : new Color(0.6f, 0.62f, 0.68f);
                row.Effect.text = available ? Localize.Desc(row.Node) : "Needs: " + string.Join(" or ", Localize.Names(row.Node.Prerequisites));
                row.Buy.interactable = can;
                row.BuyLabel.text = !available ? "LOCKED" : maxed ? "MAX" : "BUY";
                row.BuyImage.color = !available ? new Color(0.3f, 0.32f, 0.36f) : maxed ? UiKit.Muted : can ? UiKit.Good : new Color(0.35f, 0.4f, 0.45f);
            }
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;
            _open = Mathf.Min(1f, _open + dt / 0.35f);
            _group.alpha = Prims.EaseOutQuad(_open);
            _panel.transform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, Prims.EaseOutQuad(_open));

            var gh = _game.State.Greenhouse;
            bool ghOn = _game.State.Phase == Phase.Winter && _game.State.Stats.GreenhouseLevel > 0;
            _greenhouse.text = ghOn ? "Greenhouse: +" + NumberFormat.Short(gh.CoinsThisWinter) + " (" + Mathf.CeilToInt(gh.SecondsLeftThisWinter) + " s left)" : "";
            if (ghOn && !_showingHeritage) _coins.text = NumberFormat.Short(_game.State.Coins) + " coins";
            _coinPunch = Mathf.Max(0f, _coinPunch - dt * 5f);
            _coinsRt.localScale = Vector3.one * (1f + 0.2f * Prims.EaseOutQuad(_coinPunch));
            foreach (var row in _rows)
            {
                if (row.Punch <= 0f) continue;
                row.Punch = Mathf.Max(0f, row.Punch - dt * 6f);
                row.BuyRt.localScale = Vector3.one * (1f + 0.18f * Prims.EaseOutQuad(row.Punch));
            }
        }
    }
}
