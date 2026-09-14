using TillWinter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>Rain cloud: flattened grey sphere drifting above the top row; tap target; rain burst on tap.</summary>
    public sealed class CloudView : MonoBehaviour
    {
        private GameController _game;
        private FxManager _fx;
        private AudioManager _audio;
        private Transform _body;
        private float _shown;
        private float _bob;

        public void Init(GameController game, FxManager fx, AudioManager audio)
        {
            _game = game;
            _fx = fx;
            _audio = audio;
            _body = new GameObject("CloudBody").transform;
            _body.SetParent(transform, false);
            var grey = Prims.Lit(new Color(0.62f, 0.66f, 0.72f), 0.1f);
            Prims.Primitive(PrimitiveType.Sphere, _body, "A", new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.5f, 0.7f), grey);
            Prims.Primitive(PrimitiveType.Sphere, _body, "B", new Vector3(-0.4f, 0.1f, 0.05f), new Vector3(0.7f, 0.45f, 0.55f), grey);
            Prims.Primitive(PrimitiveType.Sphere, _body, "C", new Vector3(0.4f, 0.12f, -0.05f), new Vector3(0.75f, 0.5f, 0.6f), grey);
            foreach (var r in _body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            _body.gameObject.SetActive(false);
            _game.Sim.RainCloudTapped += OnTapped;
            _game.CloudHitTest = HitTest;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) _game.Sim.RainCloudTapped -= OnTapped;
        }

        /// <summary>World position of the cloud for the current sim state.</summary>
        private Vector3 WorldPos()
        {
            var c = _game.State.Cloud;
            int n = _game.State.GridSize;
            return _game.PlotToWorld(-0.8f + c.X * (n - 1 + 1.6f), n - 0.2f, 1.8f);
        }

        private bool HitTest(Vector2 screen)
        {
            if (!_game.State.Cloud.Active || _game.Cam == null) return false;
            var sp = _game.Cam.WorldToScreenPoint(WorldPos());
            float radius = Screen.height * 0.06f;
            return (new Vector2(sp.x, sp.y) - screen).sqrMagnitude <= radius * radius;
        }

        private void OnTapped()
        {
            _fx.RainBurst(WorldPos() + Vector3.down * 0.6f, _game.State.GridSize);
            _audio.Play(SfxId.WaterSplash);
        }

        private void LateUpdate()
        {
            var c = _game.State.Cloud;
            bool active = c.Active && _game.State.Phase == Phase.Year;
            _shown = Prims.Damp(_shown, active ? 1f : 0f, 8f, Time.deltaTime);
            if (_shown < 0.02f)
            {
                if (_body.gameObject.activeSelf) _body.gameObject.SetActive(false);
                return;
            }
            if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);
            _bob += Time.deltaTime;
            _body.position = WorldPos() + Vector3.up * Mathf.Sin(_bob * 2f) * 0.08f;
            _body.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Prims.EaseOutQuad(_shown));
        }
    }

    /// <summary>Tractor: box with four small cylinders driving along the swept row.</summary>
    public sealed class TractorView : MonoBehaviour
    {
        private GameController _game;
        private Transform _body;
        private float _shown;
        private float _wheelSpin;

        public void Init(GameController game)
        {
            _game = game;
            _body = new GameObject("TractorBody").transform;
            _body.SetParent(transform, false);
            var red = Prims.Lit(new Color(0.8f, 0.2f, 0.15f), 0.3f);
            var dark = Prims.Lit(new Color(0.15f, 0.15f, 0.17f), 0.2f);
            Prims.Primitive(PrimitiveType.Cube, _body, "Hull", new Vector3(0f, 0.28f, 0f), new Vector3(0.5f, 0.22f, 0.34f), red);
            Prims.Primitive(PrimitiveType.Cube, _body, "Cab", new Vector3(-0.1f, 0.48f, 0f), new Vector3(0.22f, 0.2f, 0.28f), red);
            foreach (var (x, z) in new[] { (-0.17f, -0.17f), (-0.17f, 0.17f), (0.17f, -0.17f), (0.17f, 0.17f) })
            {
                var w = Prims.Primitive(PrimitiveType.Cylinder, _body, "Wheel", new Vector3(x, 0.12f, z), new Vector3(0.24f, 0.04f, 0.24f), dark);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            _body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var t = _game.State.Tractor;
            bool visible = t.Owned && _game.State.Phase == Phase.Year;
            _shown = Prims.Damp(_shown, visible ? 1f : 0f, 8f, Time.deltaTime);
            if (_shown < 0.02f)
            {
                if (_body.gameObject.activeSelf) _body.gameObject.SetActive(false);
                return;
            }
            if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);
            // Parked left of the field between sweeps, on the row while sweeping.
            float x = t.Sweeping ? t.X : -1.1f;
            float y = t.Sweeping ? t.Row : -0.6f;
            var target = _game.PlotToWorld(x, y, 0.16f);
            _body.position = t.Sweeping ? target : Prims.Damp(_body.position, target, 6f, Time.deltaTime);
            if (t.Sweeping) _wheelSpin += Time.deltaTime * 720f;
            _body.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _body.localScale = Vector3.one * _shown;
            foreach (Transform child in _body)
                if (child.name == "Wheel") child.localRotation = Quaternion.Euler(90f, 0f, _wheelSpin);
        }
    }

    /// <summary>Greenhouse: small translucent box beside the field, coin trickle particles while it accrues in Winter.</summary>
    public sealed class GreenhouseView : MonoBehaviour
    {
        private GameController _game;
        private FxManager _fx;
        private Transform _body;
        private float _trickle;

        public void Init(GameController game, FxManager fx)
        {
            _game = game;
            _fx = fx;
            _body = new GameObject("GreenhouseBody").transform;
            _body.SetParent(transform, false);
            var glass = Prims.MakeTransparent(Prims.Lit(new Color(0.7f, 0.9f, 1f, 0.35f), 0.8f));
            var frame = Prims.Lit(new Color(0.9f, 0.9f, 0.92f), 0.3f);
            Prims.Primitive(PrimitiveType.Cube, _body, "Glass", new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.7f), glass);
            Prims.Primitive(PrimitiveType.Cube, _body, "Ridge", new Vector3(0f, 0.72f, 0f), new Vector3(0.95f, 0.05f, 0.08f), frame);
            Prims.Primitive(PrimitiveType.Cube, _body, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.04f, 0.75f), frame);
            _body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var s = _game.State;
            bool owned = s.Stats.GreenhouseLevel > 0;
            if (_body.gameObject.activeSelf != owned) _body.gameObject.SetActive(owned);
            if (!owned) return;
            _body.position = _game.PlotToWorld(s.GridSize + 0.4f, 0.5f, 0f);
            if (s.Phase == Phase.Winter && s.Greenhouse.SecondsLeftThisWinter > 0f)
            {
                _trickle += Time.deltaTime;
                if (_trickle > 0.35f)
                {
                    _trickle = 0f;
                    _fx.CoinTrickle(_body.position + Vector3.up * 0.8f);
                }
            }
        }
    }
}
