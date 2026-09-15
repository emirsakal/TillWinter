using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>One crow prefab per landed crow; lands from above, hops, pecks harder as it eats, flies off when scared or done.</summary>
    public sealed class CrowsView : MonoBehaviour
    {
        private GameController _game;
        private VfxPlayer _fx;
        private AudioManager _audio;
        private VisualCatalog _catalog;
        private readonly Dictionary<GridPos, CrowView> _crows = new Dictionary<GridPos, CrowView>();
        private readonly List<CrowView> _leaving = new List<CrowView>();

        public void Init(GameController game, VfxPlayer fx, AudioManager audio, VisualCatalog catalog)
        {
            _game = game;
            _fx = fx;
            _audio = audio;
            _catalog = catalog;
            _game.Sim.CrowLanded += OnLanded;
            _game.Sim.CrowScared += OnScared;
            _game.Sim.CrowAte += OnAte;
            _game.Sim.WinterStarted += OnWinter;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.CrowLanded -= OnLanded;
            _game.Sim.CrowScared -= OnScared;
            _game.Sim.CrowAte -= OnAte;
            _game.Sim.WinterStarted -= OnWinter;
        }

        private void OnLanded(CrowEvent e)
        {
            if (_crows.ContainsKey(e.Pos)) return;
            var go = new GameObject("Crow " + e.Pos);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<CrowView>();
            view.Build(_catalog);
            view.Land(_game.PlotToWorld(e.Pos, 0.16f));
            _crows.Add(e.Pos, view);
            _audio.Play(SfxId.CrowCaw);
            _fx.Play(VfxId.Feathers, _game.PlotToWorld(e.Pos, 0.5f), 0.6f);
        }

        private void OnScared(CrowEvent e)
        {
            if (!_crows.TryGetValue(e.Pos, out var view)) return;
            _crows.Remove(e.Pos);
            view.FlyOff(true);
            _leaving.Add(view);
            _audio.Play(SfxId.CrowScared);
            _fx.Play(VfxId.Feathers, _game.PlotToWorld(e.Pos, 0.4f), 1.2f);
            Haptics.Play(HapticKind.Selection);
        }

        private void OnAte(CrowEvent e)
        {
            if (!_crows.TryGetValue(e.Pos, out var view)) return;
            _crows.Remove(e.Pos);
            view.FlyOff(false);
            _leaving.Add(view);
            _audio.Play(SfxId.CrowCaw);
        }

        private void OnWinter()
        {
            foreach (var kv in _crows)
            {
                kv.Value.FlyOff(false);
                _leaving.Add(kv.Value);
            }
            _crows.Clear();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var state = _game.State;
            foreach (var kv in _crows)
            {
                float progress = 0f;
                var crows = state.Crows;
                for (int ci = 0; ci < crows.Count; ci++)
                    if (crows[ci].Pos == kv.Key) { progress = crows[ci].Progress; break; }
                kv.Value.Tick(dt, progress);
            }
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                if (_leaving[i].TickLeaving(dt))
                {
                    Destroy(_leaving[i].gameObject);
                    _leaving.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>Crow prefab (children "WingL"/"WingR" flap). Lands from above, hops, shakes while eating.</summary>
    public sealed class CrowView : MonoBehaviour
    {
        private Transform _body, _wingL, _wingR;
        private Vector3 _home;
        private float _t;
        private float _landT;
        private float _leaveT = -1f;
        private float _phase;

        public void Build(VisualCatalog catalog)
        {
            _body = catalog.Spawn(catalog.Crow, transform, "Crow").transform;
            _wingL = _body.Find("WingL") ?? _body;
            _wingR = _body.Find("WingR") ?? _body;
            _phase = Random.value * 10f;
        }

        public void Land(Vector3 at)
        {
            _home = at;
            transform.position = at + Vector3.up * 2.5f;
            _landT = 0f;
            transform.localRotation = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f);
        }

        public void FlyOff(bool scared)
        {
            _leaveT = 0f;
            if (scared) _body.localScale = Vector3.one * 1.15f;
        }

        public void Tick(float dt, float eatProgress)
        {
            _t += dt;
            _landT = Mathf.Min(1f, _landT + dt / 0.35f);
            float drop = 1f - Prims.EaseOutQuad(_landT);
            float hop = Mathf.Abs(Mathf.Sin(_t * 7f + _phase)) * 0.08f;
            float shake = eatProgress * eatProgress * 0.06f;
            var jitter = new Vector3(Mathf.Sin(_t * 41f) * shake, 0f, Mathf.Cos(_t * 37f) * shake);
            transform.position = _home + Vector3.up * (drop * 2.5f + hop) + jitter;
            float peck = eatProgress > 0.3f ? Mathf.Max(0f, Mathf.Sin(_t * 9f)) * 25f * eatProgress : 0f;
            _body.localRotation = Quaternion.Euler(peck, 0f, Mathf.Sin(_t * 47f) * shake * 120f);
            float wing = drop > 0.05f ? Mathf.Sin(_t * 30f) * 40f : 0f;
            _wingL.localRotation = Quaternion.Euler(0f, 0f, wing);
            _wingR.localRotation = Quaternion.Euler(0f, 0f, -wing);
        }

        /// <summary>Returns true when finished.</summary>
        public bool TickLeaving(float dt)
        {
            if (_leaveT < 0f) return false;
            _leaveT += dt;
            _t += dt;
            float t = _leaveT / 0.7f;
            transform.position = _home + new Vector3(0.6f * t, 2.8f * t * t + 0.3f * t, -0.4f * t);
            float wing = Mathf.Sin(_t * 40f) * 55f;
            _wingL.localRotation = Quaternion.Euler(0f, 0f, wing);
            _wingR.localRotation = Quaternion.Euler(0f, 0f, -wing);
            _body.localRotation = Quaternion.Euler(-20f * t, 0f, 0f);
            return t >= 1f;
        }
    }
}
