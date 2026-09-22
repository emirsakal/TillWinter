using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// The store-readiness balance pass (GDD v2.8): a winter's greenhouse is a share of the year, Long Summer counts
    /// past the year-length ceiling, Ring Master pays coins as well as speed, no bought radius level is wasted, and
    /// every node the advisor could suggest carries a weight.
    /// </summary>
    public class Stage4BalanceTests
    {
        private static FarmSim NewSim(System.Action<FarmConfig> tweak = null)
        {
            var cfg = TestConfig.Classic();
            cfg.CrowSpawnChance = 0f;
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, 1);
        }

        private static double EarnAYear(FarmSim sim)
        {
            sim.DebugForceRipeAll();
            for (int i = 0; i < 200 && sim.State.CoinsThisYear <= 0; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            Assert.That(sim.State.CoinsThisYear, Is.GreaterThan(0));
            return sim.State.CoinsThisYear;
        }

        [Test]
        public void Greenhouse_WinterIncome_IsCappedToAShareOfTheYear()
        {
            var sim = NewSim(c => { c.GreenhouseRatePerLevel = 100; c.GreenhouseWinterCapShare = 0.25; }); // an absurd rate: the cap is what stops it
            double take = EarnAYear(sim);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            for (int i = 0; i < 400; i++) sim.Tick(0.05f, null);
            Assert.AreEqual(0.25 * take, sim.State.Greenhouse.CoinsThisWinter, 1e-6);
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter, "once capped the greenhouse is done for the winter");
        }

        [Test]
        public void Greenhouse_AYearThatPaidNothing_EarnsNothingInWinter()
        {
            var sim = NewSim(c => c.GreenhouseRatePerLevel = 100);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            for (int i = 0; i < 100; i++) sim.Tick(0.05f, null);
            Assert.AreEqual(0, sim.State.Greenhouse.CoinsThisWinter);
        }

        [Test]
        public void LongSummer_CountsPastTheYearLengthCeiling()
        {
            var cfg = TestConfig.Classic();
            var maxed = new Dictionary<string, int> { ["year_length"] = AlmanacData.Get("year_length").MaxLevel };
            var none = new Dictionary<string, int>();
            var without = StatResolver.Resolve(cfg, maxed, none);
            Assert.AreEqual(cfg.MaxYearLength, without.YearLength, "the Almanac alone reaches the ceiling");
            var with = StatResolver.Resolve(cfg, maxed, new Dictionary<string, int> { ["h_long_summer"] = 1 });
            double bonus = HeritageData.Get("h_long_summer").ValuePerLevel;
            Assert.AreEqual(cfg.MaxYearLength + bonus, with.YearLength, 1e-4, "Long Summer lifts the ceiling by its own value");
            // The plain start-year-length node still stops at the ceiling: it is a head start, not a longer year.
            var start = StatResolver.Resolve(cfg, maxed, new Dictionary<string, int> { ["h_start_year_length"] = 4 });
            Assert.AreEqual(cfg.MaxYearLength, start.YearLength);
        }

        [Test]
        public void RingMaster_SpeedsTheRing_AndPaysItsHarvestsMore()
        {
            var cfg = TestConfig.Classic();
            var none = new Dictionary<string, int>();
            var plain = StatResolver.Resolve(cfg, none, none);
            var master = StatResolver.Resolve(cfg, none, new Dictionary<string, int> { ["h_ring_master"] = 2 });
            double speed = HeritageData.Get("h_ring_master").ValuePerLevel * 2;
            Assert.AreEqual(plain.RingGrowMult * (1 + speed), master.RingGrowMult, 1e-5);
            Assert.AreEqual(plain.RingBonusMult * (1 + cfg.RingMasterCoinsPerLevel * 2), master.RingBonusMult, 1e-9);
            // Its rival pays apprentices only, so the pair is a real fork: active hand or idle helpers.
            var steward = StatResolver.Resolve(cfg, none, new Dictionary<string, int> { ["h_steward"] = 2 });
            Assert.AreEqual(plain.RingBonusMult, steward.RingBonusMult, 1e-9);
            Assert.That(steward.ApprenticeYield, Is.GreaterThan(plain.ApprenticeYield));
        }

        [Test]
        public void RingRadiusCeiling_IsExactlyEveryLevelBought()
        {
            var cfg = new FarmConfig();
            double sum = cfg.BaseRingRadius
                + AlmanacData.Get("ring_radius").ValuePerLevel * AlmanacData.Get("ring_radius").MaxLevel
                + HeritageData.Get("h_start_radius").ValuePerLevel * HeritageData.Get("h_start_radius").MaxLevel;
            Assert.AreEqual(sum, cfg.MaxRingRadius, 1e-4, "a radius level that buys nothing is a lie in the tree");
            var s = StatResolver.Resolve(cfg,
                new Dictionary<string, int> { ["ring_radius"] = AlmanacData.Get("ring_radius").MaxLevel },
                new Dictionary<string, int> { ["h_start_radius"] = HeritageData.Get("h_start_radius").MaxLevel });
            Assert.AreEqual(cfg.MaxRingRadius, s.RingRadius, 1e-4);
        }

        [Test]
        public void Advisor_HasAWeightForEveryNodeInBothTables()
        {
            var weights = AlmanacAdvisor.CreateWeights();
            foreach (var n in AlmanacData.Nodes) Assert.IsTrue(weights.ContainsKey(n.Id), "no weight: " + n.Id);
            foreach (var n in HeritageData.Nodes) Assert.IsTrue(weights.ContainsKey(n.Id), "no weight: " + n.Id);
            foreach (var kv in weights)
                Assert.IsTrue(AlmanacData.Get(kv.Key) != null || HeritageData.Get(kv.Key) != null, "weight for a node that does not exist: " + kv.Key);
        }
    }
}
