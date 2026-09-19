using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Spawns the generation decor from <see cref="FarmDecorSet"/> through the catalogue (item prefab, else the catalogue entry for its kind). Items pop in staggered; static-batched after each rebuild.</summary>
    public sealed class FarmDecorView : MonoBehaviour
    {
        private GameController _game;
        private FarmDecorSet _set;
        private VisualCatalog _catalog;
        private readonly List<(Transform t, float delay, float scale)> _items = new List<(Transform, float, float)>();
        private float _reveal = 99f;

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            _catalog = catalog;
            _set = FarmDecorSet.Load();
            Rebuild(false);
            _game.Sim.GenerationStarted += () => Rebuild(true);
            _game.Sim.Retired += _ => Rebuild(false);
            _game.Sim.FieldExpanded += () => Rebuild(false);
        }

        private Transform _setRoot;

        /// <summary>Rebuilds for the current generation; with <paramref name="animate"/> items pop in one after another.</summary>
        public void Rebuild(bool animate)
        {
            // A fresh root each time: Destroy is deferred to the end of the frame, and batching the old root now would
            // fold the dying, already-batched decor into the new batch (DioramaView does the same).
            if (_setRoot != null) Destroy(_setRoot.gameObject);
            _setRoot = new GameObject("DecorSet").transform;
            _setRoot.SetParent(transform, false);
            _items.Clear();
            int gen = _game.State.Generation.Generation;
            float half = _game.State.GridSize * 0.5f;
            float delay = 0f;
            int tree = 0;
            foreach (var item in _set.ForGeneration(gen))
            {
                var root = new GameObject("Decor " + item.Id).transform;
                root.SetParent(_setRoot, false);
                // Offsets are relative to the field edge so decor scales with the field.
                float x = Mathf.Sign(item.Offset.x) * (half + Mathf.Abs(item.Offset.x) - 0.5f) * (Mathf.Abs(item.Offset.x) > 0.5f ? 1f : 0f) + (Mathf.Abs(item.Offset.x) <= 0.5f ? item.Offset.x : 0f);
                float z = item.Offset.y > 0f ? half + item.Offset.y - 0.5f : -half + item.Offset.y + 0.5f;
                // Mirrored left-right with the farmhouse (it stands on the right), so decor keeps its spacing from the house.
                root.localPosition = new Vector3(-x, 0f, z);
                root.localRotation = Quaternion.Euler(0f, -item.Rotation, 0f);
                var prefab = item.Prefab != null ? item.Prefab : _catalog.Decor(item.Kind, item.Kind == DecorKind.Tree ? tree++ : 0);
                _catalog.Spawn(prefab, root, item.Id);
                root.localScale = Vector3.one * (animate ? 0.001f : item.Scale);
                _items.Add((root, delay, item.Scale));
                delay += 0.12f;
            }
            _reveal = animate ? 0f : 99f;
            if (!animate) StaticBatchingUtility.Combine(_setRoot.gameObject);
        }

        /// <summary>Skip the pop-in.</summary>
        public void CompleteReveal()
        {
            _reveal = 99f;
            foreach (var (t, _, scale) in _items) t.localScale = Vector3.one * scale;
        }

        private void Update()
        {
            if (_reveal >= 99f) return;
            _reveal += Time.deltaTime;
            bool done = true;
            foreach (var (t, delay, scale) in _items)
            {
                float p = Mathf.Clamp01((_reveal - delay) / 0.3f);
                if (p < 1f) done = false;
                float s = p < 1f ? scale * Mathf.Lerp(0.001f, 1.15f, Prims.EaseOutQuad(p)) : scale;
                t.localScale = Vector3.one * Mathf.Max(0.001f, s);
            }
            if (done)
            {
                _reveal = 99f;
                if (_setRoot != null) StaticBatchingUtility.Combine(_setRoot.gameObject);
            }
        }
    }

    /// <summary>Slow rotation for the windmill blades.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        private void Update() => transform.Rotate(0f, 0f, 40f * Time.deltaTime, Space.Self);
    }
}
