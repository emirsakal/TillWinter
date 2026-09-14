using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Renders every apprentice in <see cref="TillWinter.Core.FarmState.Apprentices"/>: one capsule each, different hat colours.</summary>
    public sealed class ApprenticesView : MonoBehaviour
    {
        private static readonly Color[] HatColors =
        {
            new Color(0.9f, 0.78f, 0.4f), new Color(0.85f, 0.35f, 0.3f), new Color(0.35f, 0.6f, 0.85f),
            new Color(0.5f, 0.75f, 0.35f), new Color(0.75f, 0.45f, 0.8f), new Color(0.95f, 0.6f, 0.2f),
        };

        private GameController _game;
        private readonly List<ApprenticeView> _views = new List<ApprenticeView>();
        private Material _shirt, _skin;

        public void Init(GameController game)
        {
            _game = game;
            _shirt = Prims.Lit(new Color(0.32f, 0.5f, 0.8f), 0.2f);
            _skin = Prims.Lit(new Color(0.95f, 0.8f, 0.65f), 0.2f);
        }

        private void LateUpdate()
        {
            var list = _game.State.Apprentices;
            while (_views.Count < list.Count)
            {
                var go = new GameObject("Apprentice " + _views.Count);
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<ApprenticeView>();
                v.Build(_shirt, _skin, Prims.Lit(HatColors[_views.Count % HatColors.Length], 0.1f));
                _views.Add(v);
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

    /// <summary>Capsule with a hat. Bobs while walking, squashes while harvesting.</summary>
    public sealed class ApprenticeView : MonoBehaviour
    {
        private Transform _body;
        private Vector3 _lastPos;
        private float _bob;
        private float _facing;
        private bool _placed;

        public void Build(Material shirt, Material skin, Material hat)
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            Prims.Primitive(PrimitiveType.Capsule, _body, "Torso", new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.24f, 0.24f), shirt);
            Prims.Primitive(PrimitiveType.Sphere, _body, "Head", new Vector3(0f, 0.6f, 0f), new Vector3(0.2f, 0.2f, 0.2f), skin);
            Prims.Primitive(PrimitiveType.Cylinder, _body, "Brim", new Vector3(0f, 0.68f, 0f), new Vector3(0.34f, 0.015f, 0.34f), hat);
            Prims.Primitive(PrimitiveType.Cylinder, _body, "Crown", new Vector3(0f, 0.73f, 0f), new Vector3(0.16f, 0.05f, 0.16f), hat);
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
                _bob += dt * 12f;
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
