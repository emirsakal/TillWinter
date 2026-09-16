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
        public float Margin = 1.6f; // more grass around the field: props crowded the plots at 1.1
        public float Thickness = 1.6f;
        /// <summary>Extra grass behind the field so the house and trees stand behind the fence, not on it.</summary>
        public float BackDepth = 1.7f;

        private GameController _game;
        private VisualCatalog _catalog;
        private MeshFilter _blockFilter;
        private Transform _scenery;
        private Transform _dog;
        private int _builtSize = -1, _builtGen = -1;
        private readonly List<GameObject> _treesGreen = new List<GameObject>();
        private readonly List<GameObject> _treesAutumn = new List<GameObject>();
        private static readonly Dictionary<(int, float, float, float), Mesh> BlockCache = new Dictionary<(int, float, float, float), Mesh>();

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
            _game.Sim.SeasonChanged += OnSeasonChanged;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Sim == null) return;
            _game.Sim.FieldExpanded -= Rebuild;
            _game.Sim.GenerationStarted -= Rebuild;
            _game.Sim.SeasonChanged -= OnSeasonChanged;
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

            // A fresh root every rebuild. StaticBatchingUtility.Combine bakes a root's children into one batched
            // mesh, and Destroy only takes effect at end of frame — so re-combining this root would batch an
            // already-batched hierarchy that still holds the previous build's dying children, which scrambled the
            // scenery when the field grew.
            if (_scenery != null) Destroy(_scenery.gameObject);
            _scenery = new GameObject("Scenery").transform;
            _scenery.SetParent(transform, false);
            float half = n * 0.5f;
            float edge = HalfExtent;

            // Path from the field's bottom edge to the block edge, one tile wide.
            for (float z = -half - 0.5f; z > -edge + 0.2f; z -= 1f)
                Place(_catalog.PathTile, "Path", new Vector3(0f, 0.001f, z), 0f);
            // Fence line along the far edge, centred so both ends sit the same distance from the island sides;
            // the middle panel (two on an even count) is the gate.
            int panels = Mathf.FloorToInt(2f * edge - 1.0f); // stops clear of the block's taper, which made a full-width fence look off-centre
            for (int i = 0; i < panels; i++)
            {
                float x = -(panels - 1) * 0.5f + i;
                bool gate = Mathf.Abs(x) < 0.6f;
                Place(gate ? _catalog.FenceGate : _catalog.Fence, gate ? "Gate" : "Fence", new Vector3(x, 0f, half + 0.55f), 0f);
            }
            // Behind the fence, on the extra back strip: farmhouse far right, trees far left.
            float back = half + 0.55f + (Margin - 0.55f + BackDepth) * 0.55f;
            var house = new Vector3(edge - 1.1f, 0f, back);
            Place(_catalog.HouseFor(gen), "House", house, -15f);
            Shadow(house, 1.9f);
            _treesGreen.Clear();
            _treesAutumn.Clear();
            if (_catalog.Trees != null && _catalog.Trees.Length > 0)
            {
                PlaceTree(0, new Vector3(-edge + 0.7f, 0f, back - 0.2f), 0f);
                PlaceTree(2, new Vector3(-edge + 1.5f, 0f, back + 0.4f), -40f);
                PlaceTree(3, new Vector3(-edge + 0.5f, 0f, -half - 0.9f), 70f);
            }
            var bush = new Vector3(edge - 0.6f, 0f, -half - 0.8f);
            Place(_catalog.Bush, "Bush", bush, 20f);
            Shadow(bush, 0.8f);
            Place(_catalog.Rock, "Rock", new Vector3(-edge + 0.45f, 0f, half - 0.4f), 0f);
            // Up on the back strip beside the house, where there is room for it: down in front it crowded the field.
            Place(_catalog.Pond, "Pond", new Vector3(edge - 2.9f, 0f, back - 0.15f), 15f);
            // The kennel is scenery and batches with the rest; the dog must not, or batching would freeze its wag.
            Place(_catalog.Kennel, "Kennel", new Vector3(edge - 1.95f, 0f, back - 0.35f), -25f);
            PlaceDog(new Vector3(edge - 1.3f, 0f, back - 1.05f), -35f); // in front of the kennel, where it can be seen
            OnSeasonChanged(_game.State.Season);
            StaticBatchingUtility.Combine(_scenery.gameObject);
        }

        /// <summary>
        /// Spawned once, outside the scenery root so static batching leaves it animatable, and moved to the new spot
        /// whenever the island is rebuilt.
        /// </summary>
        private void PlaceDog(Vector3 pos, float yaw)
        {
            if (_catalog.Dog == null) return;
            if (_dog == null)
            {
                var go = _catalog.Spawn(_catalog.Dog, transform, "Dog");
                if (go == null) return;
                _dog = go.transform;
                go.AddComponent<DogView>().Init(_game, AudioManager.Instance);
            }
            _dog.localPosition = pos;
            _dog.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private GameObject Place(GameObject prefab, string name, Vector3 pos, float yaw)
        {
            if (prefab == null) return null;
            var go = _catalog.Spawn(prefab, _scenery, name);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>A soft dark disc under an object: URP shadows are off on the Low tier, so props would float.</summary>
        private void Shadow(Vector3 pos, float size)
        {
            var go = _catalog.Spawn(_catalog.BlobShadow, _scenery, "Shadow");
            if (go == null) return;
            go.transform.localPosition = new Vector3(pos.x, 0.02f, pos.z);
            go.transform.localScale = Vector3.one * size;
        }

        /// <summary>Both tree variants at one spot: the autumn one takes over when the season turns.</summary>
        private void PlaceTree(int index, Vector3 pos, float yaw)
        {
            var green = Place(_catalog.Trees[index % _catalog.Trees.Length], "Tree", pos, yaw);
            var autumn = Place(_catalog.TreeAutumn, "TreeAutumn", pos, yaw);
            Shadow(pos, 1.1f);
            if (green != null) _treesGreen.Add(green);
            if (autumn != null) _treesAutumn.Add(autumn);
        }

        /// <summary>Autumn turns the trees; Winter leaves them bare-coloured with the shader's snow.</summary>
        private void OnSeasonChanged(Season season)
        {
            bool autumn = season == Season.Autumn;
            for (int i = 0; i < _treesGreen.Count; i++)
                if (_treesGreen[i] != null) _treesGreen[i].SetActive(!autumn);
            for (int i = 0; i < _treesAutumn.Count; i++)
                if (_treesAutumn[i] != null) _treesAutumn[i].SetActive(autumn);
        }

        /// <summary>Box with a grass top (submesh 0) and soil sides/bottom (submesh 1); bevelled top edge.</summary>
        private Mesh BlockMesh(int gridSize) => BuildBlock(gridSize, Margin, Thickness, BackDepth);

        /// <summary>The island block for a field of <paramref name="gridSize"/> (cached per shape; the title scene uses it too).</summary>
        public static Mesh BuildBlock(int gridSize, float margin, float thickness, float backDepth)
        {
            var key = (gridSize, margin, thickness, backDepth);
            if (BlockCache.TryGetValue(key, out var cached) && cached != null) return cached;
            float e = gridSize * 0.5f + margin;
            float b = e + backDepth; // back edge: extra strip behind the fence
            float d = thickness;
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
            // Sides and bottom. The bottom is pulled in, so the island hangs like a chunk of earth instead of
            // ending in a flat box.
            float bx = e * 0.55f;
            float cz = (b - e) * 0.5f, depth = (b + e) * 0.5f * 0.55f;
            float bzF = cz - depth, bzB = cz + depth;
            Quad(side, new Vector3(-bx, -d, bzF), new Vector3(-e, -bevel, -e), new Vector3(e, -bevel, -e), new Vector3(bx, -d, bzF)); // front
            Quad(side, new Vector3(bx, -d, bzB), new Vector3(e, -bevel, b), new Vector3(-e, -bevel, b), new Vector3(-bx, -d, bzB)); // back
            Quad(side, new Vector3(-bx, -d, bzB), new Vector3(-e, -bevel, b), new Vector3(-e, -bevel, -e), new Vector3(-bx, -d, bzF)); // left
            Quad(side, new Vector3(bx, -d, bzF), new Vector3(e, -bevel, -e), new Vector3(e, -bevel, b), new Vector3(bx, -d, bzB)); // right
            Quad(side, new Vector3(-bx, -d, bzB), new Vector3(bx, -d, bzB), new Vector3(bx, -d, bzF), new Vector3(-bx, -d, bzF)); // bottom

            var mesh = new Mesh { name = "SoilBlock" + gridSize };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(side, 1);
            mesh.RecalculateBounds();
            BlockCache[key] = mesh;
            return mesh;
        }
    }
}
