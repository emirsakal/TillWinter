using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Renders every apprentice in <see cref="TillWinter.Core.FarmState.Apprentices"/> from the catalogue's six character prefabs.</summary>
    public sealed class ApprenticesView : MonoBehaviour
    {
        private GameController _game;
        private VisualCatalog _catalog;
        private readonly List<ApprenticeView> _views = new List<ApprenticeView>();

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            _catalog = catalog;
        }

        private void LateUpdate()
        {
            var list = _game.State.Apprentices;
            while (_views.Count < list.Count)
            {
                int i = _views.Count;
                var prefab = _catalog.Apprentices != null && _catalog.Apprentices.Length > 0 ? _catalog.Apprentices[i % _catalog.Apprentices.Length] : null;
                var go = _catalog.Spawn(prefab, transform, "Apprentice");
                go.name = "Apprentice " + i;
                _views.Add(go.AddComponent<ApprenticeView>().Setup(_catalog));
            }
            while (_views.Count > list.Count)
            {
                Destroy(_views[_views.Count - 1].gameObject);
                _views.RemoveAt(_views.Count - 1);
            }
            for (int i = 0; i < list.Count; i++)
                _views[i].Tick(_game, list[i], Time.deltaTime);
        }
    }

    /// <summary>Character prefab with a hat. Bobs while walking, squashes while harvesting.</summary>
    public sealed class ApprenticeView : MonoBehaviour
    {
        private Transform _body;
        private Vector3 _lastPos;
        private float _bob;
        private float _facing;
        private bool _placed;
        private float _lastSin;

        public ApprenticeView Setup(VisualCatalog catalog = null)
        {
            // Everything spawned from the prefab moves under one body pivot so bob/squash apply to model + hat.
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var children = new List<Transform>();
            foreach (Transform c in transform) if (c != _body) children.Add(c);
            foreach (var c in children) c.SetParent(_body, false);
            // The contact shadow stays on the root, so it does not bob with the body.
            var shadow = catalog != null ? catalog.Spawn(catalog.BlobShadow, transform, "Shadow") : null;
            if (shadow != null)
            {
                shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                shadow.transform.localScale = Vector3.one * 0.5f;
            }
            return this;
        }

        public void Tick(GameController game, TillWinter.Core.ApprenticeState a, float dt)
        {
            var target = game.PlotToWorld(a.X, a.Y, 0.16f);
            if (!_placed)
            {
                _lastPos = target;
                _placed = true;
            }
            var delta = target - _lastPos;
            transform.position = target;

            bool walking = a.IsWalking && delta.sqrMagnitude > 1e-6f;
            if (walking)
            {
                float before = Mathf.Sin(_bob);
                _bob += dt * 12f;
                float after = Mathf.Sin(_bob);
                if ((before <= 0f) != (after <= 0f)) // each footfall
                {
                    VfxPlayer.Fire(VfxId.StepDust, target + Vector3.up * 0.02f);
                    AudioManager.Instance?.Play(SfxId.Step, 0.6f);
                }
                _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 14f));
            }
            float bobY = walking ? Mathf.Abs(Mathf.Sin(_bob)) * 0.07f : 0f;
            float squash = a.IsHarvesting ? 1f - 0.18f * Mathf.Sin(a.HarvestProgress * Mathf.PI) : 1f;
            _body.localPosition = new Vector3(0f, bobY, 0f);
            _body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            _body.localRotation = Quaternion.Euler(walking ? Mathf.Sin(_bob) * 4f : 0f, _facing, 0f);
            _lastPos = target;
        }
    }
}
