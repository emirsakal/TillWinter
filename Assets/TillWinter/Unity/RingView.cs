using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>Flat translucent disc with a soft edge that follows the ring input and pulses gently.</summary>
    public sealed class RingView : MonoBehaviour
    {
        private GameController _game;
        private Transform _disc;
        private Material _material;
        private float _alpha;
        private Vector3 _pos;
        private static readonly Color RingColor = new Color(1f, 0.95f, 0.65f, 1f);

        public void Init(GameController game)
        {
            _game = game;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "RingDisc";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _disc = go.transform;
            _material = Prims.MakeTransparent(Prims.Unlit(RingColor));
            _material.SetTexture("_BaseMap", Prims.RadialGradient(256, 0.55f, 1f));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            var ring = _game.CurrentRing;
            float dt = Time.deltaTime;
            bool visible = ring.HasValue && !_game.State.IsWinter;
            _alpha = Prims.Damp(_alpha, visible ? 1f : 0f, visible ? 18f : 10f, dt);

            if (ring.HasValue)
            {
                var target = _game.PlotToWorld(ring.Value.X, ring.Value.Y, 0.175f);
                _pos = _alpha < 0.05f ? target : Prims.Damp(_pos, target, 40f, dt);
            }

            if (_alpha < 0.01f)
            {
                if (_disc.gameObject.activeSelf) _disc.gameObject.SetActive(false);
                return;
            }
            if (!_disc.gameObject.activeSelf) _disc.gameObject.SetActive(true);

            float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 5f);
            float d = _game.State.RingRadius * 2f * 1.08f * pulse;
            _disc.position = _pos;
            _disc.localScale = new Vector3(d, d, 1f);
            var c = RingColor;
            c.a = _alpha * (0.32f + 0.06f * Mathf.Sin(Time.time * 5f));
            _material.SetColor(Prims.BaseColorId, c);
        }
    }
}
