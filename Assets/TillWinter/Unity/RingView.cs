using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>
    /// The ring under the finger: a URP decal projected onto plots and ground (soft edge, subtle inner glow),
    /// pulsing gently. Falls back to a textured disc when the decal feature is not installed.
    /// </summary>
    public sealed class RingView : MonoBehaviour
    {
        private GameController _game;
        private Transform _ring;
        private DecalProjector _decal;
        private Material _discMaterial;
        private float _alpha;
        private Vector3 _pos;

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            var go = new GameObject("RingDecal");
            go.transform.SetParent(transform, false);
            _ring = go.transform;
            if (catalog.UseDecalRing && catalog.RingDecal != null)
            {
                _decal = go.AddComponent<DecalProjector>();
                _decal.material = catalog.RingDecal;
                _decal.pivot = Vector3.zero;
                _decal.size = new Vector3(2f, 2f, 3f);
                _decal.drawDistance = 200f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Disc";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _discMaterial = Prims.MakeTransparent(Prims.Unlit(Color.white));
                _discMaterial.SetTexture("_BaseMap", catalog.RingTexture != null ? catalog.RingTexture : Prims.RadialGradient(256, 0.55f, 1f));
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = _discMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            var ring = _game.CurrentRing;
            float dt = Time.deltaTime;
            bool visible = ring.HasValue && !_game.State.IsWinter;
            _alpha = Prims.Damp(_alpha, visible ? 1f : 0f, visible ? 18f : 10f, dt);
            if (_alpha < 0.01f)
            {
                if (_ring.gameObject.activeSelf) _ring.gameObject.SetActive(false);
                return;
            }
            if (!_ring.gameObject.activeSelf) _ring.gameObject.SetActive(true);
            if (ring.HasValue)
            {
                var target = _game.PlotToWorld(ring.Value.X, ring.Value.Y, 0f);
                _pos = _ring.gameObject.activeSelf && _alpha > 0.5f ? Prims.Damp(_pos, target, 30f, dt) : target;
            }
            float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 5f);
            float d = _game.State.RingRadius * 2f * 1.08f * pulse;
            if (_decal != null)
            {
                _ring.position = _pos + Vector3.up * 1.5f;
                _decal.size = new Vector3(d, d, 3f);
                _decal.fadeFactor = _alpha;
            }
            else
            {
                _ring.position = _pos + Vector3.up * 0.27f; // above lifted plots (soil top 0.16 + 0.05 lift)
                _ring.localScale = Vector3.one * d;
                var c = Color.white;
                c.a = 0.95f * _alpha;
                _discMaterial.SetColor("_BaseColor", c);
            }
        }
    }
}
