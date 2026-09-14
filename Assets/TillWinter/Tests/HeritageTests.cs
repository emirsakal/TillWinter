using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class HeritageTests
    {
        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = new FarmConfig();
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = 0.01f)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        [Test]
        public void SeedFormula_AndThreshold()
        {
            var sim = NewSim();
            Assert.AreEqual(0, sim.SeedsIfRetiredNow);
            Assert.IsFalse(sim.CanRetire);
            sim.DebugAddLifetimeCoins(4999);
            Assert.IsFalse(sim.CanRetire);
            sim.DebugAddLifetimeCoins(1);
            Assert.IsTrue(sim.CanRetire, "threshold 5000");
            Assert.AreEqual(10, sim.SeedsIfRetiredNow, "sqrt(5000/50) = 10");
            sim.DebugAddLifetimeCoins(15000); // 20000 = 4x threshold
            Assert.AreEqual(20, sim.SeedsIfRetiredNow, "sqrt(20000/50) = 20");
            Assert.AreEqual(0, sim.SeedsFor(0));
            Assert.AreEqual(1, sim.SeedsFor(50));
            Assert.AreEqual(1, sim.SeedsFor(199));
        }

        [Test]
        public void Retire_RejectedOutsideWinter_AndBelowThreshold()
        {
            var sim = NewSim();
            sim.DebugAddLifetimeCoins(1e6);
            Assert.IsFalse(sim.Retire(), "during the year");
            Assert.AreEqual(Phase.Year, sim.State.Phase);

            var poor = NewSim();
            poor.DebugSkipToWinter();
            Assert.AreEqual(Phase.Winter, poor.State.Phase);
            Assert.IsFalse(poor.Retire(), "below threshold");
            Assert.AreEqual(Phase.Winter, poor.State.Phase);
            Assert.AreEqual(1, poor.State.Generation.Generation);
        }

        [Test]
        public void Retire_ResetsAndKeepsExactlyTheListedThings()
        {
            var sim = NewSim();
            // Build up a generation worth of state.
            sim.DebugSetLevel("expand_field", 2);
            sim.DebugSetLevel("unlock_tomato", 1);
            sim.DebugSetLevel("apprentice_count", 2);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            sim.StartNextYear();
            sim.StartNextYear(); // (no-op: not winter) keeps year at 2
            sim.DebugForceRipeAll();
            Run(sim, 2f, new RingInput(1, 1));
            sim.DebugSpawnCrow();
            Assert.IsTrue(sim.TapAt(sim.State.Crows[0].Pos));
            int crowsScared = sim.State.Generation.CrowsScared;
            int harvests = sim.State.Generation.Harvests;
            double lifetimeTotal = sim.State.Generation.LifetimeCoinsTotal;
            sim.DebugAddLifetimeCoins(5000 - sim.State.Generation.LifetimeCoinsThisGeneration + 2000); // 7000 -> 11 seeds
            sim.DebugSetLevel("h_ring_speeds", 2);
            sim.DebugAddSeeds(3);
            sim.DebugSkipToWinter();

            var events = new List<RetireEvent>();
            sim.Retired += events.Add;
            Assert.AreEqual(11, sim.SeedsIfRetiredNow);
            Assert.IsTrue(sim.Retire());

            var s = sim.State;
            var g = s.Generation;
            // Reset:
            Assert.AreEqual(0, s.Coins, "coins");
            Assert.AreEqual(3, s.GridSize, "field back to the Heritage start size");
            foreach (var p in s.Plots)
            {
                Assert.AreEqual(0, p.Tier);
                Assert.AreEqual(PlotState.Dry, p.State);
                Assert.AreEqual(0f, p.Progress);
                Assert.IsFalse(p.HasCrow);
            }
            Assert.AreEqual(0, s.AlmanacLevels.Count, "almanac cleared");
            Assert.AreEqual(0, s.Apprentices.Count, "helpers removed");
            Assert.AreEqual(0, s.Crows.Count, "crows removed");
            Assert.AreEqual(1, s.Year, "year counter");
            Assert.AreEqual(0f, s.YearTime);
            Assert.AreEqual(0, g.LifetimeCoinsThisGeneration);
            Assert.AreEqual(0, g.YearsThisGeneration);
            // Kept:
            Assert.AreEqual(2, g.Generation);
            Assert.AreEqual(14, g.SeedsBanked, "3 + 11");
            Assert.AreEqual(11, g.SeedsEarnedTotal);
            Assert.AreEqual(2, s.GetLevel("h_ring_speeds"), "heritage tree kept");
            Assert.AreEqual(crowsScared, g.CrowsScared);
            Assert.AreEqual(harvests, g.Harvests);
            Assert.That(g.LifetimeCoinsTotal, Is.GreaterThan(lifetimeTotal), "lifetime total keeps growing across generations");
            // Phase and event:
            Assert.AreEqual(Phase.Heritage, s.Phase);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(11, events[0].SeedsEarned);
            Assert.AreEqual(2, events[0].Generation);
            // Nothing ticks in the Heritage phase.
            Run(sim, 5f, new RingInput(1, 1));
            Assert.AreEqual(0, s.Coins);
            Assert.AreEqual(0f, s.YearTime);
        }

        [Test]
        public void HeritagePurchases_AllowedInWinterAndHeritage_RejectedDuringYear_AlmanacRejectedInHeritage()
        {
            var sim = NewSim();
            sim.DebugAddSeeds(100);
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.CanBuy("h_start_radius"), "year");
            Assert.IsFalse(sim.TryBuy("h_start_radius"));

            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.TryBuy("h_start_radius"), "winter");
            Assert.AreEqual(1, sim.State.HeritageLevels["h_start_radius"]);
            Assert.AreEqual(97, sim.State.Seeds, "cost 3 seeds");

            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            Assert.AreEqual(Phase.Heritage, sim.State.Phase);
            Assert.IsTrue(sim.TryBuy("h_start_radius"), "heritage phase");
            Assert.IsFalse(sim.CanBuy("ring_radius"), "almanac closed in heritage phase (and no coins anyway)");
            sim.DebugAddCoins(1000);
            Assert.IsFalse(sim.TryBuy("ring_radius"));
            Assert.IsFalse(sim.TryBuy("h_ring_coins"), "prerequisite h_ring_speeds missing");
        }

        [Test]
        public void HeritageStartingBonuses_ApplyOnStartNewGeneration()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            sim.DebugAddSeeds(200);
            Assert.IsTrue(sim.Retire());
            Assert.IsTrue(sim.TryBuy("h_start_field"));
            Assert.IsTrue(sim.TryBuy("h_free_apprentice"));
            Assert.IsTrue(sim.TryBuy("h_start_radius"));
            Assert.IsTrue(sim.TryBuy("h_start_radius"));
            Assert.IsTrue(sim.TryBuy("h_start_irrigation"));
            Assert.IsTrue(sim.TryBuy("h_start_sun"));
            Assert.IsTrue(sim.TryBuy("h_start_tomato"));
            Assert.IsTrue(sim.TryBuy("h_start_year_length"));

            bool started = false;
            sim.GenerationStarted += () => started = true;
            sim.StartNewGeneration();
            Assert.IsTrue(started);
            var s = sim.State;
            Assert.AreEqual(Phase.Year, s.Phase);
            Assert.AreEqual(Season.Spring, s.Season);
            Assert.AreEqual(1, s.Year);
            Assert.AreEqual(4, s.GridSize, "starting field 4x4");
            Assert.AreEqual(16, s.Plots.Count);
            Assert.AreEqual(1, s.Apprentices.Count, "free apprentice");
            Assert.That(s.RingRadius, Is.EqualTo(0.7f + 0.5f).Within(1e-5f), "two radius levels");
            Assert.That(s.Stats.IrrigationFactor, Is.EqualTo(0.15f).Within(1e-5f), "start with irrigation 1");
            Assert.That(s.Stats.SunFactor, Is.EqualTo(0.15f).Within(1e-5f), "start with sun 1");
            Assert.AreEqual(1, s.Stats.MaxTierUnlocked, "tomato unlocked");
            Assert.AreEqual(100f, s.YearLength, "90 + 10");

            // Passive chain works from second 0: carrots water, grow and get harvested by the free apprentice.
            Run(sim, 40f, null);
            Assert.That(s.Coins, Is.GreaterThan(0));
            Assert.AreEqual(2, s.Generation.Generation);
        }

        [Test]
        public void HeritageModifiers_StackBeforeAlmanac()
        {
            var cfg = new FarmConfig();
            var s = StatResolver.Resolve(cfg,
                new Dictionary<string, int> { ["ring_water_speed"] = 5, ["soil_quality"] = 4, ["irrigation"] = 3, ["ring_bonus_coins"] = 2, ["apprentice_yield"] = 2, ["expand_field"] = 1 },
                new Dictionary<string, int> { ["h_ring_speeds"] = 5, ["h_global_growth"] = 4, ["h_start_irrigation"] = 1, ["h_ring_coins"] = 4, ["h_apprentice_yield"] = 4, ["h_start_field"] = 1, ["h_almanac_discount"] = 4 });
            Assert.That(s.RingWaterMult, Is.EqualTo(2.0f * 1.5f).Within(1e-4f));
            Assert.That(s.RingGrowMult, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(s.SoilMultiplier, Is.EqualTo(2.0f * 1.2f).Within(1e-4f));
            Assert.That(s.IrrigationFactor, Is.EqualTo(0.45f).Within(1e-4f), "almanac 3 beats heritage floor 1");
            Assert.That(s.RingBonusMult, Is.EqualTo(1.2 * 1.2).Within(1e-9));
            Assert.That(s.ApprenticeYield, Is.EqualTo(1.0 * 1.2).Within(1e-9));
            Assert.AreEqual(4, s.StartGridSize);
            Assert.AreEqual(5, s.TargetGridSize, "4 + 1 expansion");
            Assert.That(s.AlmanacCostMult, Is.EqualTo(0.8).Within(1e-9));
        }

        [Test]
        public void AlmanacDiscount_ChangesCostOf_NotHeritageCosts()
        {
            var sim = NewSim();
            Assert.AreEqual(30, sim.CostOf("ring_radius"));
            sim.DebugSetLevel("h_almanac_discount", 2); // -10%
            Assert.AreEqual(27, sim.CostOf("ring_radius"));
            Assert.AreEqual(120, sim.CostOf("ring_water_speed") + 12, "108 after discount");
            Assert.AreEqual(3, sim.CostOf("h_start_radius"), "heritage costs untouched");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(27);
            Assert.IsTrue(sim.TryBuy("ring_radius"));
            Assert.AreEqual(0, sim.State.Coins);
        }

        [Test]
        public void NotImplementedHeritageNodes_ArePurchasable_AndStoredAsFlags()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddSeeds(500);
            foreach (var id in new[] { "h_start_irrigation", "h_start_sun", "h_global_growth", "h_unlock_rain_cloud", "h_start_field", "h_start_tomato", "h_golden_crop", "h_free_apprentice", "h_apprentice_yield", "h_scarecrow_immunity", "h_start_year_length", "h_greenhouse_x2" })
                Assert.IsTrue(sim.TryBuy(id), id);
            var st = sim.State.Stats;
            Assert.IsTrue(st.RainCloudUnlocked);
            Assert.That(st.GoldenCropChance, Is.EqualTo(0.01).Within(1e-9));
            Assert.IsTrue(st.ScarecrowImmunity);
            Assert.AreEqual(1, st.GreenhouseX2Level);
            foreach (var id in new[] { "h_unlock_rain_cloud", "h_golden_crop", "h_scarecrow_immunity", "h_greenhouse_x2" })
                Assert.IsFalse(sim.GetNode(id).IsImplemented, id + " flagged NotImplemented");
            foreach (var id in new[] { "h_start_radius", "h_ring_speeds", "h_almanac_discount", "h_free_apprentice" })
                Assert.IsTrue(HeritageData.Get(id).IsImplemented, id);
        }

        [Test]
        public void Tables_ValidTogether_IdsUnique_AndHeritageMatchesGdd()
        {
            Assert.IsEmpty(SkillTree.ValidateAll(AlmanacData.Nodes, HeritageData.Nodes));
            var max = new Dictionary<string, int>
            {
                ["h_start_radius"] = 3, ["h_ring_speeds"] = 5, ["h_ring_coins"] = 4,
                ["h_start_irrigation"] = 1, ["h_start_sun"] = 1, ["h_global_growth"] = 5, ["h_unlock_rain_cloud"] = 1,
                ["h_start_field"] = 1, ["h_start_tomato"] = 1, ["h_golden_crop"] = 5,
                ["h_free_apprentice"] = 1, ["h_apprentice_yield"] = 4, ["h_scarecrow_immunity"] = 1,
                ["h_start_year_length"] = 4, ["h_greenhouse_x2"] = 2, ["h_almanac_discount"] = 4,
            };
            Assert.AreEqual(max.Count, HeritageData.Nodes.Length);
            foreach (var kv in max) Assert.AreEqual(kv.Value, HeritageData.Get(kv.Key).MaxLevel, kv.Key);
            var dup = SkillTree.ValidateAll(new[] { AlmanacData.Nodes[0] }, new[] { AlmanacData.Nodes[0] });
            Assert.IsTrue(dup.Exists(e => e.Contains("both trees")));
        }

        [Test]
        public void GenerationStats_CountHarvestsAndYears()
        {
            var sim = NewSim();
            sim.DebugForceRipeAll();
            Run(sim, 0.6f, new RingInput(1, 1));
            Assert.AreEqual(1, sim.State.Generation.Harvests);
            Assert.AreEqual(1, sim.State.Generation.LifetimeCoinsThisGeneration);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Assert.AreEqual(1, sim.State.Generation.YearsThisGeneration);
            Assert.AreEqual(2, sim.State.Year);
        }
    }
}
