using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// GDD §8 ending: when the Golden Year ends, the credits play over the field as three groups that fade in, hold
    /// and fade out (one scrolling wall of text read as a list), closing on the game's name in a seal. Skippable
    /// after 3 s; then the statistics sheet, with the normal Winter screen underneath.
    /// </summary>
    public sealed class EndingView : MonoBehaviour
    {
        public const float SkipAfterSeconds = 3f;
        private const float GroupSeconds = 6.5f;
        private const float FadeSeconds = 0.8f;

        private GameController _game;
        private PauseMenu _pause;
        private HudTheme _theme;
        private GameObject _root;
        private TMP_Text _skip;
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private float _t;

        public bool Active { get; private set; }

        public void Init(GameController game, RectTransform canvas, PauseMenu pause)
        {
            _game = game;
            _pause = pause;
            _theme = HudTheme.Load();

            var overlay = UiKit.Panel(canvas, "EndingCredits", _theme.CreditsOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tap = overlay.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(TrySkip);
            _root = overlay.gameObject;

            // 1: the Golden Year. 2: who made it. 3: the game's name in a seal.
            var first = Group(overlay.transform);
            Line(first, "ending.title", UiType.Hero, _theme.Gold, FontStyles.Bold, 90f);
            Line(first, "ending.caption", UiType.Heading, _theme.Text, FontStyles.Normal, -30f);

            var second = Group(overlay.transform);
            Line(second, "credits.made_by", UiType.Heading, _theme.Text, FontStyles.Bold, 240f);
            Line(second, "credits.unity", UiType.Body, _theme.TextMuted, FontStyles.Normal, 160f);
            Line(second, "credits.kenney", UiType.Body, _theme.Text, FontStyles.Normal, 40f);
            Line(second, "credits.kits_art", UiType.Label, _theme.TextMuted, FontStyles.Normal, -30f);
            Line(second, "credits.kits_audio", UiType.Label, _theme.TextMuted, FontStyles.Normal, -95f);
            Line(second, "credits.font", UiType.Label, _theme.Text, FontStyles.Normal, -190f);

            var third = Group(overlay.transform);
            var seal = UiKit.CircleImage(third.transform, "Seal", new Color(_theme.Gold.r, _theme.Gold.g, _theme.Gold.b, 0.16f), new Vector2(0f, 30f), 620f);
            UiKit.CircleImage(seal.transform, "SealInner", _theme.CreditsOverlay, Vector2.zero, 556f);
            Line(third, "menu.title", UiType.Display, _theme.Gold, FontStyles.Bold, 60f);
            Line(third, "credits.thanks", UiType.Heading, _theme.Text, FontStyles.Normal, -90f);
            Line(third, "ending.after", UiType.Label, _theme.TextMuted, FontStyles.Normal, -260f);

            _skip = UiKit.Label(overlay.transform, "Skip", Strings.Get("credits.skip"), UiType.Label, _theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_skip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(800f, 60f));
            UiKit.Outline(_skip, 0.12f);

            _root.SetActive(false);
            _game.Sim.GoldenYearEnded += OnGoldenYearEnded;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) _game.Sim.GoldenYearEnded -= OnGoldenYearEnded;
        }

        private CanvasGroup Group(Transform parent)
        {
            var rt = UiKit.Rect("Group" + _groups.Count, parent);
            UiKit.Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _groups.Add(group);
            return group;
        }

        private void Line(CanvasGroup group, string key, int size, Color color, FontStyles style, float y)
        {
            var t = UiKit.Label(group.transform, key, Strings.Get(key), size, color, TextAnchor.MiddleCenter);
            t.fontStyle = style;
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(940f, size * 2.4f));
            UiKit.Outline(t, 0.14f);
        }

        private void OnGoldenYearEnded()
        {
            Active = true;
            _t = 0f;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _skip.enabled = false;
            foreach (var g in _groups) g.alpha = 0f;
            _pause.SetButtonVisible(false);
        }

        private void Update()
        {
            if (!Active) return;
            // The Winter screen opens right after the ending event; keep the credits above it.
            var tr = _root.transform;
            if (tr.GetSiblingIndex() != tr.parent.childCount - 1) tr.SetAsLastSibling();

            _t += Time.unscaledDeltaTime;
            for (int i = 0; i < _groups.Count; i++)
            {
                float local = _t - i * GroupSeconds;
                float alpha = local <= 0f ? 0f
                    : local < FadeSeconds ? local / FadeSeconds
                    : local < GroupSeconds - FadeSeconds ? 1f
                    : local < GroupSeconds ? 1f - (local - (GroupSeconds - FadeSeconds)) / FadeSeconds
                    : 0f;
                if (i == _groups.Count - 1 && local >= FadeSeconds) alpha = 1f; // the last group holds until the tap
                _groups[i].alpha = Mathf.Clamp01(alpha);
            }
            if (!_skip.enabled && _t >= SkipAfterSeconds) _skip.enabled = true;
            if (_t >= _groups.Count * GroupSeconds + 2.5f) Finish();
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
