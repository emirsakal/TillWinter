using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Placeholder Almanac: a scrolling list generated from <see cref="AlmanacData"/>, grouped by branch,
    /// showing locked / available / maxed / not-implemented states. The tree canvas comes in a later session.
    /// </summary>
    public sealed class WinterShopView : MonoBehaviour
    {
        private sealed class Row
        {
            public AlmanacNode Node;
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
        private Text _title, _coins;
        private RectTransform _coinsRt;
        private readonly List<Row> _rows = new List<Row>();
        private float _coinPunch;
        private float _open;
        private bool _visible;

        public bool IsOpen => _visible;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;

            var bg = UiKit.Panel(canvas, "WinterShop", new Color(0.05f, 0.08f, 0.14f, 0.8f), false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var rt = bg.rectTransform;

            _title = UiKit.Label(rt, "Title", "Winter", 76, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 90f));
            var sub = UiKit.Label(rt, "Sub", "The Almanac. Spend your coins.", 32, new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleCenter);
            UiKit.Box(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 44f));
            _coins = UiKit.Label(rt, "Coins", "0", 54, UiKit.CoinYellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            UiKit.Box(_coinsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(600f, 64f));

            // Scroll view between the header and the Next Year button.
            var viewport = UiKit.Rect("Viewport", rt);
            UiKit.Stretch(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 260f), new Vector2(-30f, -250f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f);
            var content = UiKit.Rect("Content", viewport);
            UiKit.Stretch(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            float y = 0f;
            const float rowH = 118f, headerH = 64f;
            Branch? current = null;
            foreach (var node in _game.Sim.Nodes)
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

            var next = UiKit.Button(rt, "NextYear", "Next Year ▶", 52, UiKit.Accent, UiKit.Ink, OnNextYear);
            UiKit.Box(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 140f));

            _panel.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += OnPurchased;
        }

        private Row BuildRow(RectTransform content, AlmanacNode node, float y, float rowH)
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

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.WinterStarted -= Open;
            _game.Sim.Purchased -= OnPurchased;
        }

        private void Open()
        {
            _visible = true;
            _open = 0f;
            _panel.SetActive(true);
            _game.InputBlocked = true;
            _title.text = "Winter — Year " + _game.State.Year;
            Refresh();
        }

        private void OnNextYear()
        {
            _audio.Play(SfxId.UiClick);
            _game.Sim.StartNextYear();
            _visible = false;
            _panel.SetActive(false);
            _game.InputBlocked = false;
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

        private void OnPurchased(PurchaseEvent e) => Refresh();

        private void Refresh()
        {
            var sim = _game.Sim;
            _coins.text = NumberFormat.Short(sim.State.Coins) + " coins";
            foreach (var row in _rows)
            {
                string id = row.Node.Id;
                int level = sim.State.GetLevel(id);
                int max = sim.GetMaxLevel(id);
                bool maxed = sim.IsMaxed(id);
                bool available = sim.IsAvailable(id);
                bool can = sim.CanBuy(id);
                bool implemented = row.Node.IsImplemented;

                row.Level.text = "Lv " + level + "/" + max;
                row.Cost.text = maxed ? "MAX" : NumberFormat.Short(sim.CostOf(id));
                row.Box.color = available ? Available : Locked;
                row.Name.color = available ? UiKit.Paper : new Color(0.6f, 0.62f, 0.68f);
                row.Effect.text = (implemented ? "" : "[not implemented yet] ") + (available ? Localize.Desc(row.Node) : "Needs: " + string.Join(" or ", Localize.Names(row.Node.Prerequisites)));
                row.Buy.interactable = can;
                row.BuyLabel.text = maxed ? "MAX" : !available ? "LOCKED" : "BUY";
                row.BuyImage.color = maxed ? UiKit.Muted : !available ? new Color(0.3f, 0.32f, 0.36f) : can ? (implemented ? UiKit.Good : new Color(0.55f, 0.6f, 0.45f)) : new Color(0.35f, 0.4f, 0.45f);
            }
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;
            _open = Mathf.Min(1f, _open + dt / 0.35f);
            _group.alpha = Prims.EaseOutQuad(_open);
            _panel.transform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, Prims.EaseOutQuad(_open));

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
