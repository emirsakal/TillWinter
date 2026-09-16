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

        private static float Radius(LayoutPos p) => (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);

        /// <summary>Signed degrees between a node's direction from the centre and its branch axis.</summary>
        private static float AngleOff(LayoutPos p, Branch branch)
        {
            float angle = (float)(Math.Atan2(p.Y, p.X) * 180.0 / Math.PI);
            float diff = Math.Abs(Mathf(angle - SkillTreeLayout.AngleOf(branch)));
            return diff;
        }

        /// <summary>Wraps a degree difference into -180..180.</summary>
        private static float Mathf(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;
            return degrees;
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
        public void Branches_LeaveTheCentre_AtEvenAngles()
        {
            var order = SkillTreeLayout.BranchOrder;
            Assert.AreEqual(90f, SkillTreeLayout.AngleOf(order[0]), "the first branch points up");
            for (int i = 0; i < order.Length; i++)
            {
                float expected = 90f - i * 72f;
                Assert.That(SkillTreeLayout.AngleOf(order[i]), Is.EqualTo(expected).Within(1e-4f), order[i].ToString());
            }
        }

        [Test]
        public void Roots_SitOnTheInnerRing_AndPrerequisitesStayCloserToTheCentre()
        {
            var layout = SkillTreeLayout.Compute(AlmanacData.Nodes);
            // A branch root (no prerequisite inside its branch) sits on the root ring.
            Assert.That(Radius(layout["ring_radius"]), Is.EqualTo(SkillTreeLayout.RootRadius).Within(1e-3f));
            Assert.That(Radius(layout["irrigation"]), Is.EqualTo(SkillTreeLayout.RootRadius).Within(1e-3f));
            Assert.That(Radius(layout["expand_field"]), Is.EqualTo(SkillTreeLayout.RootRadius).Within(1e-3f));
            // Everything a node needs sits closer to the centre than the node itself.
            foreach (var n in AlmanacData.Nodes)
            foreach (var p in n.Prerequisites)
                Assert.That(Radius(layout[n.Id]), Is.GreaterThan(Radius(layout[p]) - 1e-3f), n.Id + " outside " + p);
        }

        [Test]
        public void EveryNode_StaysInItsBranchSector()
        {
            var layout = SkillTreeLayout.Compute(AlmanacData.Nodes);
            foreach (var n in AlmanacData.Nodes)
                Assert.That(AngleOff(layout[n.Id], n.Branch), Is.LessThan(36f), n.Id + " drifted out of its branch sector");
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
            Assert.That(Radius(layout["b"]), Is.EqualTo(SkillTreeLayout.RootRadius).Within(1e-3f), "b is a root of its own branch despite the cross-branch edge");
        }
    }
}
