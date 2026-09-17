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
        private Transform _dots;
        private Transform[] _dotItems;
        private Vector3[] _dotUnit;
        private float _dotRadius = -1f;
        private float _spin;
        private PaletteBinder _dotsBinder;
        private Color _dotColor = Color.white;

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

            // Beads that walk around the ring's edge (faster with the combo).
            if (catalog.RingDots != null)
            {
                _dots = catalog.Spawn(catalog.RingDots, transform, "RingDots").transform;
                _dotsBinder = _dots.GetComponent<PaletteBinder>();
                _dotItems = new Transform[_dots.childCount];
                _dotUnit = new Vector3[_dotItems.Length];
                for (int i = 0; i < _dotItems.Length; i++)
                {
                    _dotItems[i] = _dots.GetChild(i);
                    _dotUnit[i] = _dotItems[i].localPosition;
                }
                _dots.gameObject.SetActive(false);
            }
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
                if (_dots != null && _dots.gameObject.activeSelf) _dots.gameObject.SetActive(false);
                return;
            }
            if (!_ring.gameObject.activeSelf) _ring.gameObject.SetActive(true);
            if (ring.HasValue)
            {
                var target = _game.PlotToWorld(ring.Value.X, ring.Value.Y, 0f);
                _pos = _ring.gameObject.activeSelf && _alpha > 0.5f ? Prims.Damp(_pos, target, 30f, dt) : target;
            }
            float combo = Mathf.Clamp01((_game.State.Combo - 1) / 8f);
            float pulse = 1f + (0.03f + 0.03f * combo) * Mathf.Sin(Time.time * (5f + 3f * combo));
            float d = _game.State.RingRadius * 2f * 1.08f * pulse;
            if (_dots != null)
            {
                if (!_dots.gameObject.activeSelf) _dots.gameObject.SetActive(true);
                // Water blue while the ring waters, sunny while it grows, the crop's colour when it harvests.
                if (_dotsBinder != null && ring.HasValue)
                {
                    var palette = Palette.Load();
                    var state = _game.State;
                    var gp = new TillWinter.Core.GridPos(Mathf.RoundToInt(ring.Value.X), Mathf.RoundToInt(ring.Value.Y));
                    var want = palette.Get(PaletteSlot.Cloud);
                    if (state.InBounds(gp))
                    {
                        var plot = state.GetPlot(gp);
                        want = plot.State == TillWinter.Core.PlotState.Dry ? palette.Get(PaletteSlot.Water)
                            : plot.State == TillWinter.Core.PlotState.Wet ? palette.Get(PaletteSlot.Crop5)
                            : palette.Crop(plot.Tier);
                    }
                    _dotColor = Prims.Damp(_dotColor, want, 10f, dt);
                    _dotsBinder.Override(PaletteSlot.Cloud, _dotColor);
                }
                _spin += dt * (40f + 60f * combo);
                _dots.SetPositionAndRotation(_pos + Vector3.up * 0.3f, Quaternion.Euler(0f, _spin, 0f));
                // The beads sit just inside the soft edge; they shrink in with the ring as it fades.
                float radius = d * 0.47f * Mathf.Lerp(0.6f, 1f, _alpha);
                if (Mathf.Abs(radius - _dotRadius) > 0.001f)
                {
                    _dotRadius = radius;
                    for (int i = 0; i < _dotItems.Length; i++) _dotItems[i].localPosition = _dotUnit[i] * radius;
                }
            }
            if (_decal != null)
            {
                _ring.position = _pos + Vector3.up * 1.5f;
                _decal.size = new Vector3(d, d, 3f);
                _decal.fadeFactor = _alpha;
            }
            else
            {
                _ring.position = _pos + Vector3.up * 0.24f; // just above the soil ridges (0.21)
                _ring.localScale = Vector3.one * d;
                var c = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.55f), combo);
                c.a = Mathf.Min(1f, (0.85f + 0.15f * combo) * _alpha);
                _discMaterial.SetColor("_BaseColor", c);
            }
        }
    }
}
