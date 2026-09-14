using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>Orthographic portrait camera that frames the field width with the top/bottom bands left free for UI.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public UniversalAdditionalCameraData CamData { get; private set; }

        /// <summary>Tilt from straight-down. 40 degrees reads as a gentle 3/4 view.</summary>
        public float TiltFromTopDown = 40f;
        public float Distance = 30f;
        public float SideMargin = 0.45f;
        /// <summary>Where the field centre sits vertically (0 = bottom, 1 = top). Play band is 18%..80%.</summary>
        public float FieldScreenY = 0.49f;

        private float _targetSize = 4f;
        private int _gridSize = 3;

        public void Setup()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.55f, 0.75f, 0.55f);
            Cam.allowHDR = false;
            Cam.allowMSAA = false;
            go.AddComponent<AudioListener>();
            CamData = Cam.GetUniversalAdditionalCameraData();
            CamData.renderPostProcessing = true;
            CamData.renderShadows = true;
            CamData.antialiasing = AntialiasingMode.None;
            Frame(_gridSize, true);
        }

        public void Frame(int gridSize, bool instant)
        {
            _gridSize = gridSize;
            float width = gridSize + 2f * SideMargin;
            float aspect = Mathf.Max(0.2f, Cam.aspect);
            _targetSize = width / (2f * aspect);
            if (instant) Cam.orthographicSize = _targetSize;
            Place();
        }

        private void LateUpdate()
        {
            // Aspect can change when the Game view preset changes; keep the fit live.
            float width = _gridSize + 2f * SideMargin;
            float aspect = Mathf.Max(0.2f, Cam.aspect);
            _targetSize = width / (2f * aspect);
            Cam.orthographicSize = Prims.Damp(Cam.orthographicSize, _targetSize, 4f, Time.unscaledDeltaTime);
            Place();
        }

        private void Place()
        {
            var rot = Quaternion.Euler(90f - TiltFromTopDown, 0f, 0f);
            Cam.transform.rotation = rot;
            // Move the look-at point so the field centre lands at FieldScreenY of the screen.
            float shift = (0.5f - FieldScreenY) * 2f * Cam.orthographicSize;
            Vector3 lookAt = Vector3.zero + Cam.transform.up * shift;
            Cam.transform.position = lookAt - Cam.transform.forward * Distance;
        }
    }
}
