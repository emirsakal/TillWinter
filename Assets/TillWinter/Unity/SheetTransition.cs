using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The shared open animation for every sheet and dialog (pause, settings, credits, stats, confirm, away card):
    /// the scrim fades in while the page rises and scales up over <see cref="Seconds"/>. Unscaled time, so it plays
    /// while the game is paused. Add it to the scrim object and point <see cref="Page"/> at the box inside it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SheetTransition : MonoBehaviour
    {
        public RectTransform Page;
        public float Seconds = UiMotion.Normal;
        public float Rise = 54f;

        private CanvasGroup _group;
        private Vector2 _base;
        private bool _captured;
        private float _t;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            Capture();
        }

        private void Capture()
        {
            if (_captured || Page == null) return;
            _base = Page.anchoredPosition;
            _captured = true;
        }

        private void OnEnable()
        {
            Capture();
            _t = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_t >= Seconds) return;
            _t += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(_t / Seconds));
        }

        private void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k);
            if (_group != null) _group.alpha = e;
            if (Page == null) return;
            Page.anchoredPosition = _base + new Vector2(0f, Rise * (1f - e));
            Page.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, e);
        }
    }
}
