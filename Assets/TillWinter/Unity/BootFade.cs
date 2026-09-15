using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>First frame: a paper-coloured cover that fades out in 0.6 s (real time), so the diorama fades in with no loading text.</summary>
    public sealed class BootFade : MonoBehaviour
    {
        public const float Seconds = 0.6f;
        private Image _cover;
        private float _start;

        public void Init(RectTransform canvas)
        {
            _cover = UiKit.Panel(canvas, "BootFade", HudTheme.Load().BootFade, false, false);
            UiKit.Stretch(_cover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cover.transform.SetAsLastSibling();
            _start = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_cover == null) return;
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _start) / Seconds);
            var c = _cover.color;
            c.a = 1f - t;
            _cover.color = c;
            if (t >= 1f)
            {
                Destroy(_cover.gameObject);
                Destroy(this);
            }
        }
    }
}
