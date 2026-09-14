using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Top band: coin counter, year/season, season-segmented year timer. Also runs the coin flight FX.</summary>
    public sealed class HudView : MonoBehaviour
    {
        private sealed class Coin
        {
            public RectTransform Rt;
            public Vector2 From, Ctrl, To;
            public float Delay, T, Duration;
            public double Value;
            public bool Active;
        }

        private GameController _game;
        private AudioManager _audio;
        private RectTransform _canvas;
        private RectTransform _coinGroup;
        private Text _coinText;
        private Text _yearText;
        private Text _seedsHint;
        private Text _combo;
        private Text _seasonText;
        private RectTransform _timerBar;
        private RectTransform _timerFill;
        private Image _timerFillImage;
        private RectTransform _fxLayer;
        private CanvasGroup _topGroup;

        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<RectTransform> _pool = new Stack<RectTransform>();
        private double _pending;
        private float _counterPunch;
        private float _lastFrostSecond = -1f;

        private static readonly Color SpringC = new Color(0.55f, 0.8f, 0.4f);
        private static readonly Color SummerC = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color AutumnC = new Color(0.92f, 0.55f, 0.25f);

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            _canvas = canvas;

            var top = UiKit.Rect("TopBand", canvas);
            UiKit.Stretch(top, new Vector2(0f, 0.8f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _topGroup = top.gameObject.AddComponent<CanvasGroup>();

            // Coin counter
            _coinGroup = UiKit.Rect("Coins", top);
            UiKit.Box(_coinGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(600f, 120f));
            var icon = UiKit.Panel(_coinGroup, "Icon", UiKit.CoinYellow, false, false);
            icon.sprite = UiKit.Circle;
            icon.type = Image.Type.Simple;
            UiKit.Box(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(72f, 72f));
            var iconInner = UiKit.Panel(icon.transform, "Inner", new Color(0.85f, 0.6f, 0.1f), false, false);
            iconInner.sprite = UiKit.Circle;
            iconInner.type = Image.Type.Simple;
            UiKit.Box(iconInner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            _coinText = UiKit.Label(_coinGroup, "Value", "0", 96, UiKit.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_coinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(200f, 0f), Vector2.zero);
            AddShadow(_coinText);

            _yearText = UiKit.Label(top, "Year", "Year 1", 44, UiKit.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_yearText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -290f), new Vector2(400f, 60f));
            AddShadow(_yearText);
            _seasonText = UiKit.Label(top, "Season", "Spring", 44, UiKit.Paper, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Box(_seasonText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -290f), new Vector2(400f, 60f));
            AddShadow(_seasonText);
            _seedsHint = UiKit.Label(top, "SeedsHint", "", 30, new Color(0.85f, 0.7f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_seedsHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 44f));
            AddShadow(_seedsHint);
            _combo = UiKit.Label(top, "Combo", "", 40, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Box(_combo.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -400f), new Vector2(400f, 50f));
            AddShadow(_combo);

            // Timer bar with season segments
            _timerBar = UiKit.Rect("TimerBar", top);
            UiKit.Box(_timerBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(960f, 18f));
            var bg = UiKit.Panel(_timerBar, "Bg", new Color(0f, 0f, 0f, 0.35f), true, false);
            UiKit.Stretch(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            Segment(_timerBar, "Spring", 0f, 1f / 3f, SpringC);
            Segment(_timerBar, "Summer", 1f / 3f, 2f / 3f, SummerC);
            Segment(_timerBar, "Autumn", 2f / 3f, 1f, AutumnC);
            _timerFillImage = UiKit.Panel(_timerBar, "Elapsed", new Color(0.1f, 0.08f, 0.06f, 0.55f), true, false);
            _timerFill = _timerFillImage.rectTransform;
            UiKit.Stretch(_timerFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var marker = UiKit.Panel(_timerFill, "Marker", UiKit.Paper, false, false);
            UiKit.Box(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 30f));

            _fxLayer = UiKit.Rect("CoinFx", canvas);
            UiKit.Stretch(_fxLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _game.Sim.Harvested += OnHarvested;
            _game.Sim.CrowScared += OnCrowScared;
            _game.Sim.YearStarted += OnYearStarted;
            _game.Sim.WinterStarted += OnWinter;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.Harvested -= OnHarvested;
            _game.Sim.CrowScared -= OnCrowScared;
            _game.Sim.YearStarted -= OnYearStarted;
            _game.Sim.WinterStarted -= OnWinter;
        }

        private static void AddShadow(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.5f);
            s.effectDistance = new Vector2(2f, -3f);
        }

        private static void Segment(RectTransform bar, string name, float from, float to, Color color)
        {
            var img = UiKit.Panel(bar, name, color, false, false);
            UiKit.Stretch(img.rectTransform, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(from > 0f ? 2f : 0f, 0f), new Vector2(to < 1f ? -2f : 0f, 0f));
        }

        private void OnYearStarted()
        {
            _lastFrostSecond = -1f;
        }

        private bool _hudVisible = true;

        private void OnHarvested(HarvestEvent e)
        {
            int count = Mathf.Clamp(3 + e.Tier + (e.Source == HarvestSource.Apprentice ? 1 : 0) + (e.WasGolden ? 3 : 0), 3, 10);
            SpawnCoins(_game.PlotToWorld(e.Pos, 0.5f), e.Coins, count);
        }

        private void OnCrowScared(CrowEvent e)
        {
            if (e.Coins > 0) SpawnCoins(_game.PlotToWorld(e.Pos, 0.6f), e.Coins, 4);
        }

        private void OnWinter()
        {
            _audio.Play(SfxId.WinterChime);
            foreach (var c in _coins)
            {
                c.Rt.gameObject.SetActive(false);
                _pool.Push(c.Rt);
            }
            _coins.Clear();
            _pending = 0;
        }

        private void SpawnCoins(Vector3 world, double coins, int count)
        {
            Vector2 from = WorldToCanvas(world);
            Vector2 to = _canvas.InverseTransformPoint(_coinGroup.TransformPoint(new Vector3(-150f, 0f, 0f)));
            double share = coins / count;
            for (int i = 0; i < count; i++)
            {
                var coin = new Coin
                {
                    Rt = GetCoin(),
                    From = from + Random.insideUnitCircle * 30f,
                    To = to,
                    Delay = i * 0.045f,
                    Duration = Random.Range(0.5f, 0.65f),
                    Value = share,
                    Active = true,
                };
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
            var img = UiKit.Panel(_fxLayer, "Coin", UiKit.CoinYellow, false, false);
            img.sprite = UiKit.Circle;
            img.type = Image.Type.Simple;
            UiKit.Box(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            var inner = UiKit.Panel(img.transform, "Inner", new Color(0.85f, 0.6f, 0.1f), false, false);
            inner.sprite = UiKit.Circle;
            inner.type = Image.Type.Simple;
            UiKit.Box(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
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

            // Coins in flight
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                c.T += dt;
                float t = (c.T - c.Delay) / c.Duration;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    _pending -= c.Value;
                    _counterPunch = 1f;
                    _audio.Play(SfxId.CoinArrive);
                    c.Rt.gameObject.SetActive(false);
                    _pool.Push(c.Rt);
                    _coins.RemoveAt(i);
                    continue;
                }
                float e = t * t * (3f - 2f * t);
                e = Mathf.Lerp(t, e * e, 0.5f); // ease-in-ish
                var p = Bezier(c.From, c.Ctrl, c.To, e);
                c.Rt.anchoredPosition = p;
                c.Rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, t * 3f)) * Mathf.Lerp(1f, 0.8f, t);
            }
            if (_coins.Count == 0) _pending = 0;

            _hudVisible = !state.IsWinter;
            _topGroup.alpha = Prims.Damp(_topGroup.alpha, _hudVisible ? 1f : 0f, 8f, dt);
            double shown = System.Math.Max(0, state.Coins - _pending);
            _coinText.text = NumberFormat.Short(shown);
            _counterPunch = Mathf.Max(0f, _counterPunch - dt * 5f);
            _coinGroup.localScale = Vector3.one * (1f + 0.22f * Prims.EaseOutQuad(_counterPunch));

            _yearText.text = "Year " + state.Year + "  \u00B7  Gen " + state.Generation.Generation;
            _seedsHint.text = _game.Sim.CanRetire ? "seeds if you retire: " + _game.Sim.SeedsIfRetiredNow : "";
            _combo.text = state.Combo >= 2 ? "combo x" + state.Combo : "";
            _combo.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Max(0f, 1f - state.ComboTimer * 4f));
            _seasonText.text = state.Season.ToString();

            // Timer
            float progress = state.YearLength > 0f ? Mathf.Clamp01(state.YearTime / state.YearLength) : 0f;
            _timerFill.anchorMax = new Vector2(progress, 1f);
            if (state.FrostWarning && !state.IsWinter)
            {
                float left = state.SecondsUntilWinter;
                float f = 1f - Mathf.Clamp01(left / _game.State.Stats.FrostWarningSeconds);
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(4f, 9f, f))), 8f);
                _timerBar.localScale = Vector3.one * (1f + 0.08f * beat);
                _timerFillImage.color = Color.Lerp(new Color(0.1f, 0.08f, 0.06f, 0.55f), new Color(0.3f, 0.5f, 1f, 0.8f), beat);
                float sec = Mathf.Ceil(left);
                if (sec != _lastFrostSecond)
                {
                    _lastFrostSecond = sec;
                    _audio.Play(SfxId.FrostTick, Mathf.Lerp(0.5f, 1f, f));
                }
            }
            else
            {
                _timerBar.localScale = Vector3.one;
                _timerFillImage.color = new Color(0.1f, 0.08f, 0.06f, 0.55f);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
