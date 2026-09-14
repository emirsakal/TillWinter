using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class AlmanacTests
    {
        private static SkillNode Node(string id, params string[] pre) =>
            new SkillNode(id, Branch.Hand, pre, 1, 10, 1.6, EffectType.RingRadius, 0.25, id, id);

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
                "ring_radius", "ring_water_speed", "ring_grow_speed", "ring_harvest_speed", "ring_bonus_coins", "ring_combo",
                "irrigation", "sun", "soil_quality", "crop_value", "fertile_start",
                "expand_field", "upgrade_plot", "unlock_tomato", "unlock_corn", "unlock_pumpkin", "unlock_grapes", "unlock_golden_wheat", "bulk_upgrade",
                "apprentice_count", "apprentice_speed", "apprentice_harvest_time", "apprentice_yield", "tractor", "scarecrow", "helper_water",
                "year_length", "frost_warning", "late_frost", "greenhouse", "crow_bounty", "spring_head_start",
            };
            foreach (var id in expected) Assert.IsTrue(ids.Contains(id), "missing node " + id);
            foreach (var root in new[] { "ring_radius", "irrigation", "expand_field", "apprentice_count", "year_length" })
                Assert.AreEqual(0, AlmanacData.Get(root).Prerequisites.Length, root + " is a branch root");
            foreach (var n in AlmanacData.Nodes)
                if (Array.IndexOf(new[] { "ring_radius", "irrigation", "expand_field", "apprentice_count", "year_length" }, n.Id) < 0)
                    Assert.That(n.Prerequisites.Length, Is.GreaterThan(0), n.Id + " should have a prerequisite");
        }

        [Test]
        public void Table_MaxLevelsMatchGdd()
        {
            var max = new Dictionary<string, int>
            {
                ["ring_radius"] = 5, ["ring_water_speed"] = 5, ["ring_grow_speed"] = 5, ["ring_harvest_speed"] = 3, ["ring_bonus_coins"] = 4, ["ring_combo"] = 3,
                ["irrigation"] = 5, ["sun"] = 5, ["soil_quality"] = 6, ["crop_value"] = 5, ["fertile_start"] = 1,
                ["expand_field"] = 3, ["upgrade_plot"] = -1, ["unlock_tomato"] = 1, ["unlock_corn"] = 1, ["unlock_pumpkin"] = 1, ["unlock_grapes"] = 1, ["unlock_golden_wheat"] = 1, ["bulk_upgrade"] = 1,
                ["apprentice_count"] = 6, ["apprentice_speed"] = 4, ["apprentice_harvest_time"] = 3, ["apprentice_yield"] = 4, ["tractor"] = 3, ["scarecrow"] = 2, ["helper_water"] = 1,
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
                ["ring_radius"] = 2, ["ring_water_speed"] = 1, ["irrigation"] = 3, ["sun"] = 2, ["soil_quality"] = 4,
                ["crop_value"] = 2, ["ring_bonus_coins"] = 1, ["apprentice_count"] = 3, ["apprentice_speed"] = 2,
                ["apprentice_harvest_time"] = 1, ["apprentice_yield"] = 2, ["scarecrow"] = 1, ["year_length"] = 2,
                ["frost_warning"] = 2, ["expand_field"] = 2, ["unlock_tomato"] = 1, ["unlock_corn"] = 1, ["tractor"] = 2,
            });
            Assert.That(s.RingRadius, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(s.RingWaterMult, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(s.IrrigationFactor, Is.EqualTo(0.45f).Within(1e-5f));
            Assert.That(s.SunFactor, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(s.SoilMultiplier, Is.EqualTo(2.0f).Within(1e-5f));
            Assert.That(s.CropValueMult, Is.EqualTo(1.2).Within(1e-9));
            Assert.That(s.RingBonusMult, Is.EqualTo(1.1).Within(1e-9));
            Assert.AreEqual(3, s.ApprenticeCount);
            Assert.That(s.ApprenticeSpeed, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(s.ApprenticeHarvestTime, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.AreEqual(1.0, s.ApprenticeYield);
            Assert.AreEqual(0.15f, s.CrowSpawnChance);
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
                ["ring_radius"] = 20, ["year_length"] = 20, ["apprentice_harvest_time"] = 10, ["expand_field"] = 10, ["unlock_golden_wheat"] = 1,
            });
            Assert.AreEqual(2.5f, s.RingRadius);
            Assert.AreEqual(180f, s.YearLength);
            Assert.AreEqual(0.4f, s.ApprenticeHarvestTime);
            Assert.AreEqual(6, s.TargetGridSize);
            Assert.AreEqual(5, s.MaxTierUnlocked);
        }
    }
}
