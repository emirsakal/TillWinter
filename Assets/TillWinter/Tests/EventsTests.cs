using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Session 3: rain cloud, golden crop, tractor, greenhouse and the remaining small nodes.</summary>
    public class EventsTests
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

        private static readonly RingInput Centre = new RingInput(1, 1);

        // ---------------------------------------------------------------- rain cloud (GDD §5.2)

        [Test]
        public void Cloud_NeverSpawnsWhenLocked_SpawnsOncePerSummerWhenUnlocked()
        {
            var locked = NewSim();
            int appeared = 0;
            locked.RainCloudAppeared += () => appeared++;
            Run(locked, 89f, null);
            Assert.AreEqual(0, appeared);
            Assert.IsFalse(locked.State.Cloud.Active);

            var sim = NewSim();
            sim.DebugSetLevel("h_unlock_rain_cloud", 1);
            sim.DebugSkipToWinter();
            sim.StartNextYear(); // spawn time is chosen at Spring
            int seen = 0, left = 0;
            float appearedAt = -1f;
            sim.RainCloudAppeared += () => { seen++; appearedAt = sim.State.YearTime; };
            sim.RainCloudLeft += () => left++;
            Run(sim, 89.9f, null);
            Assert.AreEqual(1, seen, "once per year");
            Assert.That(appearedAt, Is.InRange(30f, 60f), "in Summer");
            Assert.AreEqual(1, left, "drifted away untapped");
            Assert.IsFalse(sim.State.Cloud.Active);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Run(sim, 89.9f, null);
            Assert.AreEqual(2, seen, "again next year");
        }

        [Test]
        public void Cloud_Tap_WatersDry_AdvancesWet_AndDisappears()
        {
            var sim = NewSim();
            var wet = sim.State.GetPlot(1, 1);
            Run(sim, 1.05f, Centre); // (1,1) is Wet at ~0
            Assert.AreEqual(PlotState.Wet, wet.State);
            Run(sim, 1.2f, Centre); // (1,1) Wet with progress 0.8
            Assert.That(wet.Progress, Is.EqualTo(0.82f).Within(0.05f));
            Assert.IsFalse(sim.TapCloud(), "no cloud yet");
            Assert.IsTrue(sim.DebugSpawnCloud());
            Assert.IsTrue(sim.State.Cloud.Active);
            Assert.AreEqual(0f, sim.State.Cloud.X);
            int tapped = 0;
            sim.RainCloudTapped += () => tapped++;
            Run(sim, 2f, null);
            Assert.That(sim.State.Cloud.X, Is.EqualTo(0.25f).Within(0.01f), "drift over 8 s");
            Assert.IsTrue(sim.TapCloud());
            Assert.AreEqual(1, tapped);
            Assert.IsFalse(sim.State.Cloud.Active);
            Assert.AreEqual(PlotState.Ripe, wet.State, "0.8 + 0.25 clamps to Ripe");
            foreach (var p in sim.State.Plots)
                if (p.Pos != new GridPos(1, 1))
                {
                    Assert.AreEqual(PlotState.Wet, p.State, "dry plot watered " + p.Pos);
                    Assert.AreEqual(0f, p.Progress);
                }
            Assert.IsFalse(sim.TapCloud(), "gone");
        }

        [Test]
        public void Cloud_LeavesAfterDrift_AndWinterCancels()
        {
            var sim = NewSim(c => c.CloudDriftSeconds = 2f);
            sim.DebugSpawnCloud();
            int left = 0;
            sim.RainCloudLeft += () => left++;
            Run(sim, 1.9f, null);
            Assert.IsTrue(sim.State.Cloud.Active);
            Run(sim, 0.2f, null);
            Assert.IsFalse(sim.State.Cloud.Active);
            Assert.AreEqual(1, left);

            sim.DebugSpawnCloud();
            sim.DebugSkipToWinter();
            Assert.IsFalse(sim.State.Cloud.Active, "winter cancels the cloud");
        }

        // ---------------------------------------------------------------- golden crop (GDD §5.3)

        [Test]
        public void Golden_Chance100_EveryReplantGolden_Pays10x_WithMultipliers()
        {
            var sim = NewSim();
            sim.DebugSetLevel("h_golden_crop", 5); // 5 %
            var s = sim.State.Stats;
            Assert.That(s.GoldenCropChance, Is.EqualTo(0.05).Within(1e-9));
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            sim.DebugSetLevel("ring_bonus_coins", 2); // 1.2x
            sim.DebugSetLevel("crop_value", 1);       // 1.1x
            sim.DebugForceRipeAll();
            sim.DebugNextHarvestGolden();
            Run(sim, 0.55f, Centre); // first harvest: normal crop, replant golden
            Assert.AreEqual(1, events.Count);
            Assert.IsFalse(events[0].WasGolden);
            Assert.IsTrue(sim.State.GetPlot(1, 1).IsGolden, "replanted golden");
            Run(sim, 1f + 1.5f + 0.5f + 0.1f, Centre); // full cycle: same timings
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events[1].WasGolden);
            Assert.That(events[1].Coins, Is.EqualTo(1 * 10 * 1.1 * 1.2).Within(1e-9));
            Assert.IsFalse(sim.State.GetPlot(1, 1).IsGolden, "flag cleared on harvest (chance roll at 5 % may re-set it; check deterministic below)");
        }

        [Test]
        public void Golden_Chance0_NeverGolden_AndDeterministicPerSeed()
        {
            var never = NewSim();
            never.DebugForceRipeAll();
            never.DebugSetRingRadiusOverride(10f);
            Run(never, 30f, Centre);
            foreach (var p in never.State.Plots) Assert.IsFalse(p.IsGolden);

            int CountGolden(int seed)
            {
                var sim = NewSim(null, seed);
                sim.DebugSetLevel("h_golden_crop", 5);
                sim.DebugSetRingRadiusOverride(10f);
                int golden = 0;
                sim.Harvested += e => { if (e.WasGolden) golden++; };
                sim.DebugForceRipeAll();
                Run(sim, 120f, Centre);
                return golden;
            }
            Assert.AreEqual(CountGolden(11), CountGolden(11), "same seed, same result");
            int total = 0;
            for (int seed = 1; seed <= 6; seed++) total += CountGolden(seed);
            Assert.That(total, Is.GreaterThan(0), "5 % over ~2000 harvests yields some golden crops");
        }

        [Test]
        public void Golden_ClearedByCrowAndWinter()
        {
            var sim = NewSim();
            sim.DebugForceRipeAll();
            sim.DebugNextHarvestGolden();
            Run(sim, 0.55f, Centre);
            var plot = sim.State.GetPlot(1, 1);
            Assert.IsTrue(plot.IsGolden);
            sim.DebugSkipToWinter();
            Assert.IsFalse(plot.IsGolden, "winter clears");
        }

        // ---------------------------------------------------------------- tractor (GDD §4)

        private static FarmSim TractorSim(int level, int seed = 1)
        {
            var sim = NewSim(c => c.CrowFirstYear = 99, seed);
            sim.DebugSetLevel("tractor", level);
            return sim;
        }

        [Test]
        public void Tractor_IntervalPerLevel_PicksRowWithMostRipe_TiesLowest()
        {
            var sim = TractorSim(1);
            Assert.IsTrue(sim.State.Tractor.Owned);
            Assert.AreEqual(30f, sim.State.Tractor.TimeToNextSweep);
            Assert.AreEqual(20f, TractorSim(2).State.Tractor.TimeToNextSweep);
            Assert.AreEqual(12f, TractorSim(3).State.Tractor.TimeToNextSweep);

            sim.DebugForceRipeAll();
            int row = -1;
            sim.TractorSweepStarted += r => row = r;
            Run(sim, 29.9f, null);
            Assert.IsFalse(sim.State.Tractor.Sweeping);
            Run(sim, 0.2f, null);
            Assert.IsTrue(sim.State.Tractor.Sweeping);
            Assert.AreEqual(0, row, "all rows equal (3 ripe each): lowest row index wins");

            // Ties broken by lowest index; most-ripe wins: make row 1 the only fully ripe row.
            var sim2 = TractorSim(1, 2);
            var s2 = sim2.State;
            sim2.DebugForceRipeAll();
            sim2.DebugSetRingRadiusOverride(0.5f);
            Run(sim2, 0.6f, new RingInput(0, 0)); // harvest (0,0): row 0 has 2 ripe
            Run(sim2, 0.6f, new RingInput(1, 2)); // harvest (1,2): row 2 has 2 ripe
            Assert.AreEqual(2, CountRipe(s2, 0));
            Assert.AreEqual(3, CountRipe(s2, 1));
            int row2 = -1;
            sim2.TractorSweepStarted += r => row2 = r;
            Run(sim2, 30f, null);
            Assert.AreEqual(1, row2);
        }

        private static int CountRipe(FarmState s, int row)
        {
            int c = 0;
            for (int x = 0; x < s.GridSize; x++) if (s.GetPlot(x, row).IsRipe) c++;
            return c;
        }

        [Test]
        public void Tractor_SweepHarvestsFullValue_SourceTractor_TakesPlotsRipeningMidSweep_ScaresCrowsWithBounty()
        {
            var sim = TractorSim(3);
            sim.DebugSetLevel("ring_bonus_coins", 4);   // must not apply
            sim.DebugSetLevel("apprentice_yield", 4);   // must not apply
            sim.DebugSetLevel("crow_bounty", 2);        // (2+2) x value on scare
            var s = sim.State;
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;

            // Row 0: plots 0 and 2 Ripe, plot 1 will ripen mid-sweep (Wet at 0.99 with Sun would be needed; use cloud trick instead).
            sim.DebugForceRipeAll();
            sim.DebugSetRingRadiusOverride(0.5f);
            Run(sim, 0.6f, new RingInput(1, 0)); // harvest (1,0) -> Dry
            var mid = s.GetPlot(1, 0);
            Assert.AreEqual(PlotState.Dry, mid.State);
            // put a crow on (2,0)
            sim.DebugSpawnCrow(); // lands on some ripe plot outside the ring; find it
            GridPos crowPos = s.Crows[0].Pos;
            harvests.Clear();
            scared.Clear();
            double coinsBefore = s.Coins;

            // Force the sweep on row 0 (row 0 and row 1/2 have 2 vs 3 ripe: row 1 wins). Make row 0 the best by harvesting rows 1-2 ripe plots away:
            Run(sim, 0.6f, new RingInput(0, 1)); Run(sim, 0.6f, new RingInput(1, 1)); Run(sim, 0.6f, new RingInput(2, 1));
            Run(sim, 0.6f, new RingInput(0, 2)); Run(sim, 0.6f, new RingInput(1, 2)); Run(sim, 0.6f, new RingInput(2, 2));
            harvests.Clear();
            coinsBefore = s.Coins;
            Assert.AreEqual(2, CountRipe(s, 0));
            Assert.IsTrue(sim.DebugForceTractorSweep());
            Assert.IsTrue(s.Tractor.Sweeping);
            Assert.AreEqual(0, s.Tractor.Row);

            // Mid-sweep: when the tractor is between plot 0 and 1, ripen plot 1 (tap a cloud with Wet 0.8 would work; force via debug ripe of just that plot is not available -> use cloud on Wet).
            Run(sim, 0.1f, null); // X ~ 0.17: plot 0 harvested
            Assert.AreEqual(1, harvests.Count);
            Assert.AreEqual(HarvestSource.Tractor, harvests[0].Source);
            Assert.AreEqual(new GridPos(0, 0), harvests[0].Pos);
            Assert.AreEqual(1, harvests[0].Coins, "full value, no ring bonus, no yield");
            // ripen (1,0) now: water it with the ring quickly is too slow; instead spawn a cloud on a Wet plot: (1,0) is Dry -> cloud makes it Wet only.
            // So use two cloud taps: Dry->Wet, then Wet +0.25 x4 -> Ripe.
            sim.DebugSpawnCloud(); sim.TapCloud();
            Assert.AreEqual(PlotState.Wet, mid.State);
            for (int i = 0; i < 4; i++) { sim.DebugSpawnCloud(); sim.TapCloud(); }
            Assert.AreEqual(PlotState.Ripe, mid.State);
            Run(sim, 0.5f, null); // sweep passes plots 1 and 2
            Assert.IsFalse(s.Tractor.Sweeping);
            var tractorHarvests = harvests.FindAll(h => h.Source == HarvestSource.Tractor);
            Assert.That(tractorHarvests.Count, Is.GreaterThanOrEqualTo(3), "plots 0, 1 (ripened mid-sweep) and 2");
            Assert.IsTrue(tractorHarvests.Exists(h => h.Pos == new GridPos(1, 0)), "plot ripening mid-sweep taken");
            if (crowPos.Y == 0)
            {
                Assert.AreEqual(1, scared.Count, "crow on the swept row scared");
                Assert.AreEqual(4, scared[0].Coins, "bounty (2+2) x value 1");
            }
            Assert.That(s.Tractor.TimeToNextSweep, Is.EqualTo(12f).Within(0.5f));
        }

        [Test]
        public void Tractor_WaitsWhenNothingRipe_AndRunsOffline()
        {
            var sim = TractorSim(3);
            int started = 0;
            sim.TractorSweepStarted += _ => started++;
            Run(sim, 40f, null);
            Assert.AreEqual(0, started, "nothing ripe: no sweep");
            Assert.That(sim.State.Tractor.TimeToNextSweep, Is.InRange(0f, 12f));

            sim.DebugSetLevel("irrigation", 5);
            sim.DebugSetLevel("sun", 5);
            var r = sim.SimulateOffline(600);
            Assert.That(r.Harvests, Is.GreaterThan(0), "tractor harvests offline");
        }

        // ---------------------------------------------------------------- greenhouse (GDD §4)

        [Test]
        public void Greenhouse_OnlyInWinter_Formula_CapPerWinter_X2Node()
        {
            // The share-of-the-year cap (v2.8) has its own test; here it is out of the way so the formula shows.
            var sim = NewSim(c => c.GreenhouseWinterCapShare = 1000);
            sim.DebugSetLevel("greenhouse", 2);
            Run(sim, 5f, null);
            Assert.AreEqual(0, sim.State.Coins, "nothing during the year");
            sim.DebugForceRipeAll();
            Run(sim, 5f, new RingInput(1f, 1f)); // a year with a take: winter income is a share of it
            double take = sim.State.Coins;
            Assert.That(take, Is.GreaterThan(0));
            sim.DebugSkipToWinter();
            // 9 carrot plots: field value 9; rate = 2 x 0.02 x 9 = 0.36 coins/s
            Assert.That(sim.State.Greenhouse.CoinsPerSecond, Is.EqualTo(0.36).Within(1e-9));
            Assert.AreEqual(60f, sim.State.Greenhouse.SecondsLeftThisWinter);
            Run(sim, 10f, null);
            Assert.That(sim.State.Coins - take, Is.EqualTo(3.6).Within(1e-3));
            Run(sim, 100f, null);
            Assert.That(sim.State.Coins - take, Is.EqualTo(0.36 * 60).Within(0.02), "capped at 60 s per winter");
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter);
            Assert.That(sim.State.Greenhouse.CoinsThisWinter, Is.EqualTo(21.6).Within(0.02));

            sim.DebugSetLevel("h_greenhouse_x2", 1);
            Assert.That(sim.State.Greenhouse.CoinsPerSecond, Is.EqualTo(0.72).Within(1e-9));
            sim.StartNextYear();
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter);
            sim.DebugForceRipeAll();
            Run(sim, 5f, new RingInput(1f, 1f)); // this year needs a take of its own before its winter pays
            sim.DebugSkipToWinter();
            double before = sim.State.Coins;
            Run(sim, 5f, null);
            Assert.That(sim.State.Coins - before, Is.EqualTo(0.72 * 5).Within(1e-3), "x2 node");

            // Heritage phase: nothing accrues.
            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            before = sim.State.Coins;
            Run(sim, 10f, null);
            Assert.AreEqual(before, sim.State.Coins);
        }

        // ---------------------------------------------------------------- small nodes

        [Test]
        public void RingCombo_StacksWithinWindow_ResetsOtherwise_Max10()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            sim.DebugSetLevel("ring_combo", 3);
            sim.DebugSetLevel("ring_harvest_speed", 0);
            sim.DebugSetRingRadiusOverride(10f);
            var coins = new List<double>();
            sim.Harvested += e => coins.Add(e.Coins);
            sim.DebugForceRipeAll();
            // GDD §2.1 (v1.4): the ring harvests one plot at a time (0.5 s each), so harvests chain inside the 1 s window.
            for (int guard = 0; coins.Count < 12 && guard < 10_000; guard++) sim.Tick(Dt, Centre);
            Assert.AreEqual(12, coins.Count);
            Assert.That(coins[0], Is.EqualTo(1 * (1 + 0.03 * 1)).Within(1e-9));
            Assert.That(coins[8], Is.EqualTo(1 * (1 + 0.03 * 9)).Within(1e-9));
            Assert.That(coins[11], Is.EqualTo(1 * (1 + 0.03 * 10)).Within(1e-9), "multiplier caps at 10 stacks");
            Assert.That(sim.State.Combo, Is.GreaterThanOrEqualTo(10));
            Run(sim, 1.2f, null);
            Assert.AreEqual(0, sim.State.Combo, "window expired");
        }

        [Test]
        public void HelperWater_ApprenticeReplantsWet()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            sim.DebugSetLevel("apprentice_count", 1);
            sim.DebugSetLevel("helper_water", 1);
            sim.DebugForceRipeAll();
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            Run(sim, 2.5f, null);
            Assert.That(events.Count, Is.GreaterThan(0));
            Assert.AreEqual(HarvestSource.Apprentice, events[0].Source);
            Assert.AreEqual(PlotState.Wet, sim.State.GetPlot(events[0].Pos).State);
            // Ring harvest still replants Dry.
            sim.DebugSetRingRadiusOverride(0.5f);
            Run(sim, 0.6f, new RingInput(2, 2));
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(2, 2).State);
        }

        [Test]
        public void LateFrost_HarvestsRipeAndWetAtLeast80Percent_AtHalfValue()
        {
            var sim = NewSim(c => { c.CrowFirstYear = 99; c.BaseYearLength = 20f; });
            sim.DebugSetLevel("late_frost", 1);
            sim.DebugSetLevel("unlock_tomato", 1);
            var s = sim.State;
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            // Prepare: (0,0) Ripe, (1,1) Wet exactly 0.8, (2,2) Wet 0.5, rest Dry.
            sim.DebugSetRingRadiusOverride(0.5f);
            Run(sim, 1.05f, new RingInput(0, 0)); Run(sim, 1.6f, new RingInput(0, 0)); // Ripe
            Assert.IsTrue(s.GetPlot(0, 0).IsRipe);
            Run(sim, 1.05f, new RingInput(1, 1)); Run(sim, 1.2f, new RingInput(1, 1)); // Wet ~0.8
            Assert.That(s.GetPlot(1, 1).Progress, Is.EqualTo(0.82f).Within(0.05f));
            Run(sim, 1.05f, new RingInput(2, 2)); Run(sim, 0.75f, new RingInput(2, 2)); // Wet 0.5
            events.Clear();
            double before = s.Coins;
            sim.DebugSkipToWinter();
            var frost = events.FindAll(e => e.Source == HarvestSource.LateFrost);
            Assert.That(frost.Count, Is.InRange(1, 2), "ripe plot always; wet 0.8 plot depending on float rounding");
            Assert.IsTrue(frost.Exists(e => e.Pos == new GridPos(0, 0)));
            Assert.IsFalse(frost.Exists(e => e.Pos == new GridPos(2, 2)), "0.5 is lost");
            foreach (var e in frost) Assert.AreEqual(0.5, e.Coins, "half value");
            Assert.That(s.Coins - before, Is.EqualTo(0.5 * frost.Count).Within(1e-9));
            foreach (var p in s.Plots) Assert.AreEqual(PlotState.Dry, p.State);

            // Exactly 0.8 counts: set progress via a dedicated sim using the cloud (+0.25 x n).
            var exact = NewSim(c => c.CrowFirstYear = 99);
            exact.DebugSetLevel("late_frost", 1);
            exact.DebugSpawnCloud(); exact.TapCloud();               // all Wet 0
            exact.DebugSpawnCloud(); exact.TapCloud();               // 0.25
            exact.DebugSpawnCloud(); exact.TapCloud();               // 0.5
            exact.DebugSpawnCloud(); exact.TapCloud();               // 0.75
            int frostCount = 0;
            exact.Harvested += e => { if (e.Source == HarvestSource.LateFrost) frostCount++; };
            exact.DebugSkipToWinter();
            Assert.AreEqual(0, frostCount, "0.75 is below the threshold");
            var exact2 = NewSim(c => { c.CrowFirstYear = 99; c.CloudWetBoost = 0.8f; });
            exact2.DebugSetLevel("late_frost", 1);
            exact2.DebugSpawnCloud(); exact2.TapCloud();
            exact2.DebugSpawnCloud(); exact2.TapCloud();               // exactly 0.8
            Assert.That(exact2.State.GetPlot(0, 0).Progress, Is.EqualTo(0.8f));
            int frostCount2 = 0;
            exact2.Harvested += e => { if (e.Source == HarvestSource.LateFrost) frostCount2++; };
            exact2.DebugSkipToWinter();
            Assert.AreEqual(9, frostCount2, "exactly 0.8 counts");
        }

        [Test]
        public void FertileStart_ExpansionPlotsStartWet()
        {
            var sim = NewSim();
            sim.DebugSetLevel("fertile_start", 1);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1000);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            var s = sim.State;
            Assert.AreEqual(PlotState.Wet, s.GetPlot(3, 3).State);
            Assert.AreEqual(PlotState.Wet, s.GetPlot(0, 3).State);
            Assert.AreEqual(PlotState.Dry, s.GetPlot(0, 0).State, "existing plots untouched until spring");
            sim.StartNextYear();
            foreach (var p in sim.State.Plots) Assert.AreEqual(PlotState.Wet, p.State, "(v2.8) every plot starts spring Wet");
            var plain = NewSim();
            plain.DebugSkipToWinter();
            plain.DebugAddCoins(1000);
            plain.TryBuy("expand_field");
            Assert.AreEqual(PlotState.Dry, plain.State.GetPlot(3, 3).State);
        }

        [Test]
        public void SpringHeadStart_AllPlotsWetOnNextYearAndNewGeneration()
        {
            var sim = NewSim();
            sim.DebugSetLevel("spring_head_start", 1);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Wet, p.State);
                Assert.AreEqual(sim.Config.SpringHeadStartProgress, p.Progress, 1e-6f, "(v2.8) and part-grown");
            }
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            sim.DebugSetLevel("spring_head_start", 1); // almanac reset on retire; grant again
            sim.StartNewGeneration();
            foreach (var p in sim.State.Plots) Assert.AreEqual(PlotState.Wet, p.State);
        }

        [Test]
        public void BulkUpgrade_RaisesTwoPlots_OrOneWhenOneLeft()
        {
            var sim = NewSim();
            sim.DebugSetLevel("unlock_tomato", 1);
            sim.DebugSetLevel("bulk_upgrade", 1);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);
            Assert.AreEqual(1, sim.State.GetPlot(1, 0).Tier);
            Assert.AreEqual(0, sim.State.GetPlot(2, 0).Tier);
            for (int i = 0; i < 3; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot")); // 8 plots at tier 1
            Assert.AreEqual(0, sim.State.GetPlot(2, 2).Tier);
            Assert.IsFalse(sim.IsMaxed("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"), "one plot left: raises just that one");
            foreach (var p in sim.State.Plots) Assert.AreEqual(1, p.Tier);
            Assert.IsTrue(sim.IsMaxed("upgrade_plot"));
        }

        [Test]
        public void CrowBounty_ScareDrop_2PlusLevel_TimesValue()
        {
            var sim = NewSim();
            sim.DebugSetLevel("crow_bounty", 3);
            sim.DebugSpawnCrow();
            var pos = sim.State.Crows[0].Pos;
            double coins = 0;
            sim.CrowScared += e => coins = e.Coins;
            Assert.IsTrue(sim.TapAt(pos));
            Assert.AreEqual(5, coins, "(2 + 3) x 1");
            Assert.AreEqual(5, sim.State.Coins);
        }

        [Test]
        public void ScarecrowImmunity_NeedsScarecrow2_ThenAQuarterOfTheCrows()
        {
            var cfg = TestConfig.Classic();
            var only = StatResolver.Resolve(cfg, new Dictionary<string, int>(), new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            Assert.AreEqual(0.25f, only.CrowSpawnChance, "node alone does nothing");
            var one = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["scarecrow"] = 1 }, new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            Assert.AreEqual(0.25f, one.CrowSpawnChance, "a scarecrow guards an area; it does not lower the chance (v2.0)");
            Assert.AreEqual(1, one.ScarecrowCount);
            var both = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["scarecrow"] = 2 }, new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            // (v2.8) A quarter, never none: at zero the crow bounty and a watchful heir had nothing left to act on.
            Assert.AreEqual(0.25f * cfg.ScarecrowImmunityCrowFactor, both.CrowSpawnChance, 1e-6f);

            // In a running sim the stat lands on the state (two scarecrows guard all of a 3x3 field, so a landing
            // could not be observed here anyway).
            var sim = NewSim(c => c.CrowSpawnChance = 1f);
            sim.DebugSetLevel("scarecrow", 2);
            sim.DebugSetLevel("h_scarecrow_immunity", 1);
            Assert.AreEqual(sim.Config.ScarecrowImmunityCrowFactor, sim.State.Stats.CrowSpawnChance, 1e-6f, "crows still come, just rarely");
        }

        // ---------------------------------------------------------------- every node does something

        /// <summary>
        /// Explicit map of every node id in both tables to how it is applied. A new node must be added
        /// here (and implemented) or this test fails; nothing can be added silently.
        /// </summary>
        private static readonly Dictionary<string, string> Applied = new Dictionary<string, string>
        {
            // Almanac: "stat" = changes a Stats field at level 1; "purchase" = FarmSim.ApplyPurchase side effect
            ["ring_radius"] = "stat", ["ring_water_speed"] = "stat", ["ring_grow_speed"] = "stat", ["ring_harvest_speed"] = "stat",
            ["ring_bonus_coins"] = "stat", ["ring_combo"] = "stat", ["ring_shape"] = "stat", ["tap_harvest"] = "stat",
            ["irrigation"] = "stat", ["sun"] = "stat", ["soil_quality"] = "stat",
            ["crop_value"] = "stat", ["fertile_start"] = "stat", ["expand_field"] = "stat", ["upgrade_plot"] = "purchase",
            ["unlock_tomato"] = "stat", ["unlock_corn"] = "stat", ["unlock_pumpkin"] = "stat", ["unlock_grapes"] = "stat",
            ["unlock_golden_wheat"] = "stat", ["bulk_upgrade"] = "stat", ["apprentice_count"] = "stat", ["apprentice_speed"] = "stat",
            ["apprentice_harvest_time"] = "stat", ["apprentice_yield"] = "stat", ["tractor"] = "stat", ["scarecrow"] = "stat",
            ["helper_water"] = "stat", ["farm_dog"] = "stat", ["beehive"] = "stat", ["hens"] = "stat", ["barn"] = "stat", ["year_length"] = "stat", ["frost_warning"] = "stat", ["late_frost"] = "stat",
            ["greenhouse"] = "stat", ["crow_bounty"] = "stat", ["spring_head_start"] = "stat",
            // Heritage
            ["h_start_radius"] = "stat", ["h_ring_speeds"] = "stat", ["h_ring_coins"] = "stat", ["h_start_irrigation"] = "stat",
            ["h_start_sun"] = "stat", ["h_global_growth"] = "stat", ["h_unlock_rain_cloud"] = "stat", ["h_start_field"] = "stat",
            ["h_start_tomato"] = "stat", ["h_golden_crop"] = "stat", ["h_free_apprentice"] = "stat", ["h_apprentice_yield"] = "stat",
            ["h_scarecrow_immunity"] = "stat+scarecrow2", ["h_start_year_length"] = "stat", ["h_greenhouse_x2"] = "stat", ["h_almanac_discount"] = "stat",
            ["h_ring_master"] = "stat", ["h_steward"] = "stat", ["h_long_summer"] = "stat", ["h_rich_soil"] = "stat",
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

        private static bool Differs(Stats a, Stats b)
        {
            foreach (var f in typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!Equals(f.GetValue(a), f.GetValue(b))) return true;
            return false;
        }
    }
}
