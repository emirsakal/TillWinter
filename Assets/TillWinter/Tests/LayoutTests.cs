using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class LayoutTests
    {
        private static void AssertValid(IReadOnlyList<SkillNode> nodes, Dictionary<string, LayoutPos> layout)
        {
            Assert.AreEqual(nodes.Count, layout.Count, "every node has a position");
            foreach (var n in nodes) Assert.IsTrue(layout.ContainsKey(n.Id), n.Id);
            var ids = new List<string>(layout.Keys);
            for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
            {
                var a = layout[ids[i]];
                var b = layout[ids[j]];
                float d = (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                Assert.That(d, Is.GreaterThanOrEqualTo(SkillTreeLayout.MinDistance), ids[i] + " vs " + ids[j]);
            }
        }

        [Test]
        public void Almanac_LaysOut_Deterministically_NoOverlap()
        {
            var a = SkillTreeLayout.Compute(AlmanacData.Nodes);
            var b = SkillTreeLayout.Compute(AlmanacData.Nodes);
            AssertValid(AlmanacData.Nodes, a);
            foreach (var kv in a)
            {
                Assert.AreEqual(kv.Value.X, b[kv.Key].X);
                Assert.AreEqual(kv.Value.Y, b[kv.Key].Y);
            }
        }

        [Test]
        public void Heritage_LaysOutToo()
        {
            var layout = SkillTreeLayout.Compute(HeritageData.Nodes);
            AssertValid(HeritageData.Nodes, layout);
        }

        [Test]
        public void Roots_OnBottomArc_BranchesGrowUpward_LanesDoNotOverlap()
        {
            var layout = SkillTreeLayout.Compute(AlmanacData.Nodes);
            // Roots: y equals the arc height of their lane, centre lane lowest.
            Assert.AreEqual(0f, layout["expand_field"].Y, "centre lane root at the bottom");
            Assert.That(layout["irrigation"].Y, Is.GreaterThan(0f));
            Assert.That(layout["ring_radius"].Y, Is.GreaterThan(layout["irrigation"].Y), "outer lanes higher on the arc");
            Assert.AreEqual(layout["ring_radius"].Y, layout["year_length"].Y, "symmetric arc");
            // Deeper nodes are higher than their prerequisites.
            foreach (var n in AlmanacData.Nodes)
            foreach (var p in n.Prerequisites)
                Assert.That(layout[n.Id].Y, Is.GreaterThan(layout[p].Y), n.Id + " above " + p);
            // Every node stays inside its branch lane.
            var lanes = new Dictionary<Branch, (float min, float max)>();
            foreach (var n in AlmanacData.Nodes)
            {
                var pos = layout[n.Id];
                if (!lanes.TryGetValue(n.Branch, out var r)) r = (pos.X, pos.X);
                lanes[n.Branch] = (Math.Min(r.min, pos.X), Math.Max(r.max, pos.X));
            }
            var order = SkillTreeLayout.BranchOrder;
            for (int i = 0; i < order.Length - 1; i++)
                Assert.That(lanes[order[i]].max, Is.LessThan(lanes[order[i + 1]].min), order[i] + " lane overlaps " + order[i + 1]);
        }

        [Test]
        public void LayoutOverride_Nudges_ThatNodeOnly()
        {
            var nodes = new List<SkillNode>(AlmanacData.Nodes);
            var plain = SkillTreeLayout.Compute(nodes);
            var root = AlmanacData.Get("ring_radius");
            nodes[nodes.IndexOf(root)] = new SkillNode(root.Id, root.Branch, root.Prerequisites, root.MaxLevel, root.BaseCost, root.CostGrowth,
                root.Effect, root.ValuePerLevel, root.NameKey, root.DescKey, new LayoutPos(0.2f, -0.1f));
            var nudged = SkillTreeLayout.Compute(nodes);
            Assert.That(nudged["ring_radius"].X, Is.EqualTo(plain["ring_radius"].X + 0.2f).Within(1e-5f));
            Assert.That(nudged["ring_radius"].Y, Is.EqualTo(plain["ring_radius"].Y - 0.1f).Within(1e-5f));
            Assert.AreEqual(plain["ring_water_speed"].X, nudged["ring_water_speed"].X);
        }

        [Test]
        public void CrossBranchPrerequisite_IsIgnoredForPlacement()
        {
            var nodes = new List<SkillNode>
            {
                new SkillNode("a", Branch.Hand, new string[0], 1, 1, 1.6, EffectType.RingRadius, 0.25, "a", "a"),
                new SkillNode("b", Branch.Soil, new[] { "a" }, 1, 1, 1.6, EffectType.Irrigation, 0.15, "b", "b"),
            };
            var layout = SkillTreeLayout.Compute(nodes);
            Assert.AreEqual(SkillTreeLayout.ArcCurvature * 1, layout["b"].Y, "b is a root of its own lane despite the cross-branch edge");
        }
    }
}
