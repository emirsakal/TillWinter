using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>Builds the generation decor from <see cref="FarmDecorSet"/> out of primitives (prefab slot honoured when set). Items pop in staggered.</summary>
    public sealed class FarmDecorView : MonoBehaviour
    {
        private GameController _game;
        private FarmDecorSet _set;
        private readonly List<(Transform t, float delay)> _items = new List<(Transform, float)>();
        private float _reveal = 99f;
        private Material _wood, _stone, _leaf, _roof, _wall, _flower, _metal;

        public void Init(GameController game)
        {
            _game = game;
            _set = FarmDecorSet.Load();
            _wood = Prims.Lit(new Color(0.5f, 0.34f, 0.2f), 0.1f);
            _stone = Prims.Lit(new Color(0.6f, 0.6f, 0.62f), 0.2f);
            _leaf = Prims.Lit(new Color(0.25f, 0.55f, 0.28f), 0.1f);
            _roof = Prims.Lit(new Color(0.7f, 0.25f, 0.2f), 0.2f);
            _wall = Prims.Lit(new Color(0.95f, 0.9f, 0.78f), 0.1f);
            _flower = Prims.Lit(new Color(0.95f, 0.4f, 0.6f), 0.3f);
            _metal = Prims.Lit(new Color(0.75f, 0.75f, 0.78f), 0.5f);
            Rebuild(false);
            _game.Sim.GenerationStarted += () => Rebuild(true);
            _game.Sim.Retired += _ => Rebuild(false);
            _game.Sim.FieldExpanded += () => Rebuild(false);
        }

        /// <summary>Rebuilds for the current generation; with <paramref name="animate"/> items pop in one after another.</summary>
        public void Rebuild(bool animate)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _items.Clear();
            int gen = _game.State.Generation.Generation;
            float half = _game.State.GridSize * 0.5f;
            float delay = 0f;
            foreach (var item in _set.ForGeneration(gen))
            {
                var root = new GameObject("Decor " + item.Id).transform;
                root.SetParent(transform, false);
                // Offsets are relative to the field edge so decor scales with the field.
                float x = Mathf.Sign(item.Offset.x) * (half + Mathf.Abs(item.Offset.x) - 0.5f) * (Mathf.Abs(item.Offset.x) > 0.5f ? 1f : 0f) + (Mathf.Abs(item.Offset.x) <= 0.5f ? item.Offset.x : 0f);
                float z = item.Offset.y > 0f ? half + item.Offset.y - 0.5f : -half + item.Offset.y + 0.5f;
                root.localPosition = new Vector3(x, 0f, z);
                root.localRotation = Quaternion.Euler(0f, item.Rotation, 0f);
                if (item.Prefab != null) Instantiate(item.Prefab, root);
                else Build(item.Kind, root);
                root.localScale = Vector3.one * (animate ? 0.001f : item.Scale);
                _items.Add((root, delay));
                delay += 0.12f;
            }
            _reveal = animate ? 0f : 99f;
        }

        /// <summary>Skip the pop-in.</summary>
        public void CompleteReveal() => _reveal = 99f;

        private void Update()
        {
            if (_reveal >= 99f) return;
            _reveal += Time.deltaTime;
            int gen = _game.State.Generation.Generation;
            int k = 0;
            foreach (var item in _set.ForGeneration(gen))
            {
                if (k >= _items.Count) break;
                var (t, delay) = _items[k++];
                float p = Mathf.Clamp01((_reveal - delay) / 0.3f);
                float s = item.Scale * (p < 1f ? Mathf.Lerp(0.001f, 1.15f, Prims.EaseOutQuad(p)) : 1f);
                if (p >= 1f) s = item.Scale;
                t.localScale = Vector3.one * Mathf.Max(0.001f, s);
            }
        }

        private void Build(DecorKind kind, Transform root)
        {
            switch (kind)
            {
                case DecorKind.Tree:
                    Prims.Primitive(PrimitiveType.Cylinder, root, "Trunk", new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.25f, 0.12f), _wood);
                    Prims.MeshObject(Prims.Cone(false), root, "Canopy", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f), _leaf);
                    break;
                case DecorKind.FenceSegment:
                    Prims.Primitive(PrimitiveType.Cube, root, "PostA", new Vector3(-0.35f, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), _wood);
                    Prims.Primitive(PrimitiveType.Cube, root, "PostB", new Vector3(0.35f, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), _wood);
                    Prims.Primitive(PrimitiveType.Cube, root, "RailA", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.04f, 0.04f), _wood);
                    Prims.Primitive(PrimitiveType.Cube, root, "RailB", new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.04f, 0.04f), _wood);
                    break;
                case DecorKind.Well:
                    Prims.Primitive(PrimitiveType.Cylinder, root, "Ring", new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.15f, 0.5f), _stone);
                    Prims.Primitive(PrimitiveType.Cube, root, "PostA", new Vector3(-0.2f, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.05f), _wood);
                    Prims.Primitive(PrimitiveType.Cube, root, "PostB", new Vector3(0.2f, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.05f), _wood);
                    Prims.MeshObject(Prims.Cone(false), root, "Roof", new Vector3(0f, 0.85f, 0f), new Vector3(0.4f, 0.25f, 0.4f), _roof);
                    break;
                case DecorKind.HouseFloor:
                    Prims.Primitive(PrimitiveType.Cube, root, "Wall", new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.7f), _wall);
                    Prims.Primitive(PrimitiveType.Cube, root, "Floor2", new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.5f, 0.6f), _wall);
                    Prims.MeshObject(Prims.Cone(false), root, "Roof", new Vector3(0f, 1.2f, 0f), new Vector3(0.7f, 0.5f, 0.7f), _roof);
                    Prims.Primitive(PrimitiveType.Cube, root, "Door", new Vector3(0f, 0.2f, -0.36f), new Vector3(0.2f, 0.4f, 0.03f), _wood);
                    break;
                case DecorKind.Windmill:
                {
                    Prims.MeshObject(Prims.Cone(false), root, "Tower", new Vector3(0f, 0f, 0f), new Vector3(0.35f, 1.4f, 0.35f), _wall);
                    var hub = new GameObject("Hub").transform;
                    hub.SetParent(root, false);
                    hub.localPosition = new Vector3(0f, 1.2f, -0.25f);
                    for (int i = 0; i < 4; i++)
                    {
                        var blade = Prims.Primitive(PrimitiveType.Cube, hub, "Blade" + i, Vector3.zero, new Vector3(0.08f, 0.7f, 0.02f), _wood);
                        blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                        blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, 0.35f, 0f);
                    }
                    hub.gameObject.AddComponent<Spinner>();
                    break;
                }
                case DecorKind.Signpost:
                    Prims.Primitive(PrimitiveType.Cube, root, "Post", new Vector3(0f, 0.35f, 0f), new Vector3(0.05f, 0.7f, 0.05f), _wood);
                    Prims.Primitive(PrimitiveType.Cube, root, "Sign", new Vector3(0.12f, 0.6f, 0f), new Vector3(0.35f, 0.14f, 0.03f), _wall);
                    break;
                case DecorKind.Flowerbed:
                    Prims.Primitive(PrimitiveType.Cube, root, "Bed", new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.4f), _wood);
                    for (int i = 0; i < 4; i++)
                        Prims.Primitive(PrimitiveType.Sphere, root, "Flower" + i, new Vector3(-0.24f + i * 0.16f, 0.16f, (i % 2 == 0 ? -0.08f : 0.08f)), new Vector3(0.12f, 0.12f, 0.12f), _flower);
                    break;
                case DecorKind.Barrel:
                    Prims.Primitive(PrimitiveType.Cylinder, root, "BarrelA", new Vector3(-0.15f, 0.18f, 0f), new Vector3(0.22f, 0.18f, 0.22f), _wood);
                    Prims.Primitive(PrimitiveType.Cylinder, root, "BarrelB", new Vector3(0.15f, 0.18f, 0.1f), new Vector3(0.22f, 0.18f, 0.22f), _wood);
                    Prims.Primitive(PrimitiveType.Cylinder, root, "Band", new Vector3(-0.15f, 0.2f, 0f), new Vector3(0.23f, 0.02f, 0.23f), _metal);
                    break;
            }
        }
    }

    /// <summary>Slow rotation for the windmill blades.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        private void Update() => transform.Rotate(0f, 0f, 40f * Time.deltaTime, Space.Self);
    }
}
