using System;
using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEngine;

namespace TillWinter.Tests.Unity
{
    /// <summary>Presentation-side EditMode checks: theme completeness and font/string resources.</summary>
    public class ThemeTests
    {
        [Test]
        public void Theme_HasAColourForEveryBranch_AndLoadsFromResources()
        {
            var theme = TreeTheme.Load();
            Assert.IsNotNull(theme);
            var seen = new System.Collections.Generic.HashSet<Color>();
            foreach (Branch b in Enum.GetValues(typeof(Branch)))
            {
                var c = theme.BranchColor(b);
                Assert.That(c.a, Is.GreaterThan(0.5f), b + " colour is visible");
                Assert.IsTrue(seen.Add(c), b + " colour must be distinct");
            }
            Assert.IsNotNull(Resources.Load<TreeTheme>("TreeTheme"), "TreeTheme asset in Resources");
        }

        [Test]
        public void EveryNodeState_HasAVisualMapping()
        {
            // The view maps each state to ring colour / lock / pips; this guards the enum against silent additions.
            var states = Enum.GetValues(typeof(SkillTreeView.NodeState));
            CollectionAssert.AreEquivalent(new[] { "Locked", "Unaffordable", "Affordable", "Maxed" }, Array.ConvertAll((SkillTreeView.NodeState[])states, s => s.ToString()));
        }

        [Test]
        public void FontAsset_HasTurkishGlyphs()
        {
            var font = Resources.Load<TMPro.TMP_FontAsset>("NunitoSDF");
            Assert.IsNotNull(font, "NunitoSDF in Assets/Fonts/Resources");
            foreach (char c in "ÇçĞğİıÖöŞşÜü")
                Assert.IsTrue(font.HasCharacter(c), "missing glyph " + c);
        }
    }
}
