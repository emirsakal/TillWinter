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
    /// Deterministic radial layout (GDD §6). Five branches leave one centre 72° apart and grow outward, but nothing
    /// about it is straight: each branch curves a little further round with every layer, siblings fan out along an
    /// arc, and every node carries a small fixed offset of its own. A perfect star read as a diagram rather than a
    /// thing that grew, so the shape is a rounded canopy with five lobes, not five spokes.
    ///
    /// Layer = prerequisite depth inside the branch, so a node always sits further out than what it needs.
    /// Cross-branch prerequisites do not move nodes.
    /// </summary>
    public static class SkillTreeLayout
    {
        /// <summary>Distance from the centre to a branch's first nodes. Wide enough that a branch's fan cannot reach its neighbour's.</summary>
        public const float RootRadius = 3.2f;
        /// <summary>Each prerequisite layer sits this much further out.</summary>
        public const float LayerStep = 1.6f;
        /// <summary>Spacing between siblings, measured along their arc so it holds at any radius.</summary>
        public const float SiblingSpacing = 1.15f;
        /// <summary>How far each layer swings round: the gentle curve that keeps a branch from reading as a spoke.</summary>
        public const float CurveDegreesPerLayer = 9f;
        /// <summary>
        /// Per-node variation. Both are measured in layout units, never degrees: a fixed angle is a small nudge near
        /// the centre and a large one at the rim, which would pull the outermost siblings under
        /// <see cref="MinDistance"/>. As an arc offset the worst case is the same wherever a node sits.
        /// </summary>
        public const float JitterRadius = 0.12f;
        public const float JitterArc = 0.1f;
        /// <summary>Guaranteed by the geometry: siblings sit 1.15 apart along their arc and jitter can close at most 0.2 of it.</summary>
        public const float MinDistance = 0.85f;
        /// <summary>The first branch points straight up; the rest follow clockwise.</summary>
        public const float FirstAngleDegrees = 90f;

        public static readonly Branch[] BranchOrder = { Branch.Hand, Branch.Soil, Branch.Field, Branch.Helpers, Branch.Calendar };

        /// <summary>The direction a branch leaves the centre in, in degrees (90 = up), before any curve.</summary>
        public static float AngleOf(Branch branch)
        {
            int index = Array.IndexOf(BranchOrder, branch);
            if (index < 0) index = BranchOrder.Length;
            return FirstAngleDegrees - index * (360f / BranchOrder.Length);
        }

        /// <summary>The direction a branch faces once it has curved out to <paramref name="layer"/>.</summary>
        public static float AxisAt(Branch branch, int layer) => AngleOf(branch) + layer * CurveDegreesPerLayer;

        public static Dictionary<string, LayoutPos> Compute(IReadOnlyList<SkillNode> nodes)
        {
            var byId = new Dictionary<string, SkillNode>();
            foreach (var n in nodes) byId[n.Id] = n;

            var depth = new Dictionary<string, int>();
            foreach (var n in nodes) Depth(n, byId, depth, 0);

            // Group by branch, then by layer, preserving table order.
            var layers = new Dictionary<Branch, SortedDictionary<int, List<SkillNode>>>();
            foreach (var n in nodes)
            {
                if (!layers.TryGetValue(n.Branch, out var perLayer)) layers[n.Branch] = perLayer = new SortedDictionary<int, List<SkillNode>>();
                if (!perLayer.TryGetValue(depth[n.Id], out var list)) perLayer[depth[n.Id]] = list = new List<SkillNode>();
                list.Add(n);
            }

            var result = new Dictionary<string, LayoutPos>();
            foreach (var kv in layers)
            {
                foreach (var layer in kv.Value)
                {
                    var list = layer.Value;
                    float baseRadius = RootRadius + layer.Key * LayerStep;
                    float axis = AxisAt(kv.Key, layer.Key);
                    for (int k = 0; k < list.Count; k++)
                    {
                        var n = list[k];
                        // Siblings fan along the arc, so they stay the same distance apart however far out they are,
                        // and the node's own wander is an arc offset for the same reason.
                        float arc = (k - (list.Count - 1) * 0.5f) * SiblingSpacing + Jitter(n.Id, 31) * JitterArc;
                        float radius = baseRadius + Jitter(n.Id, 17) * JitterRadius;
                        float degrees = axis + arc / Math.Max(0.001f, radius) * (180.0f / (float)Math.PI);

                        double radians = degrees * Math.PI / 180.0;
                        float x = (float)Math.Cos(radians) * radius;
                        float y = (float)Math.Sin(radians) * radius;
                        if (n.LayoutOverride.HasValue) { x += n.LayoutOverride.Value.X; y += n.LayoutOverride.Value.Y; }
                        result[n.Id] = new LayoutPos(x, y);
                    }
                }
            }
            return result;
        }

        /// <summary>A stable -1..1 offset for a node: the same id always lands in the same place.</summary>
        private static float Jitter(string id, int salt)
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
