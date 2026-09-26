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
        private DogView _dogView;
        private PaletteBinder _windows;
        private float _windowGlow = -1f;
        private float _windowTarget;
        private FrogView _frog;
        private readonly List<(Vector2 at, float radius)> _avoid = new List<(Vector2, float)>();
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
            mr.sharedMaterials = BlockMaterials(catalog);
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

            // Path from the field's bottom edge all the way to the rim, one tile wide. The last tile sits flush with
            // the edge: with the wider margin the path used to stop after one tile and read as a stray slab.
            float pathEnd = -edge + 0.5f;
            for (float z = -half - 0.5f; z > pathEnd; z -= 1f)
                Place(_catalog.PathTile, "Path", new Vector3(0f, 0.001f, z), 0f);
            Place(_catalog.PathTile, "PathEnd", new Vector3(0f, 0.002f, pathEnd), 0f);
            // Fence line along the far edge, centred so both ends sit the same distance from the island sides;
            // the middle panel (two on an even count) is the gate.
            int panels = Mathf.FloorToInt(2f * edge - 1.0f); // stops clear of the block's taper, which made a full-width fence look off-centre
            for (int i = 0; i < panels; i++)
            {
                float x = -(panels - 1) * 0.5f + i;
                bool gate = Mathf.Abs(x) < 0.6f;
                Place(gate ? _catalog.FenceGate : _catalog.Fence, gate ? "Gate" : "Fence", new Vector3(x, 0f, half + 0.55f), 0f);
            }
            float fenceEnd = (panels - 1) * 0.5f + 0.5f;
            Place(_catalog.FencePost, "FencePostL", new Vector3(-fenceEnd, 0f, half + 0.55f), 0f);
            Place(_catalog.FencePost, "FencePostR", new Vector3(fenceEnd, 0f, half + 0.55f), 0f);
            // Behind the fence, on the extra back strip: farmhouse far right, trees far left.
            float back = half + 0.55f + (Margin - 0.55f + BackDepth) * 0.55f;
            var house = new Vector3(edge - 1.1f, 0f, back);
            var houseGo = Place(_catalog.HouseFor(gen), "House", house, -15f);
            _windows = houseGo != null ? houseGo.transform.Find("Windows")?.GetComponent<PaletteBinder>() : null;
            _windowGlow = -1f;
            Shadow(house, 1.9f);
            _treesGreen.Clear();
            _treesAutumn.Clear();
            if (_catalog.Trees != null && _catalog.Trees.Length > 0)
            {
                // The two back trees stand apart so the kennel fits between them (the house corner was crowded).
                PlaceTree(0, new Vector3(-edge + 0.45f, 0f, back - 0.15f), 0f);
                PlaceTree(2, new Vector3(-edge + 2.35f, 0f, back + 0.45f), -40f);
                PlaceTree(3, new Vector3(-edge + 0.5f, 0f, -half - 0.9f), 70f);
            }
            var bush = new Vector3(edge - 0.6f, 0f, -half - 0.8f);
            Place(_catalog.Bush, "Bush", bush, 20f);
            Shadow(bush, 0.8f);
            Place(_catalog.Rock, "Rock", new Vector3(-edge + 0.45f, 0f, half - 0.4f), 0f);
            // Up on the back strip beside the house, where there is room for it: down in front it crowded the field.
            Place(_catalog.Pond, "Pond", new Vector3(edge - 2.9f, 0f, back - 0.15f), 15f);
            // The kennel is scenery and batches with the rest; the dog must not, or batching would freeze its wag.
            // Between the back trees on the left, away from the house, pond and frog on the right.
            var kennel = new Vector3(-edge + 1.4f, 0f, back + 0.1f);
            Place(_catalog.Kennel, "Kennel", kennel, 25f);
            var dogHome = new Vector3(-edge + 1.5f, 0f, back - 0.65f);
            PlaceDog(dogHome, 35f); // in front of the kennel, where it can be seen
            // It trots along the grass strip just behind the fence, clear of the pond and the house.
            _dogView?.SetArea(dogHome, -edge + 0.9f, edge - 0.5f, half + 0.55f + 0.3f, half + 0.55f + 0.42f);
            PlaceFrog(new Vector3(edge - 2.9f + 0.62f, 0f, back - 0.15f - 0.5f), 200f);
            // Stepping stones from the gate up to the farmhouse door.
            var gateStep = new Vector3(0f, 0f, half + 0.95f);
            var door = new Vector3(edge - 1.25f, 0f, back - 0.75f);
            for (int i = 0; i < 4; i++)
            {
                var p = Vector3.Lerp(gateStep, door, i / 3.5f);
                Place(_catalog.SteppingStone, "Step", p + new Vector3(0f, 0.002f, (i % 2 == 0 ? 0.05f : -0.05f)), i * 23f);
            }
            _pondCentre = new Vector3(edge - 2.9f, 0.06f, back - 0.15f);
            PlaceLife(n, half, edge, back);
            Dress(n, half, edge, back, panels);
            OnSeasonChanged(_game.State.Season);
            StaticBatchingUtility.Combine(_scenery.gameObject);
        }

        /// <summary>
        /// Dressing every farm gets from the first generation: tufts, flowers and pebbles along the rim, low clusters in
        /// the open grass beside the field, rock hanging under the front edge and two small islands far off. Seeded by
        /// the field size so a farm looks the same every time it loads; the generation decor still adds the big pieces.
        /// </summary>
        private void Dress(int n, float half, float edge, float back, int panels)
        {
            var rnd = new System.Random(1000 + n);
            float far = edge + BackDepth; // the back edge of the block

            _avoid.Clear();
            _avoid.Add((new Vector2(edge - 1.1f, back), 1.3f));        // house
            _avoid.Add((new Vector2(edge - 1.95f, back - 0.35f), 0.55f)); // kennel
            _avoid.Add((new Vector2(edge - 1.3f, back - 1.05f), 0.45f));  // dog
            _avoid.Add((new Vector2(edge - 2.9f, back - 0.15f), 0.85f));  // pond
            _avoid.Add((new Vector2(-edge + 0.75f, 0.35f), 0.35f));        // cat
            _avoid.Add((new Vector2(edge - 2.05f, back + 0.6f), 0.3f));    // flag
            for (int i = 0; i < 4; i++)                                     // stepping stones
                _avoid.Add((Vector2.Lerp(new Vector2(0f, half + 0.95f), new Vector2(edge - 1.25f, back - 0.75f), i / 3.5f), 0.25f));
            _avoid.Add((new Vector2(-edge + 0.7f, back - 0.2f), 0.5f));   // trees
            _avoid.Add((new Vector2(-edge + 1.5f, back + 0.4f), 0.5f));
            _avoid.Add((new Vector2(-edge + 0.5f, -half - 0.9f), 0.5f));
            _avoid.Add((new Vector2(edge - 0.6f, -half - 0.8f), 0.55f));  // bush
            _avoid.Add((new Vector2(-edge + 0.45f, half - 0.4f), 0.45f)); // rock

            // Rim: front, both sides, back.
            const float inset = 0.3f;
            for (float x = -edge + 0.35f; x < edge - 0.3f; x += 0.5f)
                Scatter(new Vector3(x + Jit(rnd, 0.2f), 0f, -edge + inset + Jit(rnd, 0.08f)), rnd, half, panels);
            for (float z = -edge + 0.8f; z < far - 0.3f; z += 0.55f)
            {
                Scatter(new Vector3(-edge + inset + Jit(rnd, 0.08f), 0f, z + Jit(rnd, 0.2f)), rnd, half, panels);
                Scatter(new Vector3(edge - inset + Jit(rnd, 0.08f), 0f, z + Jit(rnd, 0.2f)), rnd, half, panels);
            }
            for (float x = -edge + 0.8f; x < edge - 0.8f; x += 0.55f)
                Scatter(new Vector3(x + Jit(rnd, 0.2f), 0f, far - inset + Jit(rnd, 0.08f)), rnd, half, panels);

            // Low clusters in the open grass either side of the field.
            float side = half + (edge - half) * 0.5f;
            foreach (float z in new[] { -half * 0.4f, half * 0.35f })
                for (int k = 0; k < 4; k++)
                {
                    Scatter(new Vector3(-side + Jit(rnd, 0.3f), 0f, z + Jit(rnd, 0.3f)), rnd, half, panels);
                    Scatter(new Vector3(side + Jit(rnd, 0.3f), 0f, -z + Jit(rnd, 0.3f)), rnd, half, panels);
                }

            // Torn earth under the front edge, where the tapered block ends.
            if (_catalog.HangingRock != null)
            {
                float bottom = -Thickness, lip = edge * 0.55f;
                for (int i = 0; i < 5; i++)
                {
                    float x = Mathf.Lerp(-lip + 0.4f, lip - 0.4f, i / 4f) + Jit(rnd, 0.15f);
                    var rock = Place(_catalog.HangingRock, "HangingRock", new Vector3(x, bottom + 0.05f, -lip + 0.25f), (float)rnd.NextDouble() * 360f);
                    if (rock != null) rock.transform.localScale = Vector3.one * (0.7f + (float)rnd.NextDouble() * 0.4f);
                }
            }

            // Two small islands far behind: the farm floats among others.
            var blockMats = _blockFilter.GetComponent<MeshRenderer>().sharedMaterials;
            for (int i = 0; i < 2; i++)
            {
                var islet = new GameObject("Islet" + i).transform;
                islet.SetParent(_scenery, false);
                // Small and well clear of the island: larger and closer they read as stray floor tiles in the sky.
                // v3.6: the top of the sky now carries the season timeline, which they kept crossing, so they float in
                // the open sky under the island instead, either side of the stamina bar. Placed from the camera's
                // framing, so they land on the same spot of the screen for every field size and screen shape.
                islet.localPosition = AtScreen(n, i == 0 ? 0.27f : 0.74f, i == 0 ? 0.2f : 0.185f, far + 1f + i * 0.5f);
                islet.localScale = Vector3.one * (i == 0 ? 0.34f : 0.28f);
                islet.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildBlock(1, 0.45f, 0.9f, 0f);
                var mr = islet.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = blockMats;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                if (_catalog.Trees != null && _catalog.Trees.Length > 0)
                {
                    var tree = _catalog.Spawn(_catalog.Trees[(i + 1) % _catalog.Trees.Length], islet, "Tree");
                    tree.transform.localPosition = new Vector3(0.1f, 0f, 0.05f);
                }
                if (_catalog.HangingRock != null)
                {
                    var under = _catalog.Spawn(_catalog.HangingRock, islet, "Under"); // torn earth underneath: an island, not a tile
                    under.transform.localPosition = new Vector3(0f, -0.85f, 0f);
                    under.transform.localScale = Vector3.one * 1.3f;
                }
            }
        }

        /// <summary>
        /// The point at depth <paramref name="z"/> that <see cref="CameraRig"/>'s framing of an <paramref name="n"/>-wide field
        /// draws at (<paramref name="sx"/>, <paramref name="sy"/>), fractions of the screen from the bottom left. The rig fits the
        /// field's width and never shows less height than the reference phone, so the fractions hold on every screen.
        /// </summary>
        private static Vector3 AtScreen(int n, float sx, float sy, float z)
        {
            var rig = CameraRig.Instance;
            float side = rig != null ? rig.SideMargin : 0.45f;
            float extra = rig != null ? rig.ExtraWidth : 2.7f;
            float fieldY = rig != null ? rig.FieldScreenY : 0.46f;
            float tilt = rig != null ? rig.TiltFromTopDown : 40f;
            float width = n + 2f * side + extra;
            float size = width / (2f * CameraRig.ReferenceAspect);
            float pitch = (90f - tilt) * Mathf.Deg2Rad;
            float upY = Mathf.Cos(pitch), upZ = Mathf.Sin(pitch); // the camera's up in world space
            float along = (sy - fieldY) * 2f * size;
            return new Vector3((sx - 0.5f) * width, (along - upZ * z) / upY, z);
        }

        private static float Jit(System.Random rnd, float amount) => ((float)rnd.NextDouble() - 0.5f) * 2f * amount;

        /// <summary>One piece of rim dressing, unless the spot belongs to the field, the fence, the path or a prop.</summary>
        private void Scatter(Vector3 pos, System.Random rnd, float half, int panels)
        {
            float roll = (float)rnd.NextDouble();
            float yaw = (float)rnd.NextDouble() * 360f;
            float scale = 0.8f + (float)rnd.NextDouble() * 0.45f;
            if (Mathf.Abs(pos.x) < half + 0.2f && Mathf.Abs(pos.z) < half + 0.2f) return;                       // field
            if (Mathf.Abs(pos.z - (half + 0.55f)) < 0.3f && Mathf.Abs(pos.x) < panels * 0.5f + 0.2f) return;  // fence
            if (Mathf.Abs(pos.x) < 0.65f && pos.z < -half) return;                                                // path
            var flat = new Vector2(pos.x, pos.z);
            foreach (var (at, radius) in _avoid)
                if ((flat - at).sqrMagnitude < radius * radius) return;

            GameObject prefab;
            if (roll < 0.58f) prefab = _catalog.GrassTuft;
            else if (roll < 0.82f && _catalog.Flowers != null && _catalog.Flowers.Length > 0) prefab = _catalog.Flowers[rnd.Next(_catalog.Flowers.Length)];
            else prefab = _catalog.Pebbles;
            var go = Place(prefab, "Dressing", pos, yaw);
            if (go == null) return;
            go.transform.localScale = Vector3.one * scale;
            // Grass, flowers and pebbles hug the ground: their shadows were invisible and cost a shadow pass each.
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
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
                _dogView = go.AddComponent<DogView>();
                _dogView.Init(_game, AudioManager.Instance);
            }
            _dog.localPosition = pos;
            _dog.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Like the dog: animated, so it lives outside the batched scenery and moves with a rebuild.</summary>
        private void PlaceFrog(Vector3 pos, float yaw)
        {
            if (_catalog.Frog == null) return;
            if (_frog == null)
            {
                var go = _catalog.Spawn(_catalog.Frog, transform, "Frog");
                _frog = go.AddComponent<FrogView>();
                _frog.Init(_game);
            }
            _frog.transform.localPosition = pos;
            _frog.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            _frog.SetHome(pos);
        }

        /// <summary>The farmhouse lights its windows as the year cools: a little in Autumn, fully once frost is near.</summary>
        private void LateUpdate()
        {
            if (_game == null || _game.State == null) return;
            RefreshUpgrades(false);
            TickPond();
            if (_windows == null) return;
            var s = _game.State;
            _windowTarget = s.IsWinter || s.FrostWarning ? 1f : s.Season == TillWinter.Core.Season.Autumn ? 0.45f : 0f;
            float next = _windowGlow < 0f ? _windowTarget : Prims.Damp(_windowGlow, _windowTarget, 1.5f, Time.deltaTime);
            if (Mathf.Abs(next - _windowGlow) < 0.002f) return;
            _windowGlow = next;
            var palette = Palette.Load();
            _windows.Override(PaletteSlot.Glass, Color.Lerp(palette.Get(PaletteSlot.Glass), palette.Golden, _windowGlow));
            _windows.SetEmission(palette.GoldenGlow * (_windowGlow * 0.8f));
        }

        private Transform _flag, _cat, _channel, _sunflowers;
        private readonly List<Transform> _chickens = new List<Transform>();
        private Vector3 _pondCentre;
        private float _rippleAt;

        /// <summary>
        /// The animated extras, outside the batched scenery: a flag by the house, two hens behind the fence, a cat asleep
        /// in the front grass, and the upgrades that show on the land (an irrigation channel, a row of sunflowers).
        /// </summary>
        private void PlaceLife(int n, float half, float edge, float back)
        {
            if (_flag == null && _catalog.Flag != null)
            {
                _flag = _catalog.Spawn(_catalog.Flag, transform, "Flag").transform;
                _flag.gameObject.AddComponent<FlagView>();
            }
            if (_flag != null) _flag.localPosition = new Vector3(edge - 2.05f, 0f, back + 0.6f);
            if (_chickens.Count == 0 && _catalog.Chicken != null)
                for (int i = 0; i < 2; i++)
                {
                    var hen = _catalog.Spawn(_catalog.Chicken, transform, "Chicken").transform;
                    hen.gameObject.AddComponent<ChickenView>();
                    _chickens.Add(hen);
                }
            for (int i = 0; i < _chickens.Count; i++)
                _chickens[i].GetComponent<ChickenView>().SetArea(new Vector3(-edge + 1.2f + i * 0.9f, 0f, half + 0.95f), -edge + 0.6f, -0.9f, half + 0.8f, half + 1.1f, i);
            if (_cat == null && _catalog.Cat != null)
            {
                _cat = _catalog.Spawn(_catalog.Cat, transform, "Cat").transform;
                _cat.gameObject.AddComponent<CatView>();
            }
            if (_cat != null)
            {
                _cat.localPosition = new Vector3(-edge + 0.75f, 0f, 0.35f);
                _cat.localRotation = Quaternion.Euler(0f, 140f, 0f);
            }
            if (_channel == null && _catalog.Channel != null) _channel = _catalog.Spawn(_catalog.Channel, transform, "Channel").transform;
            if (_channel != null)
            {
                _channel.localPosition = new Vector3(-half - 0.3f, 0f, 0f);
                _channel.localScale = new Vector3(1f, 1f, n + 0.4f);
            }
            if (_sunflowers == null && _catalog.Sunflower != null)
            {
                _sunflowers = new GameObject("Sunflowers").transform;
                _sunflowers.SetParent(transform, false);
                for (int i = 0; i < 3; i++) _catalog.Spawn(_catalog.Sunflower, _sunflowers, "Sunflower");
            }
            if (_sunflowers != null)
                for (int i = 0; i < _sunflowers.childCount; i++)
                    _sunflowers.GetChild(i).localPosition = new Vector3(half + 0.32f, 0f, -half + 0.4f + i * (n - 0.8f) / 2f);
            RefreshUpgrades(true);
        }

        private bool _showChannel, _showSunflowers;

        /// <summary>Bought upgrades show on the land; checked each frame, rebuilt only when the answer changes.</summary>
        private void RefreshUpgrades(bool force)
        {
            var stats = _game.State.Stats;
            // The channel comes with the soft-ground line (the well), the sunflowers with growth: what the Soil branch buys.
            bool channel = stats.HpMult < 0.999f;
            bool sun = stats.GrowthMult > 1.001f;
            if (_channel != null && (force || channel != _showChannel)) _channel.gameObject.SetActive(channel);
            if (_sunflowers != null && (force || sun != _showSunflowers)) _sunflowers.gameObject.SetActive(sun);
            _showChannel = channel;
            _showSunflowers = sun;
        }

        /// <summary>Now and then a ring spreads on the pond (the VFX pool rate-limits it like every burst).</summary>
        private void TickPond()
        {
            if (!SettingsStore.MotionAllowed || VfxPlayer.Instance == null || _game.State.Phase != Phase.Year) return;
            if (Time.time < _rippleAt) return;
            _rippleAt = Time.time + 1.6f + Random.value * 2.2f;
            var p = Palette.Load();
            var offset = new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.3f, 0.3f));
            VfxPlayer.Instance.Play(VfxId.SoilRipple, transform.TransformPoint(_pondCentre + offset), 0.7f, Color.Lerp(p.Get(PaletteSlot.Water), p.Get(PaletteSlot.Snow), 0.6f));
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
        /// <summary>The block's three submeshes: grass top, soil band, rock below.</summary>
        public static Material[] BlockMaterials(VisualCatalog catalog) => new[]
        {
            catalog.SlotMaterial(PaletteSlot.Grass), catalog.SlotMaterial(PaletteSlot.SoilBlock), catalog.SlotMaterial(PaletteSlot.Stone),
        };

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
            var rock = new List<int>();

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
            // Each side is earth over rock: the top 45% soil, the rest stone, so the profile reads as layers.
            void Layered(Vector3 lowL, Vector3 highL, Vector3 highR, Vector3 lowR)
            {
                const float soil = 0.45f;
                var midL = Vector3.Lerp(highL, lowL, soil);
                var midR = Vector3.Lerp(highR, lowR, soil);
                Quad(side, midL, highL, highR, midR);
                Quad(rock, lowL, midL, midR, lowR);
            }
            Layered(new Vector3(-bx, -d, bzF), new Vector3(-e, -bevel, -e), new Vector3(e, -bevel, -e), new Vector3(bx, -d, bzF)); // front
            Layered(new Vector3(bx, -d, bzB), new Vector3(e, -bevel, b), new Vector3(-e, -bevel, b), new Vector3(-bx, -d, bzB)); // back
            Layered(new Vector3(-bx, -d, bzB), new Vector3(-e, -bevel, b), new Vector3(-e, -bevel, -e), new Vector3(-bx, -d, bzF)); // left
            Layered(new Vector3(bx, -d, bzF), new Vector3(e, -bevel, -e), new Vector3(e, -bevel, b), new Vector3(bx, -d, bzB)); // right
            Quad(rock, new Vector3(-bx, -d, bzB), new Vector3(bx, -d, bzB), new Vector3(bx, -d, bzF), new Vector3(-bx, -d, bzF)); // bottom

            var mesh = new Mesh { name = "SoilBlock" + gridSize };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(side, 1);
            mesh.SetTriangles(rock, 2);
            mesh.RecalculateBounds();
            BlockCache[key] = mesh;
            return mesh;
        }
    }
}
