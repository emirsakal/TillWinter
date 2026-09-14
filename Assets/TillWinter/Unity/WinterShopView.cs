using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Full-screen winter shop over the frozen field. Opens on WinterStarted, closes on Next Year.</summary>
    public sealed class WinterShopView : MonoBehaviour
    {
        private sealed class Row
        {
            public UpgradeDef Def;
            public Text Level, Cost;
            public Button Buy;
            public Image BuyImage;
            public RectTransform BuyRt;
            public float Punch;
        }

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

            var bg = UiKit.Panel(canvas, "WinterShop", new Color(0.05f, 0.08f, 0.14f, 0.74f), false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var rt = bg.rectTransform;

            _title = UiKit.Label(rt, "Title", "Winter", 84, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 100f));
            var sub = UiKit.Label(rt, "Sub", "The field sleeps. Spend your coins.", 36, new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleCenter);
            UiKit.Box(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(900f, 50f));

            _coins = UiKit.Label(rt, "Coins", "0", 60, UiKit.CoinYellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            _coinsRt = _coins.rectTransform;
            UiKit.Box(_coinsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(600f, 70f));

            float y = -320f;
            const float rowH = 132f;
            foreach (var def in _game.Sim.Config.Upgrades)
            {
                var row = new Row { Def = def };
                var box = UiKit.Panel(rt, "Row " + def.Id, new Color(1f, 1f, 1f, 0.08f), true, false);
                UiKit.Stretch(box.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, y - rowH + 8f), new Vector2(-40f, y));
                var b = box.rectTransform;

                var name = UiKit.Label(b, "Name", def.Name, 40, UiKit.Paper, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Stretch(name.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(28f, 0f), new Vector2(0f, -16f));
                var effect = UiKit.Label(b, "Effect", def.Effect, 27, new Color(0.78f, 0.84f, 0.92f), TextAnchor.LowerLeft);
                UiKit.Stretch(effect.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(28f, 16f), new Vector2(0f, 0f));

                row.Level = UiKit.Label(b, "Level", "Lv 0/1", 28, new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleRight);
                UiKit.Stretch(row.Level.rectTransform, new Vector2(0.62f, 0.5f), new Vector2(0.78f, 1f), Vector2.zero, new Vector2(-6f, -10f));
                row.Cost = UiKit.Label(b, "Cost", "0", 34, UiKit.CoinYellow, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.Stretch(row.Cost.rectTransform, new Vector2(0.62f, 0f), new Vector2(0.78f, 0.5f), new Vector2(0f, 10f), new Vector2(-6f, 0f));

                var id = def.Id;
                row.Buy = UiKit.Button(b, "Buy", "BUY", 34, UiKit.Good, UiKit.Paper, () => OnBuy(id));
                row.BuyImage = row.Buy.GetComponent<Image>();
                row.BuyRt = row.Buy.GetComponent<RectTransform>();
                UiKit.Box(row.BuyRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(180f, 90f));
                _rows.Add(row);
                y -= rowH;
            }

            var next = UiKit.Button(rt, "NextYear", "Next Year ▶", 56, UiKit.Accent, UiKit.Ink, OnNextYear);
            UiKit.Box(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(760f, 150f));

            _panel.SetActive(false);
            _game.Sim.WinterStarted += Open;
            _game.Sim.Purchased += OnPurchased;
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

        private void OnBuy(UpgradeId id)
        {
            var row = _rows.Find(r => r.Def.Id == id);
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

        private void OnPurchased(UpgradeId id)
        {
            Refresh();
        }

        private void Refresh()
        {
            var sim = _game.Sim;
            _coins.text = NumberFormat.Short(sim.State.Coins) + " coins";
            foreach (var row in _rows)
            {
                int level = sim.State.GetLevel(row.Def.Id);
                int max = sim.GetMaxLevel(row.Def.Id);
                bool maxed = sim.IsMaxed(row.Def.Id);
                row.Level.text = "Lv " + level + "/" + max;
                row.Cost.text = maxed ? "MAX" : NumberFormat.Short(sim.GetCost(row.Def.Id));
                bool can = sim.CanBuy(row.Def.Id);
                row.Buy.interactable = can;
                row.BuyImage.color = maxed ? UiKit.Muted : can ? UiKit.Good : new Color(0.35f, 0.4f, 0.45f);
                row.Buy.GetComponentInChildren<Text>().text = maxed ? "MAX" : "BUY";
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
