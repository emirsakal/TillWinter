using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Every node carries an icon key (the presentation-side test checks the key exists in the atlas).</summary>
    public class IconTests
    {
        [Test]
        public void EveryAlmanacNode_HasAnIconKey()
        {
            foreach (var n in AlmanacData.Nodes) Assert.IsFalse(string.IsNullOrEmpty(n.IconKey), n.Id + " icon");
        }

        [Test]
        public void EveryHeritageNode_HasAnIconKey()
        {
            foreach (var n in HeritageData.Nodes) Assert.IsFalse(string.IsNullOrEmpty(n.IconKey), n.Id + " icon");
        }

        [Test]
        public void IconTables_HaveNoOrphanEntries()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var n in AlmanacData.Nodes) ids.Add(n.Id);
            foreach (var n in HeritageData.Nodes) ids.Add(n.Id);
            foreach (var k in AlmanacData.Icons.Keys) Assert.IsTrue(ids.Contains(k), "orphan almanac icon " + k);
            foreach (var k in HeritageData.Icons.Keys) Assert.IsTrue(ids.Contains(k), "orphan heritage icon " + k);
        }
    }
}
