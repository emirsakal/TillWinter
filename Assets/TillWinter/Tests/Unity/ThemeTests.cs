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
        public void HeritageTheme_AndHudTheme_FullyPopulated()
        {
            var h = TreeTheme.Load("HeritageTheme");
            Assert.IsNotNull(Resources.Load<TreeTheme>("HeritageTheme"), "HeritageTheme asset");
            var a = TreeTheme.Load("TreeTheme");
            foreach (Branch b in Enum.GetValues(typeof(Branch)))
            {
                Assert.That(h.BranchColor(b).a, Is.GreaterThan(0.5f));
                Assert.AreNotEqual(a.BranchColor(b), h.BranchColor(b), b + " heritage colour must differ from the almanac");
            }
            Assert.AreNotEqual(a.Paper, h.Paper);
            foreach (var f in typeof(TreeTheme).GetFields())
                if (f.FieldType == typeof(Color)) Assert.That(((Color)f.GetValue(h)).a, Is.GreaterThan(0f), f.Name);

            var hud = HudTheme.Load();
            Assert.IsNotNull(Resources.Load<HudTheme>("HudTheme"), "HudTheme asset");
            foreach (var f in typeof(HudTheme).GetFields())
            {
                if (f.FieldType == typeof(Color)) Assert.That(((Color)f.GetValue(hud)).a, Is.GreaterThan(0f), f.Name);
                if (f.FieldType == typeof(float)) Assert.AreNotEqual(0f, (float)f.GetValue(hud), f.Name);
            }
            foreach (Season sn in Enum.GetValues(typeof(Season))) Assert.That(hud.SeasonColor(sn).a, Is.GreaterThan(0.5f));
        }

        [Test]
        public void FarmDecor_HasEntriesForGenerations2To8_NoDuplicateIds()
        {
            var set = FarmDecorSet.Load();
            Assert.IsNotNull(Resources.Load<FarmDecorSet>("FarmDecor"), "FarmDecor asset");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var i in set.Items) Assert.IsTrue(ids.Add(i.Id), "duplicate " + i.Id);
            for (int g = 2; g <= 8; g++)
            {
                bool any = false;
                foreach (var i in set.Items) if (i.MinGeneration == g) any = true;
                Assert.IsTrue(any, "generation " + g + " adds something");
            }
            int count = 0;
            foreach (var _ in set.ForGeneration(1)) count++;
            Assert.AreEqual(0, count, "generation 1 has no decor");
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
