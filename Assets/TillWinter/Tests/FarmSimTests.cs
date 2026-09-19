using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class FarmSimTests
    {
        private const float Dt = 0.01f;

        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = TestConfig.Classic();
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        /// <summary>Sets node levels directly (debug hook), leaving the sim in its current season.</summary>
        private static void Grant(FarmSim sim, string nodeId, int level = 1) => sim.DebugSetLevel(nodeId, level);

        /// <summary>Ring centred on (1,1); with the 0.7 starting radius it covers only that plot.</summary>
        private static readonly RingInput Centre = new RingInput(1, 1);

        private static float TimeUntil(FarmSim sim, RingInput? ring, Func<bool> done, float max = 60f)
        {
            float t = 0f;
            while (!done() && t < max)
            {
                sim.Tick(Dt, ring);
                t += Dt;
            }
            return t;
        }

        // ---------------------------------------------------------------- three-phase timings

        [Test]
        public void Carrot_UnderRing_Level0_WaterGrowHarvest_MatchCropTable()
        {
            var sim = NewSim();
            var plot = sim.State.GetPlot(1, 1);
            Assert.AreEqual(PlotState.Dry, plot.State);
            Assert.AreEqual(0.7f, sim.State.RingRadius, "starting radius covers one plot");
            sim.Tick(0f, Centre);
            Assert.IsTrue(sim.State.IsUnderRing(new GridPos(1, 1)));
            Assert.IsFalse(sim.State.IsUnderRing(new GridPos(0, 1)), "neighbour is outside a 0.7 ring");

            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Wet), Is.EqualTo(1.0f).Within(0.03f));
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Ripe), Is.EqualTo(1.5f).Within(0.03f));
            int harvests = 0;
            sim.Harvested += _ => harvests++;
            Assert.That(TimeUntil(sim, Centre, () => harvests > 0), Is.EqualTo(0.5f).Within(0.03f));
            Assert.AreEqual(1, sim.State.Coins);
            Assert.AreEqual(PlotState.Dry, plot.State, "replanted Dry");
            Assert.AreEqual(0f, plot.Progress);
        }

        [Test]
        public void Corn_UnderRing_Level0_Timings_MatchCropTable()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            Grant(sim, "unlock_corn");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            for (int i = 0; i < 18; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot"), "upgrade #" + i);
            Assert.AreEqual(2, sim.State.GetPlot(1, 1).Tier);
            sim.StartNextYear();
            var plot = sim.State.GetPlot(1, 1);

            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Wet), Is.EqualTo(2.0f).Within(0.03f));
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Ripe), Is.EqualTo(6.0f).Within(0.03f));
            double before = sim.State.Coins;
            Assert.That(TimeUntil(sim, Centre, () => sim.State.Coins > before), Is.EqualTo(0.7f).Within(0.03f));
            Assert.AreEqual(12, sim.State.Coins - before);
        }

        [Test]
        public void RipePlot_UnderRing_HarvestsAfterHarvestTime_Once_ReplantsDry()
        {
            var sim = NewSim();
            sim.DebugForceRipeAll();
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            Run(sim, 0.45f, Centre);
            Assert.AreEqual(0, events.Count);
            Run(sim, 0.1f, Centre);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(new GridPos(1, 1), events[0].Pos);
            Assert.AreEqual(HarvestSource.Ring, events[0].Source);
            Assert.AreEqual(-1, events[0].ApprenticeIndex);
            Assert.AreEqual(1, events[0].Coins);
            var plot = sim.State.GetPlot(1, 1);
            Assert.AreEqual(PlotState.Dry, plot.State);
            Assert.That(plot.Progress, Is.LessThan(0.15f));
            Assert.IsTrue(sim.State.GetPlot(0, 0).IsRipe, "plots outside the ring wait");
        }

        [Test]
        public void ZeroDtTap_DoesNotHarvestInstantly()
        {
            var sim = NewSim();
            sim.DebugForceRipeAll();
            sim.Tick(0f, Centre);
            Assert.AreEqual(0, sim.State.Coins);
            Assert.IsTrue(sim.State.GetPlot(1, 1).IsRipe);
        }

        // ---------------------------------------------------------------- passive systems

        [Test]
        public void NoPassiveProgress_AtLevel0()
        {
            var sim = NewSim();
            Run(sim, 10f, null);
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Dry, p.State);
                Assert.AreEqual(0f, p.Progress);
            }
        }

        [Test]
        public void Irrigation2_WatersDryPlot_InExpectedTime()
        {
            var sim = NewSim();
            Grant(sim, "irrigation", 2); // 0.3 × base water speed -> carrot wet in 1 / 0.3 = 3.33 s
            var plot = sim.State.GetPlot(0, 0);
            Assert.That(TimeUntil(sim, null, () => plot.State == PlotState.Wet), Is.EqualTo(3.333f).Within(0.03f));
            Run(sim, 10f, null);
            Assert.AreEqual(PlotState.Wet, plot.State, "without Sun a Wet plot never grows");
            Assert.AreEqual(0f, plot.Progress);
        }

        [Test]
        public void Sun_DoesNothingToDryPlots_ButGrowsWetOnes()
        {
            var sim = NewSim();
            Grant(sim, "sun", 1);
            Run(sim, 10f, null);
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(0, 0).State);
            Assert.AreEqual(0f, sim.State.GetPlot(0, 0).Progress);

            Grant(sim, "irrigation", 5); // 0.75 × -> wet in 1.33 s
            var plot = sim.State.GetPlot(2, 2);
            TimeUntil(sim, null, () => plot.State == PlotState.Wet);
            // Sun 1 = 0.15 × base grow speed: carrot grows in 1.5 / 0.15 = 10 s
            Assert.That(TimeUntil(sim, null, () => plot.State == PlotState.Ripe), Is.EqualTo(10f).Within(0.05f));
        }

        [Test]
        public void Soil_MultipliesRingAndSunGrowth_NotWatering()
        {
            // Ring: soil 2 -> 1.5× -> carrot grows in 1.0 s instead of 1.5 s.
            var ring = NewSim();
            Grant(ring, "soil_quality", 2);
            var plot = ring.State.GetPlot(1, 1);
            TimeUntil(ring, Centre, () => plot.State == PlotState.Wet);
            Assert.That(TimeUntil(ring, Centre, () => plot.State == PlotState.Ripe), Is.EqualTo(1.0f).Within(0.03f));

            // Sun 1 + soil 2: 0.15 × 1.5 = 0.225 -> 1.5 / 0.225 = 6.67 s.
            var sun = NewSim();
            Grant(sun, "soil_quality", 2);
            Grant(sun, "sun", 1);
            Grant(sun, "irrigation", 5);
            var p2 = sun.State.GetPlot(0, 0);
            TimeUntil(sun, null, () => p2.State == PlotState.Wet);
            Assert.That(TimeUntil(sun, null, () => p2.State == PlotState.Ripe), Is.EqualTo(6.667f).Within(0.05f));

            // Soil does not touch watering.
            var water = NewSim();
            Grant(water, "soil_quality", 6);
            var p3 = water.State.GetPlot(1, 1);
            Assert.That(TimeUntil(water, Centre, () => p3.State == PlotState.Wet), Is.EqualTo(1.0f).Within(0.03f));
        }

        [Test]
        public void RingRate_ReplacesPassiveRate_NoStacking()
        {
            var sim = NewSim();
            Grant(sim, "irrigation", 5);
            Grant(sim, "sun", 5);
            var plot = sim.State.GetPlot(1, 1);
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Wet), Is.EqualTo(1.0f).Within(0.03f));
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Ripe), Is.EqualTo(1.5f).Within(0.03f));
        }

        [Test]
        public void RingSpeedNodes_ScaleEachPhase()
        {
            var sim = NewSim();
            Grant(sim, "ring_water_speed", 5);   // 2.0× -> 0.5 s
            Grant(sim, "ring_grow_speed", 5);    // 2.0× -> 0.75 s
            Grant(sim, "ring_harvest_speed", 3); // 1.6× -> 0.3125 s
            var plot = sim.State.GetPlot(1, 1);
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Wet), Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(TimeUntil(sim, Centre, () => plot.State == PlotState.Ripe), Is.EqualTo(0.75f).Within(0.03f));
            Assert.That(TimeUntil(sim, Centre, () => sim.State.Coins > 0), Is.EqualTo(0.3125f).Within(0.03f));
        }

        [Test]
        public void RingBonusCoins_And_CropValue_MultiplyRingHarvests()
        {
            var sim = NewSim();
            Grant(sim, "ring_bonus_coins", 4); // 1.4×
            Grant(sim, "crop_value", 5);       // 1.5×
            sim.DebugForceRipeAll();
            Run(sim, 0.6f, Centre);
            Assert.That(sim.State.Coins, Is.EqualTo(1 * 1.4 * 1.5).Within(1e-9));
        }

        // ---------------------------------------------------------------- year

        [Test]
        public void Winter_StartsAt90Seconds()
        {
            var sim = NewSim();
            bool winter = false;
            sim.WinterStarted += () => winter = true;
            Run(sim, 89.9f, null);
            Assert.IsFalse(winter);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Assert.IsTrue(sim.State.FrostWarning);
            Run(sim, 0.2f, null);
            Assert.IsTrue(winter);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void YearLength_PlusFifteenPerLevel_CapsAt180()
        {
            var sim = NewSim();
            Grant(sim, "year_length", 1);
            Assert.AreEqual(105f, sim.State.YearLength);
            Grant(sim, "year_length", 6);
            Assert.AreEqual(180f, sim.State.YearLength);
            Run(sim, 179.9f, null);
            Assert.IsFalse(sim.State.IsWinter);
            Run(sim, 0.2f, null);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void FrostWarning_Last10Seconds_Plus5PerLevel()
        {
            var sim = NewSim();
            Run(sim, 79.9f, null);
            Assert.IsFalse(sim.State.FrostWarning);
            Run(sim, 0.2f, null);
            Assert.IsTrue(sim.State.FrostWarning);

            var longer = NewSim();
            Grant(longer, "frost_warning", 1);
            Assert.AreEqual(15f, longer.State.Stats.FrostWarningSeconds);
            bool frost = false;
            longer.FrostWarningStarted += () => frost = true;
            Run(longer, 74.9f, null);
            Assert.IsFalse(frost);
            Run(longer, 0.2f, null);
            Assert.IsTrue(frost);
        }

        [Test]
        public void Seasons_ChangeAtThirds()
        {
            var sim = NewSim();
            var seasons = new List<Season>();
            sim.SeasonChanged += seasons.Add;
            Run(sim, 29.9f, null);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Run(sim, 0.2f, null);
            Assert.AreEqual(Season.Summer, sim.State.Season);
            Run(sim, 30f, null);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Run(sim, 30f, null);
            CollectionAssert.AreEqual(new[] { Season.Summer, Season.Autumn, Season.Winter }, seasons);
        }

        [Test]
        public void Winter_ClearsAllPlotsToDry_KeepsCoinsAndTiers()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(100);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            sim.StartNextYear();
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);

            sim.DebugForceRipeAll();
            Run(sim, 0.6f, Centre); // one harvest
            double coins = sim.State.Coins;
            Assert.That(coins, Is.GreaterThan(75));
            sim.DebugSpawnCrow();

            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.State.IsWinter);
            Assert.AreEqual(coins, sim.State.Coins);
            Assert.AreEqual(0, sim.State.Crows.Count);
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Dry, p.State);
                Assert.AreEqual(0f, p.Progress);
                Assert.IsFalse(p.HasCrow);
            }
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier, "tiers kept");

            // Frozen: ring does nothing, timer does not move.
            Run(sim, 5f, Centre);
            Assert.AreEqual(coins, sim.State.Coins);
            Assert.IsNull(sim.State.Ring);
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(1, 1).State);

            sim.StartNextYear();
            Assert.AreEqual(3, sim.State.Year);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);
        }

        // ---------------------------------------------------------------- almanac

        [Test]
        public void Almanac_RejectsOutsideWinter()
        {
            var sim = NewSim();
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.CanBuy("ring_radius"));
            Assert.IsFalse(sim.TryBuy("ring_radius"));
            Assert.AreEqual(0, sim.State.GetLevel("ring_radius"));
            Assert.AreEqual(1e6, sim.State.Coins);
        }

        [Test]
        public void Almanac_EnforcesPrerequisites_AnyOf()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.IsAvailable("ring_water_speed"));
            Assert.IsFalse(sim.TryBuy("ring_water_speed"));
            Assert.IsTrue(sim.TryBuy("ring_radius"));
            Assert.IsTrue(sim.IsAvailable("ring_water_speed"));
            Assert.IsTrue(sim.TryBuy("ring_water_speed"));
            // ring_harvest_speed needs water OR grow (GDD §6: at least one prerequisite).
            Assert.IsTrue(sim.IsAvailable("ring_harvest_speed"));
            Assert.AreEqual(0, sim.State.GetLevel("ring_grow_speed"));
            Assert.IsTrue(sim.TryBuy("ring_harvest_speed"));
            Assert.IsFalse(sim.TryBuy("no_such_node"));
        }

        [Test]
        public void Almanac_CostCurve_And_Maxed_And_Unaffordable()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            var node = AlmanacData.Get("ring_radius");
            for (int n = 0; n < node.MaxLevel; n++)
            {
                double expected = Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, n));
                Assert.AreEqual(expected, sim.CostOf("ring_radius"), "cost at level " + n);
                sim.DebugAddCoins(expected - 1 - sim.State.Coins);
                Assert.IsFalse(sim.TryBuy("ring_radius"), "unaffordable by 1");
                sim.DebugAddCoins(1);
                Assert.IsTrue(sim.TryBuy("ring_radius"));
                Assert.AreEqual(0, sim.State.Coins);
            }
            Assert.IsTrue(sim.IsMaxed("ring_radius"));
            sim.DebugAddCoins(1e9);
            Assert.IsFalse(sim.TryBuy("ring_radius"));
            Assert.That(sim.State.RingRadius, Is.EqualTo(1.95f).Within(1e-5f));
        }

        [Test]
        public void RingRadius_PerLevel_AndCap()
        {
            var sim = NewSim();
            Assert.AreEqual(0.7f, sim.State.RingRadius);
            Grant(sim, "ring_radius", 1);
            Assert.That(sim.State.RingRadius, Is.EqualTo(0.95f).Within(1e-5f));
            var cfg = new FarmConfig { BaseRingRadius = 2.0f };
            var capped = new FarmSim(cfg, 1);
            capped.DebugSetLevel("ring_radius", 5);
            Assert.AreEqual(2.5f, capped.State.RingRadius);
        }

        [Test]
        public void Purchased_CarriesNodeIdAndLevel()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            var events = new List<PurchaseEvent>();
            sim.Purchased += events.Add;
            sim.TryBuy("irrigation");
            sim.TryBuy("irrigation");
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("irrigation", events[1].NodeId);
            Assert.AreEqual(2, events[1].Level);
            Assert.AreEqual(2, sim.State.AlmanacLevels["irrigation"]);
        }

        [Test]
        public void UpgradePlot_NeedsUnlockedTier_PicksLowestRowMajor_MaxesWhenAllAtCap()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsFalse(sim.IsAvailable("upgrade_plot"), "needs unlock_tomato first");
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.AreEqual(1, sim.State.Stats.MaxTierUnlocked);
            Assert.AreEqual(16, sim.GetMaxLevel("upgrade_plot"));

            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(1, sim.State.GetPlot(1, 0).Tier, "row-major tiebreak");
            for (int i = 2; i < 16; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot"), "#" + i);
            foreach (var p in sim.State.Plots) Assert.AreEqual(1, p.Tier);
            Assert.IsTrue(sim.IsMaxed("upgrade_plot"), "all plots at the highest unlocked tier");
            Assert.IsFalse(sim.TryBuy("upgrade_plot"));

            Assert.IsTrue(sim.TryBuy("unlock_corn"));
            Assert.IsFalse(sim.IsMaxed("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(2, sim.State.GetPlot(0, 0).Tier);
        }

        [Test]
        public void ExpandField_3To6_NewPlotsDryTier0()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            for (int size = 4; size <= 6; size++)
            {
                Assert.IsTrue(sim.TryBuy("expand_field"));
                Assert.AreEqual(size, sim.State.GridSize);
                Assert.AreEqual(size * size, sim.State.Plots.Count);
            }
            Assert.IsTrue(sim.IsMaxed("expand_field"));
            Assert.IsFalse(sim.TryBuy("expand_field"));
            for (int i = 0; i < 36; i++)
            {
                Assert.AreEqual(new GridPos(i % 6, i / 6), sim.State.Plots[i].Pos);
                Assert.AreEqual(PlotState.Dry, sim.State.Plots[i].State);
                Assert.AreEqual(0, sim.State.Plots[i].Tier);
            }
        }

        // ---------------------------------------------------------------- apprentices

        private static FarmSim SimWithApprentices(int count, Action<FarmSim> more = null)
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, "apprentice_count", count);
            more?.Invoke(sim);
            return sim;
        }

        [Test]
        public void Apprentice_TakesHarvestTime_PaysValueTimesYield()
        {
            var sim = SimWithApprentices(1);
            Assert.AreEqual(1, sim.State.Apprentices.Count);
            var a = sim.State.Apprentices[0];
            Assert.That(a.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(a.Y, Is.EqualTo(-1.2f).Within(1e-4f));

            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            sim.DebugForceRipeAll();
            // Nearest ripe plot is (1,0): 1.2 plots at 1.5 plots/s = 0.8 s, then 1.0 s harvest.
            float t = TimeUntil(sim, null, () => harvests.Count > 0);
            Assert.That(t, Is.EqualTo(1.8f).Within(0.05f));
            Assert.AreEqual(HarvestSource.Apprentice, harvests[0].Source);
            Assert.AreEqual(0, harvests[0].ApprenticeIndex);
            Assert.AreEqual(new GridPos(1, 0), harvests[0].Pos);
            Assert.AreEqual(0.5, harvests[0].Coins, "value 1 × yield 0.5 at level 0");
            Assert.AreEqual(0.5, sim.State.Coins);
        }

        [Test]
        public void Apprentice_HarvestTimeAndYieldNodes()
        {
            var sim = SimWithApprentices(1, s =>
            {
                Grant(s, "apprentice_harvest_time", 3); // 1.0 - 0.6 = 0.4
                Grant(s, "apprentice_yield", 4);        // 1.3
            });
            Assert.That(sim.State.Stats.ApprenticeHarvestTime, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.AreEqual(1.3, sim.State.Stats.ApprenticeYield);
            sim.DebugForceRipeAll();
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            float t = TimeUntil(sim, null, () => harvests.Count > 0);
            Assert.That(t, Is.EqualTo(0.8f + 0.4f).Within(0.05f));
            Assert.AreEqual(1.3, harvests[0].Coins);
        }

        [Test]
        public void Apprentice_SpeedNode_ChangesWalkSpeed()
        {
            float DistanceIn(int speedLevel, float seconds)
            {
                var sim = SimWithApprentices(1, s => Grant(s, "apprentice_speed", speedLevel));
                sim.DebugForceRipeAll();
                var a = sim.State.Apprentices[0];
                float x0 = a.X, y0 = a.Y;
                Run(sim, seconds, null);
                return (float)Math.Sqrt((a.X - x0) * (a.X - x0) + (a.Y - y0) * (a.Y - y0));
            }
            Assert.That(DistanceIn(0, 0.4f), Is.EqualTo(0.6f).Within(0.03f));  // 1.5 plots/s
            Assert.That(DistanceIn(4, 0.2f), Is.EqualTo(0.7f).Within(0.03f));  // 3.5 plots/s
        }

        [Test]
        public void TwoApprentices_NeverTargetTheSamePlot()
        {
            var sim = SimWithApprentices(2);
            Assert.AreEqual(2, sim.State.Apprentices.Count);
            var a0 = sim.State.Apprentices[0];
            var a1 = sim.State.Apprentices[1];
            Assert.AreNotEqual(a0.IdleX, a1.IdleX, "spread along the edge");

            sim.DebugForceRipeAll();
            int bothTargeting = 0;
            for (int i = 0; i < 600; i++)
            {
                sim.Tick(Dt, null);
                if (a0.HasTarget && a1.HasTarget)
                {
                    bothTargeting++;
                    Assert.AreNotEqual(a0.Target, a1.Target, "tick " + i);
                }
            }
            Assert.That(bothTargeting, Is.GreaterThan(100), "both were busy for a while");
            Assert.That(sim.State.Coins, Is.GreaterThan(0));
        }

        [Test]
        public void Apprentice_Retargets_WhenRingHarvestsItsTarget_AndIdlesAtEdge()
        {
            var sim = SimWithApprentices(1);
            sim.DebugForceRipeAll();
            Run(sim, 0.3f, null);
            var a = sim.State.Apprentices[0];
            Assert.IsTrue(a.HasTarget);
            Assert.IsTrue(a.IsWalking);
            var target = a.Target;
            Run(sim, 0.55f, new RingInput(target.X, target.Y)); // ring harvests it (0.5 s)
            Assert.IsFalse(sim.State.GetPlot(target).IsRipe);
            sim.Tick(Dt, null);
            Assert.IsTrue(a.HasTarget);
            Assert.AreNotEqual(target, a.Target);

            // Harvest with a huge ring, leave the rest to the apprentice, then it walks home.
            sim.DebugSetRingRadiusOverride(10f);
            Run(sim, 3f, Centre);
            Run(sim, 30f, null); // GDD §2.1 v1.4: the ring harvests one plot at a time; the apprentice takes what it left
            Assert.IsFalse(a.HasTarget);
            Assert.That(a.X, Is.EqualTo(a.IdleX).Within(1e-3f));
            Assert.That(a.Y, Is.EqualTo(a.IdleY).Within(1e-3f));
            Assert.IsFalse(a.IsWalking);
        }

        [Test]
        public void SixApprentices_AllWork()
        {
            var sim = SimWithApprentices(6);
            Assert.AreEqual(6, sim.State.Apprentices.Count);
            sim.DebugForceRipeAll();
            var byIndex = new HashSet<int>();
            sim.Harvested += e => byIndex.Add(e.ApprenticeIndex);
            Run(sim, 4f, null);
            Assert.AreEqual(6, byIndex.Count, "every apprentice harvested at least once");
        }

        // ---------------------------------------------------------------- crows

        [Test]
        public void Crow_EatsAfter4Seconds_PlotBackToDry()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.DebugSpawnCrow());
            var plot = sim.State.GetPlot(sim.State.Crows[0].Pos);
            var ate = new List<CrowEvent>();
            sim.CrowAte += ate.Add;
            Run(sim, 3.9f, null);
            Assert.IsTrue(plot.HasCrow);
            Run(sim, 0.2f, null);
            Assert.IsFalse(plot.HasCrow);
            Assert.AreEqual(PlotState.Dry, plot.State);
            Assert.AreEqual(1, ate.Count);
        }

        [Test]
        public void Crow_TapScare_PaysTwiceCropValue_CropStays()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            for (int i = 0; i < 9; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot")); // all tomato (value 4)
            sim.DebugAddCoins(-sim.State.Coins);
            sim.StartNextYear();
            sim.DebugSpawnCrow();
            var pos = sim.State.Crows[0].Pos;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;
            Run(sim, 2f, null);
            Assert.IsTrue(sim.TapAt(pos));
            Assert.AreEqual(1, scared.Count);
            Assert.AreEqual(8, scared[0].Coins);
            Assert.AreEqual(8, sim.State.Coins);
            Assert.IsTrue(sim.State.GetPlot(pos).IsRipe);
            Assert.IsFalse(sim.TapAt(pos));
            Assert.IsFalse(sim.TapAt(new GridPos(-1, 0)));
        }

        [Test]
        public void Crow_HarvestScare_PaysNothing()
        {
            var sim = NewSim();
            sim.DebugSpawnCrow();
            var pos = sim.State.Crows[0].Pos;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;
            Run(sim, 0.6f, new RingInput(pos.X, pos.Y));
            Assert.AreEqual(1, scared.Count);
            Assert.AreEqual(0, scared[0].Coins);
            Assert.AreEqual(1, sim.State.Coins, "just the harvest");
            Assert.AreEqual(0, sim.State.Crows.Count);
        }

        private static int CountSpawns(int scarecrowLevel, int seed)
        {
            var sim = NewSim(null, seed);
            Grant(sim, "scarecrow", scarecrowLevel);
            int landed = 0;
            sim.CrowLanded += _ => landed++;
            sim.CrowLanded += e => sim.TapAt(e.Pos); // scare immediately so the slot frees up
            for (int year = 0; year < 5; year++)
            {
                sim.DebugSkipToWinter();
                sim.StartNextYear(); // years 2..6
                sim.DebugForceRipeAll();
                Run(sim, 88f, null); // 22 spawn checks per year
            }
            return landed; // 110 checks per seed
        }

        [Test]
        public void Scarecrow_LevelsReduceSpawnChance_Level2StillSpawns()
        {
            int l0 = 0, l1 = 0, l2 = 0;
            for (int seed = 1; seed <= 5; seed++)
            {
                l0 += CountSpawns(0, seed);
                l1 += CountSpawns(1, seed);
                l2 += CountSpawns(2, seed);
            }
            // 550 checks each at 25% / 15% / 8%: expect ≈137 / 82 / 44.
            Assert.That(l0, Is.InRange(100, 180));
            Assert.That(l1, Is.InRange(55, 115));
            Assert.That(l2, Is.InRange(22, 70));
            Assert.That(l2, Is.GreaterThan(0), "scarecrow 2 never gives immunity");
            Assert.That(l1, Is.LessThan(l0));
            Assert.That(l2, Is.LessThan(l1));
        }

        [Test]
        public void Crows_OnlyFromYear2_MaxTwo_NeverUnderRing()
        {
            var y1 = NewSim();
            int landed = 0;
            y1.CrowLanded += _ => landed++;
            y1.DebugForceRipeAll();
            Run(y1, 60f, null);
            Assert.AreEqual(0, landed, "year 1");

            var y2 = NewSim(c => { c.CrowSpawnChanceByScarecrow = new[] { 1f, 1f, 1f }; c.CrowEatTime = 100f; });
            y2.DebugSkipToWinter();
            y2.StartNextYear();
            y2.DebugForceRipeAll();
            Run(y2, 12f, null);
            Assert.AreEqual(2, y2.State.Crows.Count);

            var ring = NewSim(c => c.CrowSpawnChanceByScarecrow = new[] { 1f, 1f, 1f });
            ring.DebugSkipToWinter();
            ring.StartNextYear();
            ring.DebugSetRingRadiusOverride(10f);
            int landedRing = 0;
            ring.CrowLanded += _ => landedRing++;
            for (int i = 0; i < 100; i++)
            {
                ring.DebugForceRipeAll();
                ring.Tick(0.1f, Centre); // everything is under the ring, so nothing is a crow target
            }
            Assert.AreEqual(0, landedRing);
        }

        // ---------------------------------------------------------------- misc

        [Test]
        public void Determinism_SameSeedSameInputs_SameResult()
        {
            var a = NewSim(null, 42);
            var b = NewSim(null, 42);
            foreach (var s in new[] { a, b })
            {
                Grant(s, "irrigation", 3);
                Grant(s, "sun", 3);
                Grant(s, "apprentice_count", 2);
                s.DebugSkipToWinter();
                s.StartNextYear();
                Run(s, 40f, null);
                Run(s, 5f, Centre);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins);
            Assert.AreEqual(a.State.Crows.Count, b.State.Crows.Count);
            for (int i = 0; i < a.State.Plots.Count; i++)
            {
                Assert.AreEqual(a.State.Plots[i].State, b.State.Plots[i].State);
                Assert.AreEqual(a.State.Plots[i].Progress, b.State.Plots[i].Progress);
            }
        }

        [Test]
        public void CropTable_MatchesGdd()
        {
            var c = new FarmConfig();
            Assert.AreEqual(6, c.Crops.Length);
            Assert.AreEqual(5, c.MaxTier);
            var expected = new[]
            {
                ("crop.carrot", 1.0f, 1.5f, 0.5f, 2.2, Season.Spring), // S9 balance (GDD §2.3 v1.4), M.2 spring bonus (v1.7)
                ("crop.tomato", 1.5f, 3.5f, 0.5f, 4.0, Season.Summer),
                ("crop.corn", 2.0f, 6.0f, 0.7f, 12.0, Season.Summer),
                ("crop.pumpkin", 3.0f, 10f, 1.0f, 35.0, Season.Autumn),
                ("crop.grapes", 4.0f, 15f, 1.0f, 100.0, Season.Autumn),
                ("crop.golden_wheat", 5.0f, 22f, 1.2f, 300.0, Season.Spring),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Item1, c.Crops[i].Key);
                Assert.AreEqual(expected[i].Item2, c.Crops[i].Water);
                Assert.AreEqual(expected[i].Item3, c.Crops[i].Grow);
                Assert.AreEqual(expected[i].Item4, c.Crops[i].Harvest);
                Assert.AreEqual(expected[i].Item5, c.Crops[i].Value);
                Assert.AreEqual(expected[i].Item6, c.Crops[i].Likes, c.Crops[i].Key);
            }
        }
    }
}
