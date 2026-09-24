using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class LayoutTests
    {
        private static float Distance(LayoutPos a, LayoutPos b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static void AssertValid(IReadOnlyList<SkillNode> nodes, Dictionary<string, LayoutPos> layout)
        {
            Assert.AreEqual(nodes.Count, layout.Count, "every node has a position");
            foreach (var n in nodes) Assert.IsTrue(layout.ContainsKey(n.Id), n.Id);
            var ids = new List<string>(layout.Keys);
            for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
                Assert.That(Distance(layout[ids[i]], layout[ids[j]]), Is.GreaterThanOrEqualTo(SkillTreeLayout.MinDistance), ids[i] + " vs " + ids[j]);
            // The field is fitted whole to the screen, so every bed has to sit inside it with room for its own size.
            foreach (var kv in layout)
            {
                Assert.That(kv.Value.X, Is.InRange(0.4f, SkillTreeLayout.Width - 0.4f), kv.Key + " x");
                Assert.That(kv.Value.Y, Is.InRange(0.4f, SkillTreeLayout.Height - 0.4f), kv.Key + " y");
            }
        }

        [Test]
        public void Almanac_LaysOut_Deterministically_NoOverlap_InsideTheField()
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
        public void EveryBranch_HasALane_AndItsNameSitsInsideTheField()
        {
            foreach (Branch b in Enum.GetValues(typeof(Branch)))
            {
                Assert.AreEqual(b, SkillTreeLayout.LaneOf(b).Branch, b + " has a lane of its own");
                var label = SkillTreeLayout.LabelOf(b);
                Assert.That(label.X, Is.InRange(0.5f, SkillTreeLayout.Width - 0.5f), b + " label x");
                Assert.That(label.Y, Is.InRange(0.3f, SkillTreeLayout.Height - 0.3f), b + " label y");
            }
            Assert.AreEqual(5, SkillTreeLayout.BranchOrder.Length);
        }

        [Test]
        public void Roots_SitAtTheirLaneStart_AndPrerequisitesStayNearerTheStart()
        {
            var layout = SkillTreeLayout.Compute(AlmanacData.Nodes);
            float tolerance = SkillTreeLayout.Jitter * 2f + 1e-3f;
            foreach (var id in new[] { "hoe_damage", "growth", "expand_field", "apprentice_count", "year_length" })
            {
                var node = AlmanacData.Get(id);
                Assert.That(Distance(layout[id], SkillTreeLayout.LaneOf(node.Branch).Start), Is.LessThan(tolerance), id + " starts its lane");
            }
            // Everything a node needs sits nearer the lane's start than the node itself: a layer step dwarfs the jitter.
            foreach (var n in AlmanacData.Nodes)
            foreach (var p in n.Prerequisites)
            {
                var start = SkillTreeLayout.LaneOf(n.Branch).Start;
                Assert.That(Distance(layout[n.Id], start), Is.GreaterThan(Distance(layout[p], start)), n.Id + " must sit further along than " + p);
            }
        }

        [Test]
        public void EachLayer_TurnsALittle_SoNoLaneIsARulerLine()
        {
            foreach (var lane in SkillTreeLayout.Lanes)
            {
                Assert.That(Math.Abs(lane.TurnPerLayer), Is.GreaterThan(0f).And.LessThan(15f), lane.Branch.ToString());
                Assert.That(SkillTreeLayout.DegreesAt(lane.Branch, 3) - SkillTreeLayout.DegreesAt(lane.Branch, 0),
                    Is.EqualTo(3f * lane.TurnPerLayer).Within(1e-4f), "the turn accumulates with depth");
            }
        }

        [Test]
        public void TheLongestChain_KeepsItsLane_SideNodesHangOffIt()
        {
            // Field's crops are one chain; barn and bulk upgrade hang off it. The chain node of each layer sits on
            // the lane point itself (give or take jitter), the side node a sibling spacing away across the lane.
            var layout = SkillTreeLayout.Compute(AlmanacData.Nodes);
            float tolerance = SkillTreeLayout.Jitter * 2f + 1e-3f;
            Assert.That(Distance(layout["unlock_tomato"], SkillTreeLayout.LanePoint(Branch.Field, 1)), Is.LessThan(tolerance));
            Assert.That(Distance(layout["unlock_pumpkin"], SkillTreeLayout.LanePoint(Branch.Field, 4)), Is.LessThan(tolerance));
            Assert.That(Distance(layout["barn"], SkillTreeLayout.LanePoint(Branch.Field, 1)),
                Is.EqualTo(SkillTreeLayout.SiblingSpacing).Within(tolerance));
            Assert.That(Distance(layout["bulk_upgrade"], SkillTreeLayout.LanePoint(Branch.Field, 4)),
                Is.EqualTo(SkillTreeLayout.SiblingSpacing).Within(tolerance));
        }

        [Test]
        public void LayoutOverride_Nudges_ThatNodeOnly()
        {
            var nodes = new List<SkillNode>(AlmanacData.Nodes);
            var plain = SkillTreeLayout.Compute(nodes);
            var root = AlmanacData.Get("hoe_damage");
            nodes[nodes.IndexOf(root)] = new SkillNode(root.Id, root.Branch, root.Prerequisites, root.MaxLevel, root.BaseCost, root.CostGrowth,
                root.Effect, root.ValuePerLevel, root.NameKey, root.DescKey, new LayoutPos(0.2f, -0.1f));
            var nudged = SkillTreeLayout.Compute(nodes);
            Assert.That(nudged["hoe_damage"].X, Is.EqualTo(plain["hoe_damage"].X + 0.2f).Within(1e-5f));
            Assert.That(nudged["hoe_damage"].Y, Is.EqualTo(plain["hoe_damage"].Y - 0.1f).Within(1e-5f));
            Assert.AreEqual(plain["stamina_depot"].X, nudged["stamina_depot"].X);
        }

        [Test]
        public void CrossBranchPrerequisite_IsIgnoredForPlacement()
        {
            var nodes = new List<SkillNode>
            {
                new SkillNode("a", Branch.Hand, new string[0], 1, 1, 1.6, EffectType.StrikeDamage, 1, "a", "a"),
                new SkillNode("b", Branch.Soil, new[] { "a" }, 1, 1, 1.6, EffectType.Growth, 0.15, "b", "b"),
            };
            var layout = SkillTreeLayout.Compute(nodes);
            Assert.That(Distance(layout["b"], SkillTreeLayout.LaneOf(Branch.Soil).Start), Is.LessThan(SkillTreeLayout.Jitter * 2f + 1e-3f),
                "b is a root of its own branch despite the cross-branch edge");
        }
    }
}
