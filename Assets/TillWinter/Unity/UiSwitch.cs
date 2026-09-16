using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// A real on/off switch: a rounded track whose knob slides across when it flips. Settings used buttons whose
    /// label read "On"/"Off", which is a button pretending to be a state. Built through <see cref="UiKit.Switch"/>.
    /// </summary>
    public sealed class UiSwitch : MonoBehaviour, IPointerClickHandler
    {
        public event Action<bool> Changed;

        private RectTransform _knob;
        private Image _track;
        private Color _on, _off;
        private bool _value;
        private float _t;

        public bool Value => _value;

        internal void Init(Image track, RectTransform knob, Color on, Color off, bool value)
        {
            _track = track;
            _knob = knob;
            _on = on;
            _off = off;
            _value = value;
            _t = value ? 1f : 0f;
            Apply();
        }

        /// <summary>Sets the state; <paramref name="notify"/> false when following the settings file rather than a tap.</summary>
        public void Set(bool value, bool notify = false)
        {
            if (_value == value) return;
            _value = value;
            if (notify) Changed?.Invoke(value);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _value = !_value;
            Haptics.Play(HapticKind.Selection);
            AudioManager.Instance?.Play(SfxId.UiClick);
            Changed?.Invoke(_value);
        }

        // The knob used to be sized and placed once, in Init, before the caller had given the switch its size, and
        // never again unless the value changed: it sat as a 10 px dot in the wrong spot. It now follows the track.
        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange()
        {
            if (_knob != null) Apply();
        }

        private void Update()
        {
            float target = _value ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / UiMotion.Fast);
            Apply();
        }

        private void Apply()
        {
            if (_knob == null || _track == null) return;
            float e = UiMotion.EaseInOut(_t);
            var size = ((RectTransform)transform).rect;
            float knob = Mathf.Max(0f, size.height - 12f); // fills the track, with a 6 px rim of track colour around it
            _knob.sizeDelta = new Vector2(knob, knob);
            float travel = Mathf.Max(0f, size.width - size.height);
            _knob.anchoredPosition = new Vector2(size.height * 0.5f + travel * e, 0f);
            _track.color = Color.Lerp(_off, _on, e);
        }
    }
}
