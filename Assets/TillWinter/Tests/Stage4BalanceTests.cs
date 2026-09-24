using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;
using static TillWinter.Tests.CoreLoopTests;

namespace TillWinter.Tests
{
    /// <summary>
    /// The store-readiness balance pass (GDD v2.8, re-themed v3): a winter's greenhouse is a share of the year, Long
    /// Summer counts past the year-length ceiling, and every node the advisor could suggest carries a weight. (The Hoe
    /// Master's speed and coins are pinned in <see cref="NodeMapTests"/>.)
    /// </summary>
    public class Stage4BalanceTests
    {
        private static FarmSim Sim(System.Action<FarmConfig> tweak = null) =>
            NewSim(c => { c.CrowSpawnChance = 0f; tweak?.Invoke(c); });

        private static double EarnAYear(FarmSim sim)
        {
            sim.DebugForceRipeAll();
            sim.ReapAll();
            Assert.That(sim.State.CoinsThisYear, Is.GreaterThan(0));
            return sim.State.CoinsThisYear;
        }

        [Test]
        public void Greenhouse_WinterIncome_IsCappedToAShareOfTheYear()
        {
            var sim = Sim(c => { c.GreenhouseRatePerLevel = 100; c.GreenhouseWinterCapShare = 0.25; }); // an absurd rate: the cap is what stops it
            double take = EarnAYear(sim);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            Run(sim, 20f);
            Assert.AreEqual(0.25 * take, sim.State.Greenhouse.CoinsThisWinter, 1e-6);
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter, "once capped the greenhouse is done for the winter");
        }

        [Test]
        public void Greenhouse_AYearThatPaidNothing_EarnsNothingInWinter()
        {
            var sim = Sim(c => c.GreenhouseRatePerLevel = 100);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            Run(sim, 5f);
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
