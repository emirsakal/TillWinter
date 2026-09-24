using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class AlmanacTests
    {
        private static SkillNode Node(string id, params string[] pre) =>
            new SkillNode(id, Branch.Hand, pre, 1, 10, 1.6, EffectType.StrikeDamage, 1, id, id);

        private static readonly string[] Roots = { "hoe_damage", "growth", "expand_field", "apprentice_count", "year_length" };

        [Test]
        public void Table_IsValid()
        {
            var errors = AlmanacData.Validate(AlmanacData.Nodes);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Table_ContainsEveryGddNode_WithRootsHavingNoPrerequisites()
        {
            var ids = new HashSet<string>();
            foreach (var n in AlmanacData.Nodes) ids.Add(n.Id);
            var expected = new[]
            {
                "hoe_damage", "stamina_depot", "stamina_regen", "steady_hand", "lucky_hoe", "strike_speed", "splash", "break_bonus", "reap_combo",
                "growth", "soft_ground", "soil_quality", "crop_value", "early_thaw", "beehive",
                "expand_field", "unlock_tomato", "upgrade_plot", "unlock_corn", "unlock_pumpkin", "unlock_grapes", "unlock_golden_wheat", "bulk_upgrade", "barn",
                "apprentice_count", "apprentice_speed", "apprentice_work_time", "apprentice_yield", "apprentice_dig", "scarecrow", "tractor", "farm_dog", "hens",
                "year_length", "frost_warning", "late_frost", "greenhouse", "crow_bounty", "spring_head_start",
            };
            foreach (var id in expected) Assert.IsTrue(ids.Contains(id), "missing node " + id);
            Assert.AreEqual(expected.Length, AlmanacData.Nodes.Length, "no node outside the GDD table");
            foreach (var root in Roots)
                Assert.AreEqual(0, AlmanacData.Get(root).Prerequisites.Length, root + " is a branch root");
            foreach (var n in AlmanacData.Nodes)
                if (Array.IndexOf(Roots, n.Id) < 0)
                    Assert.That(n.Prerequisites.Length, Is.GreaterThan(0), n.Id + " should have a prerequisite");
        }

        [Test]
        public void Table_MaxLevelsMatchGdd()
        {
            var max = new Dictionary<string, int>
            {
                ["hoe_damage"] = 8, ["stamina_depot"] = 5, ["stamina_regen"] = 5, ["steady_hand"] = 3, ["lucky_hoe"] = 6, ["strike_speed"] = 4, ["splash"] = 2, ["break_bonus"] = 4, ["reap_combo"] = 3,
                ["growth"] = 5, ["soft_ground"] = 5, ["soil_quality"] = 6, ["crop_value"] = 5, ["early_thaw"] = 1, ["beehive"] = 1,
                ["expand_field"] = 3, ["unlock_tomato"] = 1, ["upgrade_plot"] = -1, ["unlock_corn"] = 1, ["unlock_pumpkin"] = 1, ["unlock_grapes"] = 1, ["unlock_golden_wheat"] = 1, ["bulk_upgrade"] = 1, ["barn"] = 3,
                ["apprentice_count"] = 6, ["apprentice_speed"] = 4, ["apprentice_work_time"] = 3, ["apprentice_yield"] = 4, ["apprentice_dig"] = 3, ["scarecrow"] = 2, ["tractor"] = 3, ["farm_dog"] = 1, ["hens"] = 1,
                ["year_length"] = 6, ["frost_warning"] = 2, ["late_frost"] = 1, ["greenhouse"] = 3, ["crow_bounty"] = 3, ["spring_head_start"] = 1,
            };
            foreach (var kv in max) Assert.AreEqual(kv.Value, AlmanacData.Get(kv.Key).MaxLevel, kv.Key);
        }

        [Test]
        public void Validate_CatchesMissingPrerequisite()
        {
            var nodes = new[] { Node("a"), Node("b", "nope") };
            var errors = AlmanacData.Validate(nodes);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("unknown prerequisite nope", errors[0]);
        }

        [Test]
        public void Validate_CatchesCycle_AndDuplicate_AndSelfReference()
        {
            var cycle = AlmanacData.Validate(new[] { Node("a", "c"), Node("b", "a"), Node("c", "b") });
            Assert.IsTrue(cycle.Exists(e => e.StartsWith("cycle")), string.Join("; ", cycle));

            var dup = AlmanacData.Validate(new[] { Node("a"), Node("a") });
            Assert.IsTrue(dup.Exists(e => e.Contains("duplicate id: a")));

            var self = AlmanacData.Validate(new[] { Node("a", "a") });
            Assert.IsTrue(self.Exists(e => e.Contains("depends on itself")));
        }

        [Test]
        public void FarmSim_RefusesInvalidTable()
        {
            Assert.Throws<InvalidOperationException>(() => new FarmSim(new FarmConfig(), 1, new[] { Node("a", "missing") }));
        }

        [Test]
        public void StatResolver_ResolvesGddNumbers()
        {
            var cfg = new FarmConfig();
            var s = StatResolver.Resolve(cfg, new Dictionary<string, int>
            {
                ["hoe_damage"] = 2, ["steady_hand"] = 1, ["lucky_hoe"] = 2, ["strike_speed"] = 2, ["splash"] = 1,
                ["stamina_depot"] = 2, ["stamina_regen"] = 2, ["break_bonus"] = 3, ["reap_combo"] = 1,
                ["growth"] = 2, ["soft_ground"] = 2, ["soil_quality"] = 4, ["crop_value"] = 2,
                ["apprentice_count"] = 3, ["apprentice_speed"] = 2, ["apprentice_work_time"] = 1, ["apprentice_yield"] = 2, ["apprentice_dig"] = 1,
                ["scarecrow"] = 1, ["year_length"] = 2, ["frost_warning"] = 2, ["expand_field"] = 2, ["unlock_tomato"] = 1, ["unlock_corn"] = 1, ["tractor"] = 2,
            });
            // The hoe: 3 + 2; 0.3 + 0.03; 0.1 + 2 × 0.03; 0.35 - 2 × 0.04; one splash level.
            Assert.That(s.StrikeDamage, Is.EqualTo(5.0).Within(1e-9));
            Assert.That(s.CritWindow, Is.EqualTo(0.33f).Within(1e-5f));
            Assert.That(s.CritChance, Is.EqualTo(0.16).Within(1e-9));
            Assert.That(s.StrikeCooldown, Is.EqualTo(0.27f).Within(1e-5f));
            Assert.That(s.SplashShare, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(s.StaminaMax, Is.EqualTo(130f).Within(1e-5f));
            Assert.That(s.StaminaRegen, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(s.BreakBonusMult, Is.EqualTo(1.3).Within(1e-9));
            Assert.That(s.ComboPerCrop, Is.EqualTo(0.12).Within(1e-9));
            // Soil: 1 + 2 × 0.15; 1 - 2 × 0.06; 1 + 4 × 0.25; 1 + 2 × 0.1.
            Assert.That(s.GrowthMult, Is.EqualTo(1.3f).Within(1e-5f));
            Assert.That(s.HpMult, Is.EqualTo(0.88f).Within(1e-5f));
            Assert.That(s.SoilMultiplier, Is.EqualTo(2.0f).Within(1e-5f));
            Assert.That(s.CropValueMult, Is.EqualTo(1.2).Within(1e-9));
            // Helpers: 1.5 + 2 × 0.5; 1.0 - 0.2; yield table; 0.5 + 0.25.
            Assert.AreEqual(3, s.ApprenticeCount);
            Assert.That(s.ApprenticeSpeed, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(s.ApprenticeWorkTime, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.AreEqual(1.0, s.ApprenticeYield);
            Assert.That(s.ApprenticeDigShare, Is.EqualTo(0.75).Within(1e-9));
            Assert.AreEqual(cfg.CrowSpawnChance, s.CrowSpawnChance);
            Assert.AreEqual(1, s.ScarecrowCount);
            Assert.AreEqual(120f, s.YearLength);
            Assert.AreEqual(20f, s.FrostWarningSeconds);
            Assert.AreEqual(5, s.TargetGridSize);
            Assert.AreEqual(2, s.MaxTierUnlocked);
            Assert.AreEqual(2, s.TractorLevel);
        }

        [Test]
        public void StatResolver_Caps()
        {
            var cfg = new FarmConfig();
            var s = StatResolver.Resolve(cfg, new Dictionary<string, int>
            {
                ["steady_hand"] = 20, ["lucky_hoe"] = 20, ["strike_speed"] = 20, ["soft_ground"] = 99,
                ["year_length"] = 20, ["apprentice_work_time"] = 10, ["expand_field"] = 10, ["unlock_golden_wheat"] = 1,
            });
            Assert.AreEqual(cfg.MaxCritWindow, s.CritWindow);
            Assert.AreEqual(cfg.MaxCritChance, s.CritChance);
            Assert.AreEqual(cfg.MinStrikeCooldown, s.StrikeCooldown);
            Assert.AreEqual(0.2f, s.HpMult, "the ground never softens past a fifth");
            Assert.AreEqual(180f, s.YearLength);
            Assert.AreEqual(0.4f, s.ApprenticeWorkTime);
            Assert.AreEqual(6, s.TargetGridSize);
            Assert.AreEqual(5, s.MaxTierUnlocked);
        }
    }
}
