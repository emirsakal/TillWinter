using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Position in abstract layout units (1 unit ≈ one node pitch). Y grows upward.</summary>
    public readonly struct LayoutPos
    {
        public readonly float X;
        public readonly float Y;

        public LayoutPos(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Deterministic field layout (GDD §6, v3.4): the tree is a field seen from above, a fixed portrait rectangle
    /// <see cref="Width"/> × <see cref="Height"/> units that the screen fits whole, no panning or zooming. Each
    /// branch is one lane: it starts at its own corner of the field and runs inward, one <see cref="LayerStep"/>
    /// per prerequisite layer, turning a few degrees each layer so no lane is a ruler line. Within a layer the child
    /// with the most descendants keeps the lane and the others hang off it sideways, so a long chain (Field's crops)
    /// stays straight and its side nodes (barn, bulk upgrade) read as side beds.
    ///
    /// Layer = prerequisite depth inside the branch, so a node always sits further along its lane than what it
    /// needs. Cross-branch prerequisites do not move nodes.
    /// </summary>
    public static class SkillTreeLayout
    {
        /// <summary>The field rectangle, in layout units; (0,0) is the bottom-left corner.</summary>
        public const float Width = 10f;
        public const float Height = 13.5f;
        /// <summary>Each prerequisite layer sits this much further along its lane.</summary>
        public const float LayerStep = 1.15f;
        /// <summary>Spacing between siblings, measured across the lane.</summary>
        public const float SiblingSpacing = 1f;
        /// <summary>Per-node wander, in layout units, so the rows read as beds someone dug rather than a grid.</summary>
        public const float Jitter = 0.08f;
        /// <summary>Guaranteed by the geometry: siblings sit 1 apart, layers 1.15, and jitter can close at most 0.16 of it.</summary>
        public const float MinDistance = 0.8f;

        /// <summary>One branch's lane: where it starts, which way it first runs (degrees, 0 = right, 90 = up), how much it turns per layer, and where its name goes.</summary>
        public readonly struct Lane
        {
            public readonly Branch Branch;
            public readonly LayoutPos Start;
            public readonly float Degrees;
            public readonly float TurnPerLayer;
            public readonly LayoutPos Label;

            public Lane(Branch branch, LayoutPos start, float degrees, float turnPerLayer, LayoutPos label)
            {
                Branch = branch;
                Start = start;
                Degrees = degrees;
                TurnPerLayer = turnPerLayer;
                Label = label;
            }
        }

        /// <summary>
        /// Hand and Soil hang from the top corners and run down; Helpers comes in from the right, Calendar from the
        /// left; Field, the deepest branch, runs along the bottom edge where a portrait field has its width.
        /// </summary>
        public static readonly Lane[] Lanes =
        {
            new Lane(Branch.Hand, new LayoutPos(2.3f, 12.3f), -90f, 9f, new LayoutPos(2.3f, 13.05f)),
            new Lane(Branch.Soil, new LayoutPos(7.7f, 12.3f), -90f, -9f, new LayoutPos(7.7f, 13.05f)),
            new Lane(Branch.Helpers, new LayoutPos(8.4f, 7.4f), -140f, 10f, new LayoutPos(8.6f, 8.2f)),
            new Lane(Branch.Calendar, new LayoutPos(1.5f, 6.2f), -60f, -8f, new LayoutPos(1.4f, 7.05f)),
            new Lane(Branch.Field, new LayoutPos(1.5f, 1.6f), 0f, 4f, new LayoutPos(1.5f, 0.75f)),
        };

        public static readonly Branch[] BranchOrder = { Branch.Hand, Branch.Soil, Branch.Helpers, Branch.Calendar, Branch.Field };

        public static Lane LaneOf(Branch branch)
        {
            foreach (var lane in Lanes) if (lane.Branch == branch) return lane;
            return Lanes[Lanes.Length - 1];
        }

        /// <summary>Where a branch's name sits, in layout units.</summary>
        public static LayoutPos LabelOf(Branch branch) => LaneOf(branch).Label;

        /// <summary>The point on a branch's lane at <paramref name="layer"/>, before any sibling offset or jitter.</summary>
        public static LayoutPos LanePoint(Branch branch, int layer)
        {
            var lane = LaneOf(branch);
            float x = lane.Start.X, y = lane.Start.Y;
            for (int k = 0; k < layer; k++)
            {
                double radians = (lane.Degrees + k * lane.TurnPerLayer) * Math.PI / 180.0;
                x += (float)Math.Cos(radians) * LayerStep;
                y += (float)Math.Sin(radians) * LayerStep;
            }
            return new LayoutPos(x, y);
        }

        /// <summary>The direction a lane runs in at <paramref name="layer"/>, in degrees.</summary>
        public static float DegreesAt(Branch branch, int layer)
        {
            var lane = LaneOf(branch);
            return lane.Degrees + layer * lane.TurnPerLayer;
        }

        public static Dictionary<string, LayoutPos> Compute(IReadOnlyList<SkillNode> nodes)
        {
            var byId = new Dictionary<string, SkillNode>();
            foreach (var n in nodes) byId[n.Id] = n;

            var depth = new Dictionary<string, int>();
            foreach (var n in nodes) Depth(n, byId, depth, 0);
            var weight = new Dictionary<string, int>();
            foreach (var n in nodes) Descendants(n, nodes, weight, 0);

            // Group by branch, then by layer, preserving table order.
            var layers = new Dictionary<Branch, SortedDictionary<int, List<SkillNode>>>();
            foreach (var n in nodes)
            {
                if (!layers.TryGetValue(n.Branch, out var perLayer)) layers[n.Branch] = perLayer = new SortedDictionary<int, List<SkillNode>>();
                if (!perLayer.TryGetValue(depth[n.Id], out var list)) perLayer[depth[n.Id]] = list = new List<SkillNode>();
                list.Add(n);
            }

            var result = new Dictionary<string, LayoutPos>();
            var offsets = new Dictionary<string, float>();
            foreach (var kv in layers)
            {
                foreach (var layer in kv.Value)
                {
                    var list = layer.Value;
                    var centre = LanePoint(kv.Key, layer.Key);
                    double radians = DegreesAt(kv.Key, layer.Key) * Math.PI / 180.0;
                    // Across the lane: the lane's direction turned a quarter turn to the left.
                    float px = -(float)Math.Sin(radians), py = (float)Math.Cos(radians);
                    // The child with the most descendants keeps the lane; the rest hang off it, alternating sides,
                    // each on the side its own parent leans toward when the parent is off the lane itself.
                    var order = new List<SkillNode>(list);
                    order.Sort((a, b) =>
                    {
                        int w = weight[b.Id].CompareTo(weight[a.Id]);
                        return w != 0 ? w : list.IndexOf(a).CompareTo(list.IndexOf(b));
                    });
                    int side = 1, step = 0;
                    for (int k = 0; k < order.Count; k++)
                    {
                        var n = order[k];
                        float across;
                        if (k == 0) across = 0f;
                        else
                        {
                            if (side > 0) step++;
                            across = side * step * SiblingSpacing;
                            float lean = ParentOffset(n, offsets);
                            if (k == 1 && lean != 0f) { side = lean > 0f ? 1 : -1; across = side * step * SiblingSpacing; }
                            side = -side;
                        }
                        offsets[n.Id] = across;
                        float along = Jitter * Wander(n.Id, 17);
                        float wander = Jitter * Wander(n.Id, 31);
                        float x = centre.X + px * (across + wander) + (float)Math.Cos(radians) * along;
                        float y = centre.Y + py * (across + wander) + (float)Math.Sin(radians) * along;
                        if (n.LayoutOverride.HasValue) { x += n.LayoutOverride.Value.X; y += n.LayoutOverride.Value.Y; }
                        result[n.Id] = new LayoutPos(x, y);
                    }
                }
            }
            return result;
        }

        private static float ParentOffset(SkillNode n, Dictionary<string, float> offsets)
        {
            foreach (var p in n.Prerequisites) if (offsets.TryGetValue(p, out float o) && o != 0f) return o;
            return 0f;
        }

        /// <summary>A stable -1..1 offset for a node: the same id always lands in the same place.</summary>
        private static float Wander(string id, int salt)
        {
            unchecked
            {
                int h = salt;
                for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
                h &= 0x7fffffff;
                return (h % 2001) / 1000f - 1f;
            }
        }

        private static int Depth(SkillNode n, Dictionary<string, SkillNode> byId, Dictionary<string, int> memo, int guard)
        {
            if (memo.TryGetValue(n.Id, out int d)) return d;
            int best = 0;
            if (guard < 64)
                foreach (var p in n.Prerequisites)
                    if (byId.TryGetValue(p, out var pn) && pn.Branch == n.Branch)
                        best = Math.Max(best, Depth(pn, byId, memo, guard + 1) + 1);
            memo[n.Id] = best;
            return best;
        }

        /// <summary>How many same-branch nodes need this one, directly or through others.</summary>
        private static int Descendants(SkillNode n, IReadOnlyList<SkillNode> nodes, Dictionary<string, int> memo, int guard)
        {
            if (memo.TryGetValue(n.Id, out int d)) return d;
            int count = 0;
            if (guard < 64)
                foreach (var c in nodes)
                    if (c.Branch == n.Branch && Array.IndexOf(c.Prerequisites, n.Id) >= 0)
                        count += 1 + Descendants(c, nodes, memo, guard + 1);
            memo[n.Id] = count;
            return count;
        }

        /// <summary>Bounds of a layout (min x, min y, max x, max y).</summary>
        public static (float minX, float minY, float maxX, float maxY) Bounds(Dictionary<string, LayoutPos> layout)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in layout.Values)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            return (minX, minY, maxX, maxY);
        }
    }
}
