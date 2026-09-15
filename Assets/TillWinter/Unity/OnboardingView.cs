using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// One-shot hints (GDD §10.5). Flags live in Core (<see cref="OnboardingFlags"/>) and are saved; this view only
    /// decides when a pending hint is relevant, shows it without blocking input, and marks it when the taught
    /// action happens. In-year hints only; the Winter/Heritage/CanRetire hints are driven by the Winter screen.
    /// </summary>
    public sealed class OnboardingView : MonoBehaviour
    {
        private GameController _game;
        private HudTheme _theme;
        private RectTransform _canvas;
        private RectTransform _hand;
        private Image _handImg, _ripple;
        private RectTransform _captionRt;
        private TMP_Text _caption;
        private CanvasGroup _captionGroup;
        private RectTransform _arrow;
        private float _captionUntil;
        private bool _handShown;
        private GridPos? _handPlot;
        private float _holdShownAt = -1f;
        private GridPos? _ripePlot;
        private GridPos? _crowPlot;
        private float _pulse;

        public void Init(GameController game, RectTransform canvas)
        {
            _game = game;
            _canvas = canvas;
            _theme = HudTheme.Load();

            // Touch hint: a beating dot with a ripple expanding out of it (the kit has no hand icon).
            _hand = UiKit.Rect("TouchHint", canvas);
            UiKit.Box(_hand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            _ripple = UiKit.CircleImage(_hand, "Ripple", _theme.HintAccent, Vector2.zero, 120f);
            _handImg = UiKit.CircleImage(_hand, "Dot", _theme.HintAccent, Vector2.zero, 54f);
            UiKit.CircleImage(_handImg.transform, "DotInner", _theme.HintText, Vector2.zero, 26f);
            _hand.gameObject.SetActive(false);

            var cap = UiKit.Panel(canvas, "Caption", _theme.HintBackground, true, false);
            _captionRt = cap.rectTransform;
            UiKit.Box(_captionRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -560f), new Vector2(920f, 84f));
            _captionGroup = cap.gameObject.AddComponent<CanvasGroup>();
            _captionGroup.alpha = 0f;
            _caption = UiKit.Label(cap.transform, "Text", "", UiType.Body, _theme.HintText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));

            var arrow = NodeIcons.Image(canvas, "arrowUp", _theme.HintAccent); // turned over: it points down at the plot
            _arrow = arrow.rectTransform;
            UiKit.Box(_arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(76f, 76f));
            _arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            _arrow.gameObject.SetActive(false);

            _game.Sim.PlotRipened += OnRipened;
            _game.Sim.CrowLanded += e => { if (_game.Sim.HintPending(Hint.FirstCrow)) _crowPlot = e.Pos; };
            _game.Sim.CrowScared += _ => { if (_crowPlot.HasValue) { _crowPlot = null; _game.Sim.MarkHint(Hint.FirstCrow); } };
            _game.Sim.CrowAte += _ => { _crowPlot = null; };
            _game.Sim.FrostWarningStarted += () => { if (_game.Sim.MarkHint(Hint.FirstFrost)) Say("hint.first_frost", 5f); };
            _game.Sim.Harvested += e => { if (_ripePlot.HasValue && e.Pos == _ripePlot.Value) { _ripePlot = null; _game.Sim.MarkHint(Hint.FirstRipeOutside); } };
        }

        private void OnRipened(GridPos pos)
        {
            if (!_game.Sim.HintPending(Hint.FirstRipeOutside) || _ripePlot.HasValue) return;
            if (_game.State.IsUnderRing(pos)) return;
            _ripePlot = pos;
            Say("hint.first_ripe", 4f);
        }

        private void Say(string key, float seconds)
        {
            _caption.text = Strings.Get(key);
            _captionUntil = Time.unscaledTime + seconds;
        }

        private Vector2 PlotToCanvas(GridPos pos, float y)
        {
            Vector2 screen = _game.Cam.WorldToScreenPoint(_game.PlotToWorld(pos, y));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            return local;
        }

        private void Update()
        {
            if (_game.Paused) return; // hints wait while the pause menu is open
            var sim = _game.Sim;
            var s = _game.State;
            float dt = Time.unscaledDeltaTime;
            _pulse += dt;
            bool inYear = s.Phase == Phase.Year && !_game.InputBlocked;

            // 1. First touch: pulsing hand over a Dry plot until the ring covers a plot.
            if (sim.HintPending(Hint.FirstTouch) && inYear)
            {
                if (!_handPlot.HasValue)
                {
                    var plots = s.Plots;
                    for (int i = 0; i < plots.Count; i++) if (plots[i].State == PlotState.Dry) { _handPlot = plots[i].Pos; break; }
                }
                if (_handPlot.HasValue)
                {
                    if (!_hand.gameObject.activeSelf) _hand.gameObject.SetActive(true);
                    _hand.anchoredPosition = PlotToCanvas(_handPlot.Value, 0.3f);
                    float ripple = _pulse * 0.8f % 1f;
                    _ripple.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, ripple);
                    var rippleColor = _theme.HintAccent;
                    rippleColor.a = 0.5f * (1f - ripple);
                    _ripple.color = rippleColor;
                    _handImg.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(_pulse * 6f));
                    if (_captionGroup.alpha < 0.5f) Say("hint.first_touch", 1f);
                }
                bool anyUnderRing = false;
                if (s.Ring.HasValue)
                {
                    var plots = s.Plots;
                    for (int i = 0; i < plots.Count; i++) if (s.IsUnderRing(plots[i].Pos)) { anyUnderRing = true; break; }
                }
                if (anyUnderRing)
                {
                    sim.MarkHint(Hint.FirstTouch);
                    _hand.gameObject.SetActive(false);
                    _holdShownAt = Time.unscaledTime;
                    if (sim.MarkHint(Hint.Hold)) Say("hint.hold", 3f);
                }
            }
            else if (_hand.gameObject.activeSelf) _hand.gameObject.SetActive(false);

            // 3. First Ripe outside the ring: highlight arrow on that plot.
            if (_ripePlot.HasValue && inYear && s.InBounds(_ripePlot.Value) && s.GetPlot(_ripePlot.Value).IsRipe)
            {
                if (!_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(true);
                _arrow.anchoredPosition = PlotToCanvas(_ripePlot.Value, 0.8f) + new Vector2(0f, 20f + Mathf.Abs(Mathf.Sin(_pulse * 5f)) * 20f);
            }
            else if (_crowPlot.HasValue && inYear && s.InBounds(_crowPlot.Value) && s.GetPlot(_crowPlot.Value).HasCrow)
            {
                // 6. First crow: arrow + caption.
                if (!_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(true);
                _arrow.anchoredPosition = PlotToCanvas(_crowPlot.Value, 0.9f) + new Vector2(0f, 20f + Mathf.Abs(Mathf.Sin(_pulse * 5f)) * 20f);
                if (_captionGroup.alpha < 0.5f) Say("hint.first_crow", 1.5f);
            }
            else if (_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(false);
            if (_ripePlot.HasValue && (!s.InBounds(_ripePlot.Value) || !s.GetPlot(_ripePlot.Value).IsRipe)) _ripePlot = null;

            bool show = Time.unscaledTime < _captionUntil && s.Phase == Phase.Year;
            _captionGroup.alpha = Prims.Damp(_captionGroup.alpha, show ? 1f : 0f, 10f, dt);
        }
    }
}
