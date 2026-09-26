using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class HeritageTests
    {
        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1) => CoreLoopTests.NewSim(tweak, seed);

        private static void Run(FarmSim sim, float seconds, float dt = 0.01f) => CoreLoopTests.Run(sim, seconds, dt);

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
            Assert.AreEqual(25, sim.ReapAll(), "one swipe over the 5x5 field");
            sim.DebugSpawnCrow();
            Assert.IsTrue(sim.TapAt(sim.State.Crows[0].Pos));
            int crowsScared = sim.State.Generation.CrowsScared;
            int harvests = sim.State.Generation.Harvests;
            double lifetimeTotal = sim.State.Generation.LifetimeCoinsTotal;
            sim.DebugAddLifetimeCoins(5000 - sim.State.Generation.LifetimeCoinsThisGeneration + 2000); // 7000 -> 11 seeds
            sim.DebugSetLevel("h_strike_speed", 2);
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
                Assert.AreEqual(PlotState.Hard, p.State, "the field returns to the surface");
                Assert.AreEqual(0, p.Layer);
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
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
            Assert.AreEqual(2, s.GetLevel("h_strike_speed"), "heritage tree kept");
            Assert.AreEqual(crowsScared, g.CrowsScared);
            Assert.AreEqual(harvests, g.Harvests);
            Assert.That(g.LifetimeCoinsTotal, Is.GreaterThan(lifetimeTotal), "lifetime total keeps growing across generations");
            // Phase and event:
            Assert.AreEqual(Phase.Heritage, s.Phase);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(11, events[0].SeedsEarned);
            Assert.AreEqual(2, events[0].Generation);
            // Nothing ticks in the Heritage phase.
            Run(sim, 5f);
            Assert.AreEqual(0, s.Coins);
            Assert.AreEqual(0f, s.YearTime);
        }

        [Test]
        public void HeritagePurchases_AllowedInWinterAndHeritage_RejectedDuringYear_AlmanacRejectedInHeritage()
        {
            var sim = NewSim();
            sim.DebugAddSeeds(100);
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.CanBuy("h_start_damage"), "year");
            Assert.IsFalse(sim.TryBuy("h_start_damage"));

            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.TryBuy("h_start_damage"), "winter");
            Assert.AreEqual(1, sim.State.HeritageLevels["h_start_damage"]);
            Assert.AreEqual(97, sim.State.Seeds, "cost 3 seeds");

            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            Assert.AreEqual(Phase.Heritage, sim.State.Phase);
            Assert.IsTrue(sim.TryBuy("h_start_damage"), "heritage phase");
            Assert.IsFalse(sim.CanBuy("hoe_damage"), "almanac closed in heritage phase (and no coins anyway)");
            sim.DebugAddCoins(1000);
            Assert.IsFalse(sim.TryBuy("hoe_damage"));
            Assert.IsFalse(sim.TryBuy("h_break_coins"), "prerequisite h_strike_speed missing");
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
            Assert.IsTrue(sim.TryBuy("h_start_damage"));
            Assert.IsTrue(sim.TryBuy("h_start_damage"));
            Assert.IsTrue(sim.TryBuy("h_start_growth"));
            Assert.IsTrue(sim.TryBuy("h_start_soft"));
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
            Assert.AreEqual(3 + 2, s.Stats.StrikeDamage, 1e-9, "two damage levels");
            Assert.That(s.Stats.GrowthMult, Is.EqualTo(1.15f).Within(1e-5f), "start with growth 1");
            Assert.That(s.Stats.HpMult, Is.EqualTo(0.94f).Within(1e-5f), "start with soft ground 1");
            foreach (var p in s.Plots) Assert.AreEqual(sim.Config.BaseHp * 0.94, p.MaxHp, 1e-6, "softer ground from the first layer");
            Assert.AreEqual(1, s.Stats.MaxTierUnlocked, "tomato unlocked");
            Assert.AreEqual(100f, s.YearLength, "90 + 10");

            // The hoe hits harder from the first swing: 5 off a 9.4 HP layer.
            var plot = s.GetPlot(0, 0);
            CoreLoopTests.WaitForBeat(sim, false);
            Assert.IsTrue(sim.Strike(plot.Pos));
            Assert.AreEqual(plot.MaxHp - 5, plot.Hp, 1e-9);

            // The passive chain works from second 0: broken ground grows carrots the free apprentice picks.
            sim.DebugBreakAll();
            Run(sim, 40f);
            Assert.That(s.Generation.HarvestsApprentice, Is.GreaterThan(0));
            Assert.That(s.Coins, Is.GreaterThan(0));
            Assert.AreEqual(2, s.Generation.Generation);
        }

        [Test]
        public void HeritageModifiers_StackBeforeAlmanac()
        {
            var cfg = TestConfig.Classic();
            var s = StatResolver.Resolve(cfg,
                new Dictionary<string, int> { ["strike_speed"] = 2, ["soil_quality"] = 4, ["growth"] = 3, ["break_bonus"] = 2, ["apprentice_yield"] = 2, ["expand_field"] = 1 },
                new Dictionary<string, int> { ["h_strike_speed"] = 5, ["h_global_growth"] = 4, ["h_start_growth"] = 1, ["h_break_coins"] = 4, ["h_apprentice_yield"] = 4, ["h_start_field"] = 1, ["h_almanac_discount"] = 4 });
            // Almanac takes 2 x 0.04 off the 0.35 s cooldown, then Heritage divides by 1 + 5 x 0.05.
            Assert.That(s.StrikeCooldown, Is.EqualTo((0.35f - 0.08f) / 1.25f).Within(1e-4f));
            // growth 3 beats the Heritage floor of 1 (1 + 3 x 0.15), then global growth x1.2 on top.
            Assert.That(s.GrowthMult, Is.EqualTo(1.45f * 1.2f).Within(1e-4f), "almanac 3 beats heritage floor 1");
            Assert.That(s.SoilMultiplier, Is.EqualTo(2.0f).Within(1e-4f), "soil quality alone");
            Assert.That(s.BreakBonusMult, Is.EqualTo(1.2 * 1.2).Within(1e-9));
            Assert.That(s.ApprenticeYield, Is.EqualTo(1.0 * 1.2).Within(1e-9));
            Assert.AreEqual(4, s.StartGridSize);
            Assert.AreEqual(5, s.TargetGridSize, "4 + 1 expansion");
            Assert.That(s.AlmanacCostMult, Is.EqualTo(0.8).Within(1e-9));
        }

        [Test]
        public void AlmanacDiscount_ChangesCostOf_NotHeritageCosts()
        {
            var sim = NewSim();
            Assert.AreEqual(120, sim.CostOf("hoe_damage"));
            sim.DebugSetLevel("h_almanac_discount", 2); // -10%
            Assert.AreEqual(108, sim.CostOf("hoe_damage"));
            Assert.AreEqual(432, sim.CostOf("stamina_regen"), "480 after discount");
            Assert.AreEqual(3, sim.CostOf("h_start_damage"), "heritage costs untouched");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(108);
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            Assert.AreEqual(0, sim.State.Coins);
        }

        [Test]
        public void FeatureHeritageNodes_ArePurchasable_AndStoredAsFlags()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddSeeds(500);
            foreach (var id in new[] { "h_start_growth", "h_start_soft", "h_global_growth", "h_unlock_rain_cloud", "h_start_field", "h_start_tomato", "h_golden_crop", "h_free_apprentice", "h_apprentice_yield", "h_scarecrow_immunity", "h_start_year_length", "h_greenhouse_x2" })
                Assert.IsTrue(sim.TryBuy(id), id);
            var st = sim.State.Stats;
            Assert.IsTrue(st.RainCloudUnlocked);
            Assert.That(st.GoldenCropChance, Is.EqualTo(0.01).Within(1e-9));
            Assert.IsTrue(st.ScarecrowImmunity);
            Assert.AreEqual(1, st.GreenhouseX2Level);
            Assert.That(st.GrowthMult, Is.EqualTo(1.15f * 1.05f).Within(1e-5f), "start growth 1 x global growth 1");
            Assert.That(st.HpMult, Is.EqualTo(0.94f).Within(1e-5f));
        }

        [Test]
        public void Tables_ValidTogether_IdsUnique_AndHeritageMatchesGdd()
        {
            Assert.IsEmpty(SkillTree.ValidateAll(AlmanacData.Nodes, HeritageData.Nodes));
            var max = new Dictionary<string, int>
            {
                ["h_start_damage"] = 3, ["h_strike_speed"] = 5, ["h_break_coins"] = 4,
                ["h_start_growth"] = 1, ["h_start_soft"] = 1, ["h_global_growth"] = 5, ["h_unlock_rain_cloud"] = 1,
                ["h_start_field"] = 1, ["h_start_tomato"] = 1, ["h_golden_crop"] = 5,
                ["h_free_apprentice"] = 1, ["h_apprentice_yield"] = 4, ["h_scarecrow_immunity"] = 1,
                ["h_start_year_length"] = 4, ["h_greenhouse_x2"] = 2, ["h_almanac_discount"] = 4,
                ["h_hoe_master"] = 2, ["h_steward"] = 2, ["h_long_summer"] = 1, ["h_rich_soil"] = 1,
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
            var pos = new GridPos(1, 1);
            Assert.IsTrue(sim.DebugForceRipe(pos));
            Assert.IsTrue(sim.ReapOne(pos));
            Assert.AreEqual(1, sim.State.Generation.Harvests);
            Assert.AreEqual(1, sim.State.Generation.LifetimeCoinsThisGeneration, "one carrot, one coin");
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Assert.AreEqual(1, sim.State.Generation.YearsThisGeneration);
            Assert.AreEqual(2, sim.State.Year);
        }
    
        [Test]
        public void HeritageStartLevels_AreGrantedAsAlmanacLevels_SoTheyAreNeverSoldTwice()
        {
            // GDD §7 (v3.5): "start with growth 1" is the Almanac level itself, owned for free, not a hidden floor the
            // player can pay to reach a second time.
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            sim.DebugAddSeeds(200);
            Assert.IsTrue(sim.Retire());
            Assert.IsTrue(sim.TryBuy("h_start_growth"));
            Assert.IsTrue(sim.TryBuy("h_start_soft"));
            sim.StartNewGeneration();
            var s = sim.State;
            Assert.AreEqual(1, s.GetLevel("growth"), "growth level 1 owned at the start");
            Assert.AreEqual(1, s.GetLevel("soft_ground"), "soft ground level 1 owned at the start");
            Assert.AreEqual(0, s.Generation.AlmanacSpent, 1e-9, "granted levels cost nothing");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(100000);
            double coins = s.Coins;
            float growth = s.Stats.GrowthMult;
            Assert.IsTrue(sim.TryBuy("growth"), "the next purchase is level 2");
            Assert.AreEqual(2, s.GetLevel("growth"));
            Assert.That(s.Stats.GrowthMult, Is.GreaterThan(growth), "and it changes the number");
            Assert.That(coins - s.Coins, Is.GreaterThan(0), "at level 2's price");
            // A respec keeps the family's levels.
            Assert.IsTrue(sim.RespecAlmanac());
            Assert.AreEqual(1, s.GetLevel("growth"));
            Assert.AreEqual(1, s.GetLevel("soft_ground"));
        }

        [Test]
        public void YearLength_StopsAtTheCeiling_SoNoLevelBuysNothing()
        {
            // With Heritage's longer years the Almanac's last year_length levels would sit past the ceiling; they are
            // shown as done instead of sold.
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            sim.DebugAddSeeds(200);
            Assert.IsTrue(sim.Retire());
            for (int i = 0; i < 4; i++) Assert.IsTrue(sim.TryBuy("h_start_year_length"));
            sim.StartNewGeneration();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            int bought = 0;
            while (sim.CanBuy("year_length")) { Assert.IsTrue(sim.TryBuy("year_length")); bought++; Assert.That(bought, Is.LessThan(10)); }
            var s = sim.State;
            Assert.AreEqual(sim.Config.MaxYearLength, s.Stats.YearLength, 1e-4, "the year is at its ceiling");
            Assert.IsTrue(sim.IsMaxed("year_length"), "and the node reads as done");
            Assert.AreEqual(s.GetLevel("year_length"), sim.GetMaxLevel("year_length"), "with no dead levels left to show");
            Assert.That(bought, Is.LessThan(6), "fewer levels than the table's six, because Heritage covered the rest");
        }
    }
}
