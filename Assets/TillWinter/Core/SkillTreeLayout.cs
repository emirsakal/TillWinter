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
    /// Deterministic portrait-friendly tree layout (GDD §6): the five branch roots sit on a bottom arc,
    /// each branch grows upward in its own lane; layer = prerequisite depth inside the branch; nodes of a
    /// layer spread around the lane centre. Cross-branch prerequisites do not move nodes.
    /// </summary>
    public static class SkillTreeLayout
    {
        public const float LaneWidth = 2.8f;
        public const float LayerHeight = 1.3f;
        public const float SiblingSpacing = 0.9f;
        public const float ArcCurvature = 0.25f;
        /// <summary>Guaranteed by the lane geometry (lanes 2.8 apart, at most three siblings 0.9 apart).</summary>
        public const float MinDistance = 0.85f;

        public static readonly Branch[] BranchOrder = { Branch.Hand, Branch.Soil, Branch.Field, Branch.Helpers, Branch.Calendar };

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
                int lane = Array.IndexOf(BranchOrder, kv.Key);
                if (lane < 0) lane = BranchOrder.Length;
                float centreX = (lane - (BranchOrder.Length - 1) * 0.5f) * LaneWidth;
                float baseY = ArcCurvature * (lane - 2) * (lane - 2);
                foreach (var layer in kv.Value)
                {
                    var list = layer.Value;
                    for (int k = 0; k < list.Count; k++)
                    {
                        var n = list[k];
                        float x = centreX + (k - (list.Count - 1) * 0.5f) * SiblingSpacing;
                        float y = baseY + layer.Key * LayerHeight;
                        if (n.LayoutOverride.HasValue) { x += n.LayoutOverride.Value.X; y += n.LayoutOverride.Value.Y; }
                        result[n.Id] = new LayoutPos(x, y);
                    }
                }
            }
            return result;
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
