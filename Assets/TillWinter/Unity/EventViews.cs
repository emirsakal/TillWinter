using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Rain cloud prefab drifting above the top row; tap target; rain burst on tap.</summary>
    public sealed class CloudView : MonoBehaviour
    {
        private GameController _game;
        private VfxPlayer _fx;
        private AudioManager _audio;
        private Transform _body;
        private float _shown;
        private float _bob;

        public void Init(GameController game, VfxPlayer fx, AudioManager audio, VisualCatalog catalog)
        {
            _game = game;
            _fx = fx;
            _audio = audio;
            _body = catalog.Spawn(catalog.Cloud, transform, "Cloud").transform;
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
            _audio.Play(SfxId.CloudTap);
            Haptics.Play(HapticKind.Selection);
        }

        private void LateUpdate()
        {
            var c = _game.State.Cloud;
            bool active = c.Active && _game.State.Phase == Phase.Year;
            _shown = Prims.Damp(_shown, active ? 1f : 0f, 8f, Time.deltaTime);
            if (_shown < 0.02f)
            {
                if (_body.gameObject.activeSelf) _body.gameObject.SetActive(false);
                _fx.SetRate(VfxId.RainDrops, 0f);
                return;
            }
            if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);
            _bob += Time.deltaTime;
            _body.position = WorldPos() + Vector3.up * Mathf.Sin(_bob * 2f) * 0.08f;
            _fx.SetRate(VfxId.RainDrops, active ? 30f * _shown : 0f, _body.position + Vector3.down * 0.45f);
            _body.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Prims.EaseOutQuad(_shown));
        }
    }

    /// <summary>Tractor prefab driving along the swept row; wheels (children named "Wheel") spin.</summary>
    public sealed class TractorView : MonoBehaviour
    {
        private GameController _game;
        private VfxPlayer _fx;
        private AudioManager _audio;
        private Transform _body;
        private float _shown;
        private float _wheelSpin;
        private float _dustTimer;

        public void Init(GameController game, VisualCatalog catalog, VfxPlayer fx, AudioManager audio)
        {
            _game = game;
            _fx = fx;
            _audio = audio;
            _game.Sim.TractorSweepStarted += OnSweepStarted;
            _body = catalog.Spawn(catalog.Tractor, transform, "Tractor").transform;
            _body.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) _game.Sim.TractorSweepStarted -= OnSweepStarted;
        }

        private void OnSweepStarted(int row)
        {
            _fx.Play(VfxId.TractorExhaust, _body.position + Vector3.up * 0.55f);
            _audio.Play(SfxId.TractorStart);
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
            if (t.Sweeping)
            {
                _wheelSpin += Time.deltaTime * 720f;
                _dustTimer += Time.deltaTime;
                if (_dustTimer > 0.08f) { _dustTimer = 0f; _fx.Play(VfxId.TractorDust, _body.position + new Vector3(-0.25f, 0.05f, 0f)); }
            }
            _body.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _body.localScale = Vector3.one * _shown;
            foreach (Transform child in _body)
                if (child.name == "Wheel") child.localRotation = Quaternion.Euler(90f, 0f, _wheelSpin);
        }
    }

    /// <summary>Greenhouse prefab beside the field, coin trickle particles while it accrues in Winter.</summary>
    public sealed class GreenhouseView : MonoBehaviour
    {
        private GameController _game;
        private VfxPlayer _fx;
        private Transform _body;
        private float _trickle;

        public void Init(GameController game, VfxPlayer fx, VisualCatalog catalog)
        {
            _game = game;
            _fx = fx;
            _body = catalog.Spawn(catalog.Greenhouse, transform, "Greenhouse").transform;
            _body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var s = _game.State;
            bool owned = s.Stats.GreenhouseLevel > 0;
            if (_body.gameObject.activeSelf != owned) _body.gameObject.SetActive(owned);
            if (!owned) return;
            _body.position = _game.PlotToWorld(s.GridSize + 0.5f, 0.5f, 0f);
            if (s.Phase == Phase.Winter && s.Greenhouse.SecondsLeftThisWinter > 0f)
            {
                _trickle += Time.deltaTime;
                if (_trickle > 0.35f)
                {
                    _trickle = 0f;
                    _fx.Play(VfxId.RipeSparkle, _body.position + Vector3.up * 0.8f, 0.6f);
                }
            }
        }
    }
}
