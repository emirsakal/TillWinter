using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TillWinter.Unity
{
    /// <summary>
    /// Single code path for mouse (Editor) and touch (device) via the Input System's <see cref="Pointer"/> device.
    /// Presses that start over UI are ignored entirely.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class PointerInput : MonoBehaviour
    {
        public struct Sample
        {
            public bool IsDown;
            public Vector2 Position;
            public bool Tapped;
            public Vector2 TapPosition;
        }

        public float TapMaxSeconds = 0.2f;
        public float TapMaxPixels = 20f;

        public Sample Current { get; private set; }

        private bool _rawWasDown;
        private bool _pressOverUi;
        private float _downTime;
        private Vector2 _downPos;
        private float _maxMove;

        private void Update()
        {
            var s = new Sample();
            var pointer = Pointer.current;
            if (pointer != null)
            {
                bool rawDown = pointer.press.isPressed;
                Vector2 pos = pointer.position.ReadValue();

                if (rawDown && !_rawWasDown)
                {
                    _downTime = Time.unscaledTime;
                    _downPos = pos;
                    _maxMove = 0f;
                    _pressOverUi = IsOverUi();
                }
                if (rawDown) _maxMove = Mathf.Max(_maxMove, (pos - _downPos).magnitude);

                bool released = !rawDown && _rawWasDown;
                if (released && !_pressOverUi && Time.unscaledTime - _downTime <= TapMaxSeconds && _maxMove <= TapMaxPixels)
                {
                    s.Tapped = true;
                    s.TapPosition = pos;
                }

                s.IsDown = rawDown && !_pressOverUi;
                s.Position = pos;
                _rawWasDown = rawDown;
            }
            else
            {
                _rawWasDown = false;
            }
            Current = s;
        }

        private static bool IsOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            try
            {
                return es.IsPointerOverGameObject();
            }
            catch
            {
                return false;
            }
        }
    }
}
