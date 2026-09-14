using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Capsule with a straw hat. Bobs while walking, squashes while harvesting.</summary>
    public sealed class ApprenticeView : MonoBehaviour
    {
        private GameController _game;
        private Transform _body;
        private Vector3 _lastPos;
        private float _bob;
        private float _facing;

        public void Init(GameController game)
        {
            _game = game;
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            var shirt = Prims.Lit(new Color(0.32f, 0.5f, 0.8f), 0.2f);
            var skin = Prims.Lit(new Color(0.95f, 0.8f, 0.65f), 0.2f);
            var straw = Prims.Lit(new Color(0.9f, 0.78f, 0.4f), 0.1f);
            Prims.Primitive(PrimitiveType.Capsule, _body, "Torso", new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.24f, 0.24f), shirt);
            Prims.Primitive(PrimitiveType.Sphere, _body, "Head", new Vector3(0f, 0.6f, 0f), new Vector3(0.2f, 0.2f, 0.2f), skin);
            Prims.Primitive(PrimitiveType.Cylinder, _body, "Brim", new Vector3(0f, 0.68f, 0f), new Vector3(0.34f, 0.015f, 0.34f), straw);
            Prims.Primitive(PrimitiveType.Cylinder, _body, "Crown", new Vector3(0f, 0.73f, 0f), new Vector3(0.16f, 0.05f, 0.16f), straw);
            _body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var a = _game.State.Apprentice;
            if (!a.Owned)
            {
                if (_body.gameObject.activeSelf) _body.gameObject.SetActive(false);
                return;
            }
            if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);

            float dt = Time.deltaTime;
            var target = _game.PlotToWorld(a.X, a.Y, 0.16f);
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
