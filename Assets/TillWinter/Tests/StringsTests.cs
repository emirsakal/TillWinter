using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>The EN string table covers every node in both trees and every description formats at level 0 and at max.</summary>
    public class StringsTests
    {
        private const string TablePath = "Assets/TillWinter/Unity/Localization/en.json";

        private static Dictionary<string, string> LoadTable()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), TablePath);
            Assert.IsTrue(File.Exists(path), "string table missing: " + path);
            var raw = MiniJson.ParseObject(File.ReadAllText(path));
            var table = new Dictionary<string, string>();
            foreach (var kv in raw) table[kv.Key] = kv.Value as string ?? "";
            return table;
        }

        [Test]
        public void EveryNode_HasNameAndDescriptionKeys()
        {
            var table = LoadTable();
            var nodes = new List<SkillNode>(AlmanacData.Nodes);
            nodes.AddRange(HeritageData.Nodes);
            foreach (var n in nodes)
            {
                Assert.IsTrue(table.ContainsKey(n.NameKey), "missing " + n.NameKey);
                Assert.IsTrue(table.ContainsKey(n.DescKey), "missing " + n.DescKey);
                Assert.IsNotEmpty(table[n.NameKey]);
                Assert.IsNotEmpty(table[n.DescKey]);
            }
            foreach (var b in SkillTreeLayout.BranchOrder) Assert.IsTrue(table.ContainsKey("branch." + b), "branch." + b);
            foreach (var c in new FarmConfig().Crops) Assert.IsTrue(table.ContainsKey(c.Key), c.Key);
        }

        [Test]
        public void EveryDescription_FormatsAtLevel0_AndAtMax_WithoutUnknownPlaceholders()
        {
            var table = LoadTable();
            var sim = new FarmSim(new FarmConfig(), 1);
            var nodes = new List<SkillNode>(AlmanacData.Nodes);
            nodes.AddRange(HeritageData.Nodes);
            foreach (var n in nodes)
            {
                string template = table[n.DescKey];
                Assert.DoesNotThrow(() => NodeText.Fill(template, NodeText.Values(sim, n.Id)), n.Id + " at level 0");
                string at0 = NodeText.Fill(template, NodeText.Values(sim, n.Id));
                Assert.IsFalse(at0.Contains("{"), n.Id + " leaves a placeholder unfilled at 0: " + at0);

                int max = n.MaxLevel < 0 ? 3 : n.MaxLevel;
                sim.DebugSetLevel(n.Id, max);
                string atMax = NodeText.Fill(template, NodeText.Values(sim, n.Id));
                Assert.IsFalse(atMax.Contains("{"), n.Id + " leaves a placeholder unfilled at max: " + atMax);
                sim.DebugSetLevel(n.Id, 0);
            }
        }

        [Test]
        public void Fill_LeavesUnknownPlaceholders_AndNeverThrows()
        {
            var v = new Dictionary<string, string> { ["cur"] = "1" };
            Assert.AreEqual("1 and {zzz} and {", NodeText.Fill("{cur} and {zzz} and {", v));
            Assert.AreEqual("", NodeText.Fill(null, v));
            Assert.AreEqual("plain", NodeText.Fill("plain", null));
        }
    }
}
