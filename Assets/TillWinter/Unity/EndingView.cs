using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// GDD §8 ending: when the Golden Year ends, the credits roll over the field (skippable after 3 s),
    /// then the statistics sheet, then the normal Winter screen underneath.
    /// </summary>
    public sealed class EndingView : MonoBehaviour
    {
        public const float SkipAfterSeconds = 3f;
        public const float RollSeconds = 24f;

        private GameController _game;
        private PauseMenu _pause;
        private HudTheme _theme;
        private GameObject _root;
        private RectTransform _rootRt, _roll;
        private TMP_Text _skip;
        private float _t, _rollHeight;

        public bool Active { get; private set; }

        public void Init(GameController game, RectTransform canvas, PauseMenu pause)
        {
            _game = game;
            _pause = pause;
            _theme = HudTheme.Load();

            var overlay = UiKit.Panel(canvas, "EndingCredits", _theme.CreditsOverlay, false, true);
            _rootRt = overlay.rectTransform;
            UiKit.Stretch(_rootRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tap = overlay.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(TrySkip);
            _root = overlay.gameObject;

            _roll = UiKit.Rect("Roll", overlay.transform);
            UiKit.Box(_roll, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(960f, 10f));
            float y = 0f;
            Line("ending.title", 76, _theme.Gold, FontStyles.Bold, ref y, 130f);
            Line("ending.caption", 38, _theme.Text, FontStyles.Normal, ref y, 160f);
            y -= 220f;
            Line("credits.title", 52, _theme.Gold, FontStyles.Bold, ref y, 110f);
            Line("credits.made_by", 40, _theme.Text, FontStyles.Bold, ref y, 100f);
            Line("credits.unity", 34, _theme.Text, FontStyles.Normal, ref y, 130f);
            Line("credits.kenney", 34, _theme.Text, FontStyles.Normal, ref y, 80f);
            Line("credits.kits_art", 28, _theme.TextMuted, FontStyles.Normal, ref y, 110f);
            Line("credits.kits_audio", 28, _theme.TextMuted, FontStyles.Normal, ref y, 140f);
            Line("credits.font", 32, _theme.Text, FontStyles.Normal, ref y, 100f);
            y -= 260f;
            Line("credits.thanks", 46, _theme.Gold, FontStyles.Bold, ref y, 110f);
            Line("ending.after", 32, _theme.TextMuted, FontStyles.Normal, ref y, 110f);
            _rollHeight = -y;

            _skip = UiKit.Label(overlay.transform, "Skip", Strings.Get("credits.skip"), 30, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_skip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(800f, 60f));
            UiKit.Outline(_skip, 0.12f);

            _root.SetActive(false);
            _game.Sim.GoldenYearEnded += OnGoldenYearEnded;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) _game.Sim.GoldenYearEnded -= OnGoldenYearEnded;
        }

        private void Line(string key, int size, Color color, FontStyles style, ref float y, float height)
        {
            var t = UiKit.Label(_roll, key, Strings.Get(key), size, color, TextAnchor.MiddleCenter);
            t.fontStyle = style;
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(920f, height));
            UiKit.Outline(t, 0.14f);
            y -= height;
        }

        private void OnGoldenYearEnded()
        {
            Active = true;
            _t = 0f;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _skip.enabled = false;
            _pause.SetButtonVisible(false);
        }

        private void Update()
        {
            if (!Active) return;
            // The Winter screen opens right after the ending event; keep the roll above it.
            var tr = _root.transform;
            if (tr.GetSiblingIndex() != tr.parent.childCount - 1) tr.SetAsLastSibling();
            _t += Time.unscaledDeltaTime;
            float travel = _rootRt.rect.height + _rollHeight;
            _roll.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, travel, _t / RollSeconds));
            if (!_skip.enabled && _t >= SkipAfterSeconds) _skip.enabled = true;
            if (_t >= RollSeconds) Finish();
        }

        private void TrySkip()
        {
            if (Active && _t >= SkipAfterSeconds) Finish();
        }

        private void Finish()
        {
            Active = false;
            _root.SetActive(false);
            _pause.ShowStats(null); // Continue resumes; the Winter screen is already open underneath
        }
    }
}
