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
    /// Deterministic star layout (GDD §6): the five branches leave one centre at fixed angles, 72° apart, and grow
    /// outward; layer = prerequisite depth inside the branch, so a node always sits further out than what it needs.
    /// Nodes of the same layer spread perpendicular to their branch. Cross-branch prerequisites do not move nodes.
    /// (Side-by-side lanes crowded the middle of the canvas and read as one block on a phone.)
    /// </summary>
    public static class SkillTreeLayout
    {
        /// <summary>Distance from the centre to a branch's root.</summary>
        public const float RootRadius = 2.4f;
        /// <summary>Each prerequisite layer sits this much further out.</summary>
        public const float LayerStep = 1.55f;
        /// <summary>Spread of nodes sharing a layer, perpendicular to the branch.</summary>
        public const float SiblingSpacing = 1.05f;
        /// <summary>Guaranteed by the geometry (branches 72° apart, siblings at least 1.05 apart).</summary>
        public const float MinDistance = 0.85f;
        /// <summary>The first branch points straight up; the rest follow clockwise.</summary>
        public const float FirstAngleDegrees = 90f;

        public static readonly Branch[] BranchOrder = { Branch.Hand, Branch.Soil, Branch.Field, Branch.Helpers, Branch.Calendar };

        /// <summary>The direction a branch grows in, in degrees (90 = up).</summary>
        public static float AngleOf(Branch branch)
        {
            int index = Array.IndexOf(BranchOrder, branch);
            if (index < 0) index = BranchOrder.Length;
            return FirstAngleDegrees - index * (360f / BranchOrder.Length);
        }

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
                double radians = AngleOf(kv.Key) * Math.PI / 180.0;
                float dirX = (float)Math.Cos(radians), dirY = (float)Math.Sin(radians);
                float perpX = -dirY, perpY = dirX;
                foreach (var layer in kv.Value)
                {
                    var list = layer.Value;
                    float radius = RootRadius + layer.Key * LayerStep;
                    for (int k = 0; k < list.Count; k++)
                    {
                        var n = list[k];
                        float spread = (k - (list.Count - 1) * 0.5f) * SiblingSpacing;
                        float x = dirX * radius + perpX * spread;
                        float y = dirY * radius + perpY * spread;
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
