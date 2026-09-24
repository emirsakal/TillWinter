using TillWinter.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TillWinter.Unity
{
    /// <summary>
    /// The beat halo (GDD §2v3.4): the field has one pulse, and the plot last struck wears it. A URP decal projected
    /// onto the plot breathes with <see cref="FarmState.Pulse"/> (wide between beats, tight and bright on the beat) so a
    /// player can read the timing off the ground. Falls back to a textured disc when the decal feature is not
    /// installed. With Reduce motion the halo stands still and only brightens on the beat.
    /// </summary>
    public sealed class BeatView : MonoBehaviour
    {
        private GameController _game;
        private Transform _halo;
        private DecalProjector _decal;
        private Material _discMaterial;
        private float _alpha;
        private Vector3 _pos;
        private GridPos? _shownAt;
        private float _bright;

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            var go = new GameObject("BeatHalo");
            go.transform.SetParent(transform, false);
            _halo = go.transform;
            if (catalog.UseDecalRing && catalog.RingDecal != null)
            {
                _decal = go.AddComponent<DecalProjector>();
                _decal.material = catalog.RingDecal;
                _decal.pivot = Vector3.zero;
                _decal.size = new Vector3(1.2f, 1.2f, 3f);
                _decal.drawDistance = 200f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                var quad = catalog.Spawn(catalog.RingDisc, go.transform, "Disc");
                _discMaterial = Prims.MakeTransparent(Prims.Unlit(Color.white));
                _discMaterial.SetTexture("_BaseMap", catalog.RingTexture != null ? catalog.RingTexture : Prims.RadialGradient(256, 0.55f, 1f));
                var r = quad.GetComponentInChildren<Renderer>();
                if (r != null) r.sharedMaterial = _discMaterial;
            }
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            var state = _game.State;
            float dt = Time.deltaTime;
            // The halo sits on the plot the finger is on, else where the last strike landed while that ground is still hard.
            GridPos? at = _game.PressedPlot ?? state.LastStrikePos;
            bool visible = at.HasValue && state.Phase == Phase.Year && state.InBounds(at.Value) && state.GetPlot(at.Value).IsHard;
            _alpha = Prims.Damp(_alpha, visible ? 1f : 0f, visible ? 18f : 6f, dt);
            if (_alpha < 0.01f)
            {
                if (_halo.gameObject.activeSelf) _halo.gameObject.SetActive(false);
                _shownAt = null;
                return;
            }
            if (!_halo.gameObject.activeSelf) _halo.gameObject.SetActive(true);
            if (at.HasValue)
            {
                var target = _game.PlotToWorld(at.Value, 0f);
                _pos = _shownAt.HasValue && _shownAt.Value != at.Value ? target : (_shownAt.HasValue ? Prims.Damp(_pos, target, 30f, dt) : target);
                _shownAt = at;
            }

            // 0 far from the beat, 1 on it; the window's width says how much of the pulse counts as "on".
            float env = 1f - Mathf.Clamp01(Mathf.Abs(state.Pulse - 0.5f) * 2f);
            float window = Mathf.Max(0.05f, state.Stats.CritWindow);
            bool moving = SettingsStore.MotionAllowed;
            float size = moving ? Mathf.Lerp(1.45f, 1.06f, Prims.EaseOutQuad(env)) : 1.12f;
            _bright = Prims.Damp(_bright, state.OnBeat ? 1f : 0f, 24f, dt);
            float fade = _alpha * Mathf.Lerp(0.45f, 1f, Mathf.Max(env * env, _bright));
            if (_decal != null)
            {
                _halo.position = _pos + Vector3.up * 1.5f;
                _decal.size = new Vector3(size, size, 3f);
                _decal.fadeFactor = fade;
            }
            else
            {
                _halo.position = _pos + Vector3.up * 0.24f; // just above the soil ridges
                _halo.localScale = new Vector3(size, size, 1f);
                var palette = Palette.Load();
                var c = Color.Lerp(palette.RingIdle, palette.RingCombo, _bright);
                c.a = fade;
                _discMaterial.SetColor("_BaseColor", c);
            }
            // A halo tighter than the window reads as "now": the window itself is what the steady hand widens.
            _ = window;
        }
    }
}
