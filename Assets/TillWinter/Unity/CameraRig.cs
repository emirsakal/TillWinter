using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>Orthographic portrait camera that frames the field width with the top/bottom bands left free for UI.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public static CameraRig Instance { get; private set; }
        private float _shakeAmount, _shakeUntil, _shakeSeconds;
        public bool Shaking => Time.unscaledTime < _shakeUntil;

        /// <summary>Tilt from straight-down. 40 degrees reads as a gentle 3/4 view.</summary>
        public float TiltFromTopDown = 40f;
        public float Distance = 30f;
        public float SideMargin = 0.45f;
        /// <summary>World units added to the framed width so the diorama block edges stay on screen.</summary>
        public float ExtraWidth = 2.7f; // the island grew (DioramaView.Margin); without this its edges crop
        /// <summary>Where the field centre sits vertically (0 = bottom, 1 = top). Play band is 18%..80%.</summary>
        public float FieldScreenY = 0.49f;

        /// <summary>0..1 combo excitement: a millimetric push-in (set by FieldView).</summary>
        public float Excitement;
        /// <summary>Winter pulls the framing back a touch.</summary>
        public bool PulledBack;

        private float _targetSize = 4f;
        private float _zoom = 1f;
        private int _gridSize = 3;

        /// <summary>Camera micro-shake. Only golden harvest and retire may call this (CLAUDE.md feel rules).</summary>
        public void Shake(float amount, float seconds)
        {
            if (!SettingsStore.MotionAllowed) return; // reduce motion
            _shakeAmount = Mathf.Max(_shakeAmount, amount);
            _shakeSeconds = seconds;
            _shakeUntil = Time.unscaledTime + seconds;
        }

        public void Setup()
        {
            Instance = this;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Load().CameraClear;
            Cam.allowHDR = false;
            Cam.allowMSAA = false;
            go.AddComponent<AudioListener>();
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None;
            Frame(_gridSize, true);
        }

        public void Frame(int gridSize, bool instant)
        {
            _gridSize = gridSize;
            float width = gridSize + 2f * SideMargin + ExtraWidth;
            _targetSize = FitSize(width);
            if (instant) Cam.orthographicSize = _targetSize;
            Place();
        }

        private void LateUpdate()
        {
            // Aspect can change when the Game view preset changes; keep the fit live.
            float width = _gridSize + 2f * SideMargin + ExtraWidth;
            _targetSize = FitSize(width);
            // A slow breath, a push-in while a streak runs, a step back in Winter.
            float zoomTarget = 1f - 0.03f * Mathf.Clamp01(Excitement) + (PulledBack ? 0.05f : 0f);
            _zoom = Prims.Damp(_zoom, zoomTarget, 2.5f, Time.unscaledDeltaTime);
            float breath = 1f + Mathf.Sin(Time.unscaledTime * 0.35f) * 0.006f;
            Cam.orthographicSize = Prims.Damp(Cam.orthographicSize, _targetSize * _zoom * breath, 4f, Time.unscaledDeltaTime);
            Place();
        }

        /// <summary>The reference phone's width / height; the UI is laid out for it.</summary>
        public const float ReferenceAspect = 1080f / 2340f;

        /// <summary>
        /// Fits <paramref name="width"/> world units across the screen, but never shows less height than the reference
        /// phone would: on a wider screen (16:9, tablet) the field otherwise grew tall enough to run under the HUD.
        /// </summary>
        /// <summary>Starts from <paramref name="zoom"/> times the fit and eases back (a new generation's reveal).</summary>
        public void Settle(float zoom) => _zoom = zoom;

        public float FitSize(float width)
        {
            float aspect = Mathf.Max(0.2f, Cam.aspect);
            return width / (2f * Mathf.Min(aspect, ReferenceAspect));
        }

        private void Place()
        {
            var rot = Quaternion.Euler(90f - TiltFromTopDown, 0f, 0f);
            Cam.transform.rotation = rot;
            // Move the look-at point so the field centre lands at FieldScreenY of the screen.
            float shift = (0.5f - FieldScreenY) * 2f * Cam.orthographicSize;
            Vector3 lookAt = Vector3.zero + Cam.transform.up * shift;
            Cam.transform.position = lookAt - Cam.transform.forward * Distance;
            float left = _shakeUntil - Time.unscaledTime;
            if (left > 0f)
            {
                float k = _shakeAmount * (left / Mathf.Max(0.01f, _shakeSeconds));
                Cam.transform.position += Cam.transform.right * (Mathf.Sin(Time.unscaledTime * 71f) * k) + Cam.transform.up * (Mathf.Sin(Time.unscaledTime * 53f) * k * 0.6f);
            }
            else _shakeAmount = 0f;
        }
    }
}
