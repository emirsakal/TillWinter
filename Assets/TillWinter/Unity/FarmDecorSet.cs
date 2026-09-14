using System.Collections.Generic;
using UnityEngine;

namespace TillWinter.Unity
{
    public enum DecorKind { Tree, FenceSegment, Well, HouseFloor, Windmill, Signpost, Flowerbed, Barrel }

    /// <summary>One decor item unlocked from a generation on. Primitives now; <see cref="Prefab"/> is the S6 slot.</summary>
    [System.Serializable]
    public sealed class DecorItem
    {
        public string Id;
        public DecorKind Kind;
        public int MinGeneration = 2;
        /// <summary>Position relative to the field centre, in plot units (x right, y away from the camera).</summary>
        public Vector2 Offset;
        public float Rotation;
        public float Scale = 1f;
        public GameObject Prefab;
    }

    /// <summary>Data-driven farm decor per generation (GDD §7: "each generation adds a visible change"). One asset in Resources/FarmDecor.</summary>
    [CreateAssetMenu(menuName = "Till Winter/Farm Decor", fileName = "FarmDecor")]
    public sealed class FarmDecorSet : ScriptableObject
    {
        public List<DecorItem> Items = new List<DecorItem>();

        /// <summary>Items visible at a generation (gen 1 = none).</summary>
        public IEnumerable<DecorItem> ForGeneration(int generation)
        {
            foreach (var i in Items) if (i.MinGeneration <= generation) yield return i;
        }

        public static FarmDecorSet Defaults()
        {
            var set = CreateInstance<FarmDecorSet>();
            void Add(string id, DecorKind kind, int gen, float x, float y, float rot = 0f, float scale = 1f) =>
                set.Items.Add(new DecorItem { Id = id, Kind = kind, MinGeneration = gen, Offset = new Vector2(x, y), Rotation = rot, Scale = scale });
            Add("well", DecorKind.Well, 2, -1.4f, 1.1f);
            Add("tree_gate", DecorKind.Tree, 3, 1.5f, 1.3f, 20f, 1.1f);
            Add("fence_left_a", DecorKind.FenceSegment, 4, -1.3f, -0.6f, 90f);
            Add("fence_left_b", DecorKind.FenceSegment, 4, -1.3f, 0.2f, 90f);
            Add("fence_right_a", DecorKind.FenceSegment, 4, 1.3f, -0.6f, 90f);
            Add("fence_right_b", DecorKind.FenceSegment, 4, 1.3f, 0.2f, 90f);
            Add("house", DecorKind.HouseFloor, 5, -0.6f, 1.6f);
            Add("windmill", DecorKind.Windmill, 6, 0.9f, 1.8f);
            Add("tree_line_a", DecorKind.Tree, 7, -1.7f, 1.8f, 0f, 0.9f);
            Add("tree_line_b", DecorKind.Tree, 7, -1.1f, 2.0f, 40f, 1.0f);
            Add("tree_line_c", DecorKind.Tree, 7, 1.7f, 2.0f, 70f, 0.95f);
            Add("signpost", DecorKind.Signpost, 8, 1.6f, -0.9f);
            Add("flowerbed", DecorKind.Flowerbed, 8, -1.6f, -1.0f);
            Add("barrels", DecorKind.Barrel, 9, 0.2f, 1.7f);
            return set;
        }

        private static FarmDecorSet _loaded;

        public static FarmDecorSet Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<FarmDecorSet>("FarmDecor");
            if (_loaded == null) _loaded = Defaults();
            return _loaded;
        }
    }
}
