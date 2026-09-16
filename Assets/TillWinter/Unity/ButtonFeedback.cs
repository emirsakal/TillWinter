using UnityEngine;
using UnityEngine.EventSystems;

namespace TillWinter.Unity
{
    /// <summary>
    /// The press on every button: a quick squeeze, the click sound and a Selection haptic, from one place instead of
    /// a colour tint here and a hand-written <c>Play(SfxId.UiClick)</c> there. Added by <see cref="UiKit.Button"/>.
    /// </summary>
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float Pressed = 0.96f;

        private Vector3 _base = Vector3.one;
        private float _t = 1f;
        private bool _down;

        private void OnEnable()
        {
            _t = 1f;
            transform.localScale = _base;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            _t = 0f;
            transform.localScale = _base * Pressed;
            Haptics.Play(HapticKind.Selection);
            AudioManager.Instance?.Play(SfxId.UiClick);
        }

        public void OnPointerUp(PointerEventData eventData) => _down = false;

        private void Update()
        {
            if (_down || _t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / UiMotion.Fast);
            transform.localScale = _base * Mathf.Lerp(Pressed, 1f, UiMotion.EaseOut(_t));
        }
    }
}
