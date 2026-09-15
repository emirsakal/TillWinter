using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Both string tables (EN, TR) cover every node in both trees, share one key set, and every description formats at level 0 and at max.</summary>
    [TestFixture("en", NumberStyle.English)]
    [TestFixture("tr", NumberStyle.Turkish)]
    public class StringsTests
    {
        private readonly string _lang;
        private readonly NumberStyle _style;

        public StringsTests(string lang, NumberStyle style) { _lang = lang; _style = style; }

        [SetUp] public void UseStyle() => NumberFormat.Style = _style;
        [TearDown] public void ResetStyle() => NumberFormat.Style = NumberStyle.English;

        private Dictionary<string, string> LoadTable() => LoadTable(_lang);

        public static Dictionary<string, string> LoadTable(string lang)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/TillWinter/Unity/Localization/" + lang + ".json");
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
        public void KeySet_MatchesEnglish_AndNoStringIsEmpty()
        {
            var table = LoadTable();
            var en = LoadTable("en");
            foreach (var k in en.Keys) Assert.IsTrue(table.ContainsKey(k), _lang + " is missing " + k);
            foreach (var k in table.Keys) Assert.IsTrue(en.ContainsKey(k), _lang + " has an extra key " + k);
            foreach (var kv in table) Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value), _lang + " empty: " + kv.Key);
        }

        [Test]
        public void Placeholders_MatchEnglish_AndNeverCarryASuffix()
        {
            var table = LoadTable();
            var en = LoadTable("en");
            foreach (var kv in table)
            {
                CollectionAssert.AreEquivalent(Placeholders(en[kv.Key]), Placeholders(kv.Value), _lang + " placeholders differ: " + kv.Key);
                string v = kv.Value;
                for (int i = v.IndexOf('}'); i >= 0 && i + 1 < v.Length; i = v.IndexOf('}', i + 1))
                {
                    char next = v[i + 1];
                    Assert.IsFalse(char.IsLetter(next) || next == '\'' || next == '’', _lang + " " + kv.Key + ": a suffix is attached to a placeholder: " + v);
                }
            }
        }

        [Test]
        public void NumberStyle_UsesTheLanguageSeparator()
        {
            string s = NumberFormat.Short(1234);
            Assert.AreEqual(_style == NumberStyle.Turkish ? "1,2K" : "1.2K", s);
            var buf = new char[16];
            int n = NumberFormat.Short(1234, buf);
            Assert.AreEqual(s, new string(buf, 0, n));
        }

        private static List<string> Placeholders(string s)
        {
            var list = new List<string>();
            for (int i = s.IndexOf('{'); i >= 0; i = s.IndexOf('{', i + 1))
            {
                int j = s.IndexOf('}', i);
                if (j < 0) break;
                list.Add(s.Substring(i, j - i + 1));
            }
            return list;
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
