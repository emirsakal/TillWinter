using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TillWinter.Unity
{
    /// <summary>
    /// Single code path for mouse (Editor) and touch (device) via the Input System's <see cref="Pointer"/> device.
    /// Presses that start over UI are ignored entirely. The sample carries the press edges (down this frame, up this
    /// frame), how long the finger has been down and how far it has moved, so <see cref="GameController"/> can tell a
    /// strike from a hold from a swipe (GDD §2v3).
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class PointerInput : MonoBehaviour
    {
        public struct Sample
        {
            public bool IsDown;
            public Vector2 Position;
            /// <summary>The finger came down this frame (not over UI).</summary>
            public bool Pressed;
            /// <summary>The finger lifted this frame (from a press that was not over UI).</summary>
            public bool Released;
            public Vector2 DownPosition;
            public float HeldSeconds;
            /// <summary>Farthest the finger has been from where it came down, in pixels.</summary>
            public float MaxMove;
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
                    _pressOverUi = IsOverUi(pos);
                    s.Pressed = !_pressOverUi;
                }
                if (rawDown) _maxMove = Mathf.Max(_maxMove, (pos - _downPos).magnitude);

                bool released = !rawDown && _rawWasDown;
                if (released && !_pressOverUi)
                {
                    s.Released = true;
                    if (Time.unscaledTime - _downTime <= TapMaxSeconds && _maxMove <= TapMaxPixels)
                    {
                        s.Tapped = true;
                        s.TapPosition = pos;
                    }
                }

                s.IsDown = rawDown && !_pressOverUi;
                s.Position = pos;
                s.DownPosition = _downPos;
                s.HeldSeconds = rawDown ? Time.unscaledTime - _downTime : 0f;
                s.MaxMove = _maxMove;
                _rawWasDown = rawDown;
            }
            else
            {
                _rawWasDown = false;
            }
            Current = s;
        }

        private readonly List<RaycastResult> _hits = new List<RaycastResult>(8);
        private PointerEventData _probe;
        private EventSystem _probeSystem;

        /// <summary>
        /// Is there UI under <paramref name="position"/> right now? EventSystem.IsPointerOverGameObject() answers for
        /// where the pointer was last frame: on a touch screen the first press after tapping a button (Next Year)
        /// still counted as "over that button", so the whole press was lost. This raycasts the press position itself
        /// (once per press, reused buffers).
        /// </summary>
        private bool IsOverUi(Vector2 position)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_probe == null || _probeSystem != es)
            {
                _probe = new PointerEventData(es);
                _probeSystem = es;
            }
            _probe.position = position;
            _hits.Clear();
            es.RaycastAll(_probe, _hits);
            return _hits.Count > 0;
        }
    }
}
