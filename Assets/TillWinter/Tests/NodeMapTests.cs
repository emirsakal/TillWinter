using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Every node does something: the explicit map of every node id in both tables to how it is applied.</summary>
    public class NodeMapTests
    {
        /// <summary>
        /// A new node must be added here (and implemented) or this test fails; nothing can be added silently.
        /// "stat" = changes a Stats field at level 1; "purchase" = FarmSim.ApplyPurchase side effect.
        /// </summary>
        private static readonly Dictionary<string, string> Applied = new Dictionary<string, string>
        {
            // Almanac — the hoe
            ["hoe_damage"] = "stat", ["stamina_depot"] = "stat", ["stamina_regen"] = "stat", ["steady_hand"] = "stat", ["lucky_hoe"] = "stat",
            ["strike_speed"] = "stat", ["splash"] = "stat", ["break_bonus"] = "stat", ["reap_combo"] = "stat",
            // Almanac — soil
            ["growth"] = "stat", ["soft_ground"] = "stat", ["soil_quality"] = "stat", ["crop_value"] = "stat", ["early_thaw"] = "stat", ["beehive"] = "stat",
            // Almanac — field
            ["expand_field"] = "stat", ["upgrade_plot"] = "purchase", ["unlock_tomato"] = "stat", ["unlock_corn"] = "stat", ["unlock_pumpkin"] = "stat",
            ["unlock_grapes"] = "stat", ["unlock_golden_wheat"] = "stat", ["bulk_upgrade"] = "stat", ["barn"] = "stat",
            // Almanac — helpers
            ["apprentice_count"] = "stat", ["apprentice_speed"] = "stat", ["apprentice_work_time"] = "stat", ["apprentice_yield"] = "stat",
            ["apprentice_dig"] = "stat", ["tractor"] = "stat", ["scarecrow"] = "stat", ["farm_dog"] = "stat", ["hens"] = "stat",
            // Almanac — calendar
            ["year_length"] = "stat", ["frost_warning"] = "stat", ["late_frost"] = "stat", ["greenhouse"] = "stat", ["crow_bounty"] = "stat", ["spring_head_start"] = "stat",
            // Heritage
            ["h_start_damage"] = "stat", ["h_strike_speed"] = "stat", ["h_break_coins"] = "stat", ["h_start_growth"] = "stat",
            ["h_start_soft"] = "stat", ["h_global_growth"] = "stat", ["h_unlock_rain_cloud"] = "stat", ["h_start_field"] = "stat",
            ["h_start_tomato"] = "stat", ["h_golden_crop"] = "stat", ["h_free_apprentice"] = "stat", ["h_apprentice_yield"] = "stat",
            ["h_scarecrow_immunity"] = "stat+scarecrow2", ["h_start_year_length"] = "stat", ["h_greenhouse_x2"] = "stat", ["h_almanac_discount"] = "stat",
            ["h_hoe_master"] = "stat", ["h_steward"] = "stat", ["h_long_summer"] = "stat", ["h_rich_soil"] = "stat",
        };

        [Test]
        public void EveryNodeInBothTables_IsAppliedByStatResolverOrAFeatureSwitch()
        {
            var ids = new HashSet<string>();
            foreach (var n in AlmanacData.Nodes) ids.Add(n.Id);
            foreach (var n in HeritageData.Nodes) ids.Add(n.Id);
            CollectionAssert.AreEquivalent(Applied.Keys, ids, "table ids and the explicit map must match exactly");

            var cfg = TestConfig.Classic();
            var none = new Dictionary<string, int>();
            var baseline = StatResolver.Resolve(cfg, none, none);
            foreach (var kv in Applied)
            {
                string id = kv.Key;
                bool heritage = HeritageData.Get(id) != null;
                var almanac = new Dictionary<string, int>();
                var her = new Dictionary<string, int>();
                if (kv.Value == "stat+scarecrow2") almanac["scarecrow"] = 2;
                (heritage ? her : almanac)[id] = 1;
                var with = StatResolver.Resolve(cfg, almanac, her);
                if (kv.Value == "purchase")
                {
                    Assert.IsFalse(Differs(baseline, with), id + " is purchase-applied and must not change stats");
                    continue;
                }
                var reference = kv.Value == "stat+scarecrow2" ? StatResolver.Resolve(cfg, new Dictionary<string, int> { ["scarecrow"] = 2 }, none) : baseline;
                Assert.IsTrue(Differs(reference, with), id + " at level 1 changes no stat");
            }
        }

        [Test]
        public void TheHoeMaster_SpeedsTheHoe_AndPaysItsBreaksMore_WhileTheStewardPaysHelpersOnly()
        {
            var cfg = TestConfig.Classic();
            var none = new Dictionary<string, int>();
            var plain = StatResolver.Resolve(cfg, none, none);
            var master = StatResolver.Resolve(cfg, none, new Dictionary<string, int> { ["h_hoe_master"] = 2 });
            double speed = HeritageData.Get("h_hoe_master").ValuePerLevel * 2;
            Assert.AreEqual(plain.StrikeCooldown / (1 + speed), master.StrikeCooldown, 1e-5);
            Assert.AreEqual(plain.BreakBonusMult * (1 + cfg.HoeMasterCoinsPerLevel * 2), master.BreakBonusMult, 1e-9);
            var steward = StatResolver.Resolve(cfg, none, new Dictionary<string, int> { ["h_steward"] = 2 });
            Assert.AreEqual(plain.BreakBonusMult, steward.BreakBonusMult, 1e-9);
            Assert.That(steward.ApprenticeYield, Is.GreaterThan(plain.ApprenticeYield));
        }

        [Test]
        public void HeritageStarts_ActAsAFloorOnTheAlmanacLevel()
        {
            var cfg = TestConfig.Classic();
            var none = new Dictionary<string, int>();
            var start = StatResolver.Resolve(cfg, none, new Dictionary<string, int> { ["h_start_growth"] = 1, ["h_start_soft"] = 1 });
            var bought = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["growth"] = 1, ["soft_ground"] = 1 }, none);
            Assert.AreEqual(bought.GrowthMult, start.GrowthMult, 1e-6f);
            Assert.AreEqual(bought.HpMult, start.HpMult, 1e-6f);
            var both = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["growth"] = 3 }, new Dictionary<string, int> { ["h_start_growth"] = 1 });
            Assert.AreEqual(StatResolver.Resolve(cfg, new Dictionary<string, int> { ["growth"] = 3 }, none).GrowthMult, both.GrowthMult, 1e-6f, "the higher of the two, not their sum");
        }

        private static bool Differs(Stats a, Stats b)
        {
            foreach (var f in typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!Equals(f.GetValue(a), f.GetValue(b))) return true;
            return false;
        }
    }
}
