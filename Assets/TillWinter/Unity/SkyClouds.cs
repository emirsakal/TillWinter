using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>
    /// A few stylised clouds drifting high above the farm so the sky is more than a gradient. They use the cloud
    /// prefab from the catalogue (recoloured with the season through the palette binder) and cast no shadows.
    /// </summary>
    public sealed class SkyClouds : MonoBehaviour
    {
        private const int Count = 3;

        private readonly Transform[] _clouds = new Transform[Count];
        private readonly float[] _speed = new float[Count];
        private float _span = 16f;

        public void Init(VisualCatalog catalog, float span = 16f)
        {
            _span = span;
            for (int i = 0; i < Count; i++)
            {
                var go = catalog.Spawn(catalog.Cloud, transform, "SkyCloud");
                if (go == null) return;
                var t = go.transform;
                float k = (i + 0.5f) / Count;
                t.localPosition = new Vector3(Mathf.Lerp(-_span, _span, k), 7.5f + i * 1.2f, 7.5f + i * 2.4f);
                t.localScale = Vector3.one * (1.7f + i * 0.45f);
                t.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                _clouds[i] = t;
                _speed[i] = 0.16f + i * 0.05f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                var t = _clouds[i];
                if (t == null) continue;
                var p = t.localPosition;
                p.x += _speed[i] * dt;
                if (p.x > _span) p.x = -_span; // wraps around, so the sky never empties
                t.localPosition = p;
            }
        }
    }
}
