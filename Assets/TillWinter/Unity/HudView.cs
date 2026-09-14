using System.Collections.Generic;
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
        }

        private GameController _game;
        private AudioManager _audio;
        private HudTheme _theme;
        private RectTransform _canvas, _safe;
        private CanvasGroup _topGroup;
        private RectTransform _coinGroup;
        private TMP_Text _coinText, _subText, _seasonName, _combo, _seedChip;
        private RectTransform _seedChipRt;
        private RectTransform _bar, _elapsed, _frostSpan;
        private Image _elapsedImage;
        private RectTransform _fxLayer;

        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<RectTransform> _pool = new Stack<RectTransform>();
        private double _pending;
        /// <summary>Coins earned offline that the away card has not released yet (kept out of the counter).</summary>
        public double HeldCoins;
        private float _counterPunch;
        private float _lastFrostSecond = -1f;
        private Season _shownSeason = Season.Winter;
        private float _seasonFade;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            _canvas = canvas;
            _theme = HudTheme.Load();

            _safe = UiKit.Rect("HudSafe", canvas);
            SafeArea.Apply(_safe);
            var top = UiKit.Rect("TopBand", _safe);
            UiKit.Stretch(top, new Vector2(0f, 0.78f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _topGroup = top.gameObject.AddComponent<CanvasGroup>();

            _coinGroup = UiKit.Rect("Coins", top);
            UiKit.Box(_coinGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 90f), new Vector2(700f, 130f));
            var icon = UiKit.CircleImage(_coinGroup, "Icon", _theme.Coin, new Vector2(-190f, 0f), 76f);
            UiKit.CircleImage(icon.transform, "Inner", _theme.CoinInner, Vector2.zero, 48f);
            _coinText = UiKit.Label(_coinGroup, "Value", "0", (int)_theme.CoinFontSize, _theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_coinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(240f, 0f), Vector2.zero);
            UiKit.Outline(_coinText);

            _subText = UiKit.Label(top, "Sub", "", (int)_theme.SubFontSize, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_subText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_theme.TopPadding - 190f), new Vector2(800f, 50f));
            UiKit.Outline(_subText, 0.12f);

            _combo = UiKit.Label(top, "Combo", "", 40, _theme.Combo, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_combo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(260f, -_theme.TopPadding - 90f), new Vector2(300f, 60f));
            UiKit.Outline(_combo);

            var chip = UiKit.Panel(top, "SeedChip", _theme.SeedChip, true, false);
            _seedChipRt = chip.rectTransform;
            UiKit.Box(_seedChipRt, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-260f, -_theme.TopPadding - 90f), new Vector2(260f, 56f));
            UiKit.CircleImage(_seedChipRt, "Seed", _theme.Seed, new Vector2(-100f, 0f), 30f);
            _seedChip = UiKit.Label(_seedChipRt, "Text", "", 28, _theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_seedChip.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 0f), new Vector2(-10f, 0f));
            _seedChipRt.gameObject.SetActive(false);

            BuildSeasonBar(top);

            _seasonName = UiKit.Label(top, "SeasonName", "", 40, _theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_seasonName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _theme.SeasonNameY), new Vector2(600f, 50f));
            UiKit.Outline(_seasonName);

            _fxLayer = UiKit.Rect("CoinFx", canvas);
            UiKit.Stretch(_fxLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _game.Sim.Harvested += OnHarvested;
            _game.Sim.CrowScared += OnCrowScared;
            _game.Sim.YearStarted += OnYearStarted;
            _game.Sim.WinterStarted += OnWinter;
        }

        private void BuildSeasonBar(RectTransform top)
        {
            _bar = UiKit.Rect("SeasonBar", top);
            UiKit.Box(_bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _theme.BarY), new Vector2(_theme.BarWidth, _theme.BarHeight));
            var bg = UiKit.Panel(_bar, "Bg", _theme.BarBackground, true, false);
            UiKit.Stretch(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            float w = 0.3f;
            Segment("Spring", 0f, w, _theme.Spring);
            Segment("Summer", w, 2f * w, _theme.Summer);
            Segment("Autumn", 2f * w, 3f * w, _theme.Autumn);
            Segment("Winter", 3f * w, 1f, _theme.Winter);
            var frost = UiKit.Panel(_bar, "Frost", _theme.Frost, false, false);
            _frostSpan = frost.rectTransform;
            UiKit.Stretch(_frostSpan, new Vector2(3f * w - 0.1f, 0f), new Vector2(3f * w, 1f), new Vector2(0f, -3f), new Vector2(0f, 3f));
            _elapsedImage = UiKit.Panel(_bar, "Elapsed", _theme.BarElapsed, true, false);
            _elapsed = _elapsedImage.rectTransform;
            UiKit.Stretch(_elapsed, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var marker = UiKit.Panel(_elapsed, "Marker", _theme.BarMarker, false, false);
            UiKit.Box(marker.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, _theme.BarHeight + 14f));
        }

        private void Segment(string name, float from, float to, Color color)
        {
            var img = UiKit.Panel(_bar, name, color, false, false);
            UiKit.Stretch(img.rectTransform, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(from > 0f ? 2f : 0f, 0f), new Vector2(to < 1f ? -2f : 0f, 0f));
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
            _audio.Play(SfxId.WinterChime);
            foreach (var c in _coins)
            {
                c.Rt.gameObject.SetActive(false);
                _pool.Push(c.Rt);
            }
            _coins.Clear();
            _pending = 0;
        }

        private void OnHarvested(HarvestEvent e)
        {
            if (_game.Sim.IsSimulatingOffline) return;
            int count = Mathf.Clamp(3 + e.Tier + (e.Source == HarvestSource.Apprentice ? 1 : 0) + (e.WasGolden ? 3 : 0), 3, 10);
            SpawnCoins(_game.PlotToWorld(e.Pos, 0.5f), e.Coins, count);
        }

        private void OnCrowScared(CrowEvent e)
        {
            if (e.Coins > 0) SpawnCoins(_game.PlotToWorld(e.Pos, 0.6f), e.Coins, 4);
        }

        /// <summary>Counter punch without coin flight (away card OK).</summary>
        public void Punch() => _counterPunch = 1f;

        private void SpawnCoins(Vector3 world, double coins, int count)
        {
            Vector2 from = WorldToCanvas(world);
            Vector2 to = _canvas.InverseTransformPoint(_coinGroup.TransformPoint(new Vector3(-190f, 0f, 0f)));
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
                e = Mathf.Lerp(t, e * e, 0.5f);
                c.Rt.anchoredPosition = Bezier(c.From, c.Ctrl, c.To, e);
                c.Rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, t * 3f)) * Mathf.Lerp(1f, 0.8f, t);
            }
            if (_coins.Count == 0) _pending = 0;

            _topGroup.alpha = Prims.Damp(_topGroup.alpha, state.Phase == Phase.Year ? 1f : 0f, 8f, dt);
            double shown = System.Math.Max(0, state.Coins - _pending - HeldCoins);
            _coinText.text = NumberFormat.Short(shown);
            _counterPunch = Mathf.Max(0f, _counterPunch - dt * 5f);
            _coinGroup.localScale = Vector3.one * (1f + 0.22f * Prims.EaseOutQuad(_counterPunch));

            _subText.text = "Year " + state.Year + "  ·  Gen " + state.Generation.Generation;
            _combo.text = state.Combo >= 2 ? "x" + state.Combo : "";
            _combo.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Max(0f, 1f - state.ComboTimer * 4f));
            bool canRetire = _game.Sim.CanRetire;
            if (_seedChipRt.gameObject.activeSelf != canRetire) _seedChipRt.gameObject.SetActive(canRetire);
            if (canRetire) _seedChip.text = "Retire: " + _game.Sim.SeedsIfRetiredNow;

            float progress = state.YearLength > 0f ? Mathf.Clamp01(state.YearTime / state.YearLength) * 0.9f : 0f;
            if (state.Phase != Phase.Year) progress = 1f;
            _elapsed.anchorMax = new Vector2(progress, 1f);
            float frostFrac = state.YearLength > 0f ? state.Stats.FrostWarningSeconds / state.YearLength * 0.9f : 0.1f;
            _frostSpan.anchorMin = new Vector2(0.9f - frostFrac, 0f);
            if (state.Season != _shownSeason)
            {
                _shownSeason = state.Season;
                _seasonFade = 0f;
                _seasonName.text = state.Season.ToString();
                _seasonName.color = _theme.SeasonColor(state.Season);
            }
            _seasonFade += dt;
            float fade = _seasonFade < 0.4f ? _seasonFade / 0.4f : _seasonFade < 2.5f ? 1f : Mathf.Max(0.35f, 1f - (_seasonFade - 2.5f));
            var sc = _seasonName.color;
            sc.a = fade;
            _seasonName.color = sc;

            if (state.FrostWarning && state.Phase == Phase.Year)
            {
                float left = state.SecondsUntilWinter;
                float f = 1f - Mathf.Clamp01(left / state.Stats.FrostWarningSeconds);
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(4f, 9f, f))), 8f);
                _bar.localScale = Vector3.one * (1f + 0.08f * beat);
                _elapsedImage.color = Color.Lerp(_theme.BarElapsed, _theme.Frost, beat);
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
