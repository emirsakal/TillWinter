using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>
    /// The diorama: a chunky soil block under the field (mesh generated from the field size, cached), a path,
    /// a fence line, the farmhouse (three sizes by generation) and a few trees on the far side. Everything is
    /// spawned from the catalogue and static-batched.
    /// </summary>
    public sealed class DioramaView : MonoBehaviour
    {
        public float Margin = 1.1f;
        public float Thickness = 1.3f;
        /// <summary>Extra grass behind the field so the house and trees stand behind the fence, not on it.</summary>
        public float BackDepth = 1.7f;

        private GameController _game;
        private VisualCatalog _catalog;
        private MeshFilter _blockFilter;
        private Transform _scenery;
        private int _builtSize = -1, _builtGen = -1;
        private static readonly Dictionary<int, Mesh> BlockCache = new Dictionary<int, Mesh>();

        public void Init(GameController game, VisualCatalog catalog)
        {
            _game = game;
            _catalog = catalog;
            var block = new GameObject("SoilBlock");
            block.transform.SetParent(transform, false);
            _blockFilter = block.AddComponent<MeshFilter>();
            var mr = block.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { catalog.SlotMaterial(PaletteSlot.Grass), catalog.SlotMaterial(PaletteSlot.SoilBlock) };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            _scenery = new GameObject("Scenery").transform;
            _scenery.SetParent(transform, false);
            Rebuild();
            _game.Sim.FieldExpanded += Rebuild;
            _game.Sim.GenerationStarted += Rebuild;
            _game.Sim.Retired += _ => Rebuild();
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.FieldExpanded -= Rebuild;
            _game.Sim.GenerationStarted -= Rebuild;
        }

        /// <summary>Half extent of the block top (from the field centre).</summary>
        public float HalfExtent => _game.State.GridSize * 0.5f + Margin;

        private void Rebuild()
        {
            int n = _game.State.GridSize;
            int gen = _game.State.Generation.Generation;
            if (n == _builtSize && gen == _builtGen) return;
            _builtSize = n;
            _builtGen = gen;
            _blockFilter.sharedMesh = BlockMesh(n);

            for (int i = _scenery.childCount - 1; i >= 0; i--) Destroy(_scenery.GetChild(i).gameObject);
            float half = n * 0.5f;
            float edge = HalfExtent;

            // Path from the field's bottom edge to the block edge, one tile wide.
            for (float z = -half - 0.5f; z > -edge + 0.2f; z -= 1f)
                Place(_catalog.PathTile, "Path", new Vector3(0f, 0.001f, z), 0f);
            // Fence line along the far edge, centred so both ends sit the same distance from the island sides;
            // the middle panel (two on an even count) is the gate.
            int panels = Mathf.FloorToInt(2f * edge - 0.2f);
            for (int i = 0; i < panels; i++)
            {
                float x = -(panels - 1) * 0.5f + i;
                bool gate = Mathf.Abs(x) < 0.6f;
                Place(gate ? _catalog.FenceGate : _catalog.Fence, gate ? "Gate" : "Fence", new Vector3(x, 0f, half + 0.55f), 0f);
            }
            // Behind the fence, on the extra back strip: farmhouse far left, trees far right.
            float back = half + 0.55f + (Margin - 0.55f + BackDepth) * 0.55f;
            Place(_catalog.HouseFor(gen), "House", new Vector3(-edge + 1.1f, 0f, back), 15f);
            if (_catalog.Trees != null && _catalog.Trees.Length > 0)
            {
                Place(_catalog.Trees[0 % _catalog.Trees.Length], "Tree", new Vector3(edge - 0.7f, 0f, back - 0.2f), 0f);
                Place(_catalog.Trees[2 % _catalog.Trees.Length], "Tree", new Vector3(edge - 1.5f, 0f, back + 0.4f), 40f);
                Place(_catalog.Trees[3 % _catalog.Trees.Length], "Tree", new Vector3(-edge + 0.5f, 0f, -half - 0.9f), 70f);
            }
            Place(_catalog.Bush, "Bush", new Vector3(edge - 0.6f, 0f, -half - 0.8f), 20f);
            Place(_catalog.Rock, "Rock", new Vector3(-edge + 0.45f, 0f, half - 0.4f), 0f);
            StaticBatchingUtility.Combine(_scenery.gameObject);
        }

        private void Place(GameObject prefab, string name, Vector3 pos, float yaw)
        {
            if (prefab == null) return;
            var go = _catalog.Spawn(prefab, _scenery, name);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Box with a grass top (submesh 0) and soil sides/bottom (submesh 1); bevelled top edge.</summary>
        private Mesh BlockMesh(int gridSize)
        {
            if (BlockCache.TryGetValue(gridSize, out var cached) && cached != null) return cached;
            float e = gridSize * 0.5f + Margin;
            float b = e + BackDepth; // back edge: extra strip behind the fence
            float d = Thickness;
            const float bevel = 0.12f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var top = new List<int>();
            var side = new List<int>();

            void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 dd)
            {
                int i = verts.Count;
                var n = Vector3.Cross(b - a, c - a).normalized;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(dd);
                for (int k = 0; k < 4; k++) norms.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            float ei = e - bevel, bi = b - bevel;
            // Top (inset) and four bevel strips.
            Quad(top, new Vector3(-ei, 0f, -ei), new Vector3(-ei, 0f, bi), new Vector3(ei, 0f, bi), new Vector3(ei, 0f, -ei));
            Quad(top, new Vector3(-e, -bevel, -e), new Vector3(-ei, 0f, -ei), new Vector3(ei, 0f, -ei), new Vector3(e, -bevel, -e)); // front
            Quad(top, new Vector3(e, -bevel, b), new Vector3(ei, 0f, bi), new Vector3(-ei, 0f, bi), new Vector3(-e, -bevel, b)); // back
            Quad(top, new Vector3(-e, -bevel, b), new Vector3(-ei, 0f, bi), new Vector3(-ei, 0f, -ei), new Vector3(-e, -bevel, -e)); // left
            Quad(top, new Vector3(e, -bevel, -e), new Vector3(ei, 0f, -ei), new Vector3(ei, 0f, bi), new Vector3(e, -bevel, b)); // right
            // Sides and bottom.
            Quad(side, new Vector3(-e, -d, -e), new Vector3(-e, -bevel, -e), new Vector3(e, -bevel, -e), new Vector3(e, -d, -e)); // front
            Quad(side, new Vector3(e, -d, b), new Vector3(e, -bevel, b), new Vector3(-e, -bevel, b), new Vector3(-e, -d, b)); // back
            Quad(side, new Vector3(-e, -d, b), new Vector3(-e, -bevel, b), new Vector3(-e, -bevel, -e), new Vector3(-e, -d, -e)); // left
            Quad(side, new Vector3(e, -d, -e), new Vector3(e, -bevel, -e), new Vector3(e, -bevel, b), new Vector3(e, -d, b)); // right
            Quad(side, new Vector3(-e, -d, b), new Vector3(e, -d, b), new Vector3(e, -d, -e), new Vector3(-e, -d, -e)); // bottom

            var mesh = new Mesh { name = "SoilBlock" + gridSize };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(side, 1);
            mesh.RecalculateBounds();
            BlockCache[gridSize] = mesh;
            return mesh;
        }
    }
}
