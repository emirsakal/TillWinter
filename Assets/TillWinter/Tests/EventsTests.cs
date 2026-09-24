using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;
using static TillWinter.Tests.CoreLoopTests;

namespace TillWinter.Tests
{
    /// <summary>Session 3 (re-themed v3): rain cloud, golden crop, tractor, greenhouse and the remaining small nodes.</summary>
    public class EventsTests
    {
        private static readonly GridPos Centre = new GridPos(1, 1);

        // ---------------------------------------------------------------- rain cloud (GDD §5.2)

        [Test]
        public void Cloud_NeverSpawnsWhenLocked_SpawnsOncePerSummerWhenUnlocked()
        {
            var locked = NewSim();
            int appeared = 0;
            locked.RainCloudAppeared += () => appeared++;
            Run(locked, 89f);
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
            Run(sim, 89.9f);
            Assert.AreEqual(1, seen, "once per year");
            Assert.That(appearedAt, Is.InRange(30f, 60f), "in Summer");
            Assert.AreEqual(1, left, "drifted away untapped");
            Assert.IsFalse(sim.State.Cloud.Active);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Run(sim, 89.9f);
            Assert.AreEqual(2, seen, "again next year");
        }

        [Test]
        public void Cloud_Tap_GivesEveryGrowingCropABurst_LeavesHardGround_AndDisappears()
        {
            var sim = NewSim(c => c.Crops[0].Grow = 1000f); // growth by itself is negligible here
            var a = sim.State.GetPlot(Centre);
            var b = sim.State.GetPlot(0, 0);
            var hard = sim.State.GetPlot(2, 2);
            sim.DebugBreak(Centre);
            sim.DebugBreak(new GridPos(0, 0));
            Assert.IsFalse(sim.TapCloud(), "no cloud yet");
            Assert.IsTrue(sim.DebugSpawnCloud());
            Assert.IsTrue(sim.State.Cloud.Active);
            Assert.AreEqual(0f, sim.State.Cloud.X);
            int tapped = 0, watered = 0;
            sim.RainCloudTapped += () => tapped++;
            sim.PlotWatered += _ => watered++;
            Run(sim, 2f);
            Assert.That(sim.State.Cloud.X, Is.EqualTo(0.25f).Within(0.01f), "drift over 8 s");
            Assert.IsTrue(sim.TapCloud());
            Assert.AreEqual(1, tapped);
            Assert.AreEqual(2, watered, "one burst per growing crop");
            Assert.IsFalse(sim.State.Cloud.Active);
            Assert.That(a.Progress, Is.EqualTo(sim.Config.CloudGrowBoost).Within(0.01f));
            Assert.That(b.Progress, Is.EqualTo(sim.Config.CloudGrowBoost).Within(0.01f));
            Assert.AreEqual(PlotState.Hard, hard.State, "hard ground drinks nothing");
            Assert.AreEqual(hard.MaxHp, hard.Hp, 1e-9);
            Assert.IsFalse(sim.TapCloud(), "gone");
            // Three more bursts: 0.25 x 4 = 1 -> Ripe.
            for (int i = 0; i < 3; i++) { sim.DebugSpawnCloud(); sim.TapCloud(); }
            Assert.AreEqual(PlotState.Ripe, a.State);
            Assert.AreEqual(PlotState.Ripe, b.State);
        }

        [Test]
        public void Cloud_LeavesAfterDrift_AndWinterCancels()
        {
            var sim = NewSim(c => c.CloudDriftSeconds = 2f);
            sim.DebugSpawnCloud();
            int left = 0;
            sim.RainCloudLeft += () => left++;
            Run(sim, 1.9f);
            Assert.IsTrue(sim.State.Cloud.Active);
            Run(sim, 0.2f);
            Assert.IsFalse(sim.State.Cloud.Active);
            Assert.AreEqual(1, left);

            sim.DebugSpawnCloud();
            sim.DebugSkipToWinter();
            Assert.IsFalse(sim.State.Cloud.Active, "winter cancels the cloud");
        }

        // ---------------------------------------------------------------- golden crop (GDD §5.3)

        [Test]
        public void Golden_TheSeedThatDropsMayBeGolden_Pays10x_WithMultipliers_AndClearsAtTheReap()
        {
            var sim = NewSim();
            sim.DebugSetLevel("h_golden_crop", 5); // 5 %
            Assert.That(sim.State.Stats.GoldenCropChance, Is.EqualTo(0.05).Within(1e-9));
            sim.DebugSetLevel("crop_value", 1); // 1.1x
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            var plot = sim.State.GetPlot(Centre);
            sim.DebugNextHarvestGolden();
            sim.DebugBreak(Centre);
            Assert.IsTrue(plot.IsGolden, "the seed that dropped is golden");
            Run(sim, 3f); // same timings: carrot 2.5 s
            Assert.AreEqual(PlotState.Ripe, plot.State);
            Assert.IsTrue(sim.ReapOne(Centre));
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].WasGolden);
            Assert.That(events[0].Coins, Is.EqualTo(1 * 10 * 1.1).Within(1e-9), "value x golden x crop_value");
            Assert.AreEqual(1, sim.State.Generation.GoldenHarvests);
            Assert.IsFalse(plot.IsGolden, "the flag goes with the crop");
            Assert.AreEqual(PlotState.Hard, plot.State);
        }

        [Test]
        public void Golden_Chance0_NeverGolden_AndDeterministicPerSeed()
        {
            var never = NewSim();
            for (int i = 0; i < 30; i++)
            {
                never.DebugBreakAll();
                foreach (var p in never.State.Plots) Assert.IsFalse(p.IsGolden);
                never.DebugForceRipeAll();
                never.ReapAll();
            }

            int CountGolden(int seed)
            {
                var sim = NewSim(null, seed);
                sim.DebugSetLevel("h_golden_crop", 5);
                int golden = 0;
                sim.Harvested += e => { if (e.WasGolden) golden++; };
                for (int i = 0; i < 400; i++)
                {
                    sim.DebugBreakAll();
                    sim.DebugForceRipeAll();
                    sim.ReapAll();
                }
                return golden;
            }
            Assert.AreEqual(CountGolden(11), CountGolden(11), "same seed, same result");
            int total = 0;
            for (int seed = 1; seed <= 6; seed++) total += CountGolden(seed);
            Assert.That(total, Is.GreaterThan(0), "5 % over 3600 seeds a run yields some golden crops");
        }

        [Test]
        public void Golden_GoesWithTheCrop_WhenACrowEatsIt_OrTheFrostReapsIt()
        {
            var eaten = NewSim();
            var plot = eaten.State.GetPlot(Centre);
            eaten.DebugNextHarvestGolden();
            eaten.DebugBreak(Centre);
            Run(eaten, 3f);
            Assert.IsTrue(plot.IsGolden && plot.IsRipe);
            Assert.IsTrue(eaten.DebugSpawnCrow(), "lands on the one ripe plot");
            Assert.IsTrue(plot.HasCrow);
            Run(eaten, eaten.Config.CrowEatTime + 0.1f);
            Assert.AreEqual(PlotState.Hard, plot.State);
            Assert.IsFalse(plot.IsGolden, "eaten");

            var frozen = NewSim();
            var fp = frozen.State.GetPlot(Centre);
            frozen.DebugNextHarvestGolden();
            frozen.DebugBreak(Centre);
            Run(frozen, 3f);
            HarvestEvent last = default;
            frozen.Harvested += e => last = e;
            frozen.DebugSkipToWinter();
            Assert.AreEqual(HarvestSource.Frost, last.Source);
            Assert.IsTrue(last.WasGolden, "the frost reaps it at its golden price");
            Assert.AreEqual(10, last.Coins, 1e-9);
            Assert.IsFalse(fp.IsGolden);
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
            Run(sim, 29.9f);
            Assert.IsFalse(sim.State.Tractor.Sweeping);
            Run(sim, 0.2f);
            Assert.IsTrue(sim.State.Tractor.Sweeping);
            Assert.AreEqual(0, row, "all rows equal (3 ripe each): lowest row index wins");

            // Most-ripe wins: reap one plot off rows 0 and 2 so row 1 is the only full one.
            var sim2 = TractorSim(1, 2);
            var s2 = sim2.State;
            sim2.DebugForceRipeAll();
            Assert.IsTrue(sim2.ReapOne(new GridPos(0, 0)));
            Assert.IsTrue(sim2.ReapOne(new GridPos(1, 2)));
            Assert.AreEqual(2, CountRipe(s2, 0));
            Assert.AreEqual(3, CountRipe(s2, 1));
            int row2 = -1;
            sim2.TractorSweepStarted += r => row2 = r;
            Run(sim2, 30f);
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
            sim.DebugSetLevel("apprentice_yield", 4); // must not apply
            sim.DebugSetLevel("crow_bounty", 2);      // (2+2) x value on scare
            var s = sim.State;
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;

            // Row 0: plots 0 and 2 ripe, plot 1 still hard; a crow on one of the two ripe plots.
            sim.DebugForceRipe(new GridPos(0, 0));
            sim.DebugForceRipe(new GridPos(2, 0));
            Assert.IsTrue(sim.DebugSpawnCrow());
            Assert.AreEqual(0, s.Crows[0].Pos.Y, "on the ripe row");
            Assert.IsTrue(sim.DebugForceTractorSweep());
            Assert.IsTrue(s.Tractor.Sweeping);
            Assert.AreEqual(0, s.Tractor.Row);

            // 0.15 s a plot from X = -0.5: plot 0 at 0.075 s, plot 1 at 0.225 s, plot 2 at 0.375 s, done at 0.525 s.
            Run(sim, 0.1f);
            Assert.AreEqual(1, harvests.Count);
            Assert.AreEqual(HarvestSource.Tractor, harvests[0].Source);
            Assert.AreEqual(new GridPos(0, 0), harvests[0].Pos);
            Assert.AreEqual(1, harvests[0].Coins, 1e-9, "full value: no combo, no apprentice yield");
            sim.DebugForceRipe(new GridPos(1, 0)); // ripens mid-sweep
            Run(sim, 0.5f);
            Assert.IsFalse(s.Tractor.Sweeping);
            Assert.AreEqual(3, harvests.Count, "plots 0, 1 (ripened mid-sweep) and 2");
            foreach (var h in harvests) Assert.AreEqual(HarvestSource.Tractor, h.Source);
            Assert.IsTrue(harvests.Exists(h => h.Pos == new GridPos(1, 0)));
            Assert.AreEqual(1, scared.Count, "the crow on the swept row is scared, and its crop taken");
            Assert.AreEqual(4, scared[0].Coins, 1e-9, "bounty (2+2) x value 1");
            Assert.AreEqual(0, s.Crows.Count);
            Assert.That(s.Tractor.TimeToNextSweep, Is.EqualTo(12f).Within(0.5f));
        }

        [Test]
        public void Tractor_WaitsWhenNothingRipe_AndRunsOffline()
        {
            var sim = TractorSim(3);
            int started = 0;
            sim.TractorSweepStarted += _ => started++;
            Run(sim, 40f);
            Assert.AreEqual(0, started, "nothing ripe on unbroken ground: no sweep");
            Assert.That(sim.State.Tractor.TimeToNextSweep, Is.InRange(0f, 12f));

            sim.DebugBreakAll(); // the seeds drop and grow while away; the tractor reaps them
            var r = sim.SimulateOffline(600);
            Assert.That(r.Harvests, Is.GreaterThan(0), "tractor harvests offline");
            Assert.AreEqual(r.Harvests, r.HarvestsTractor);
        }

        // ---------------------------------------------------------------- greenhouse (GDD §4)

        [Test]
        public void Greenhouse_OnlyInWinter_Formula_CapPerWinter_X2Node()
        {
            // The share-of-the-year cap (v2.8) has its own test; here it is out of the way so the formula shows.
            var sim = NewSim(c => c.GreenhouseWinterCapShare = 1000);
            sim.DebugSetLevel("greenhouse", 2);
            Run(sim, 5f);
            Assert.AreEqual(0, sim.State.Coins, "nothing during the year");
            sim.DebugForceRipeAll();
            sim.ReapAll(); // a year with a take: winter income is a share of it
            double take = sim.State.Coins;
            Assert.That(take, Is.GreaterThan(0));
            sim.DebugSkipToWinter();
            // 9 carrot plots: field value 9; rate = 2 x 0.02 x 9 = 0.36 coins/s
            Assert.That(sim.State.Greenhouse.CoinsPerSecond, Is.EqualTo(0.36).Within(1e-9));
            Assert.AreEqual(60f, sim.State.Greenhouse.SecondsLeftThisWinter);
            Run(sim, 10f);
            Assert.That(sim.State.Coins - take, Is.EqualTo(3.6).Within(1e-3));
            Run(sim, 100f);
            Assert.That(sim.State.Coins - take, Is.EqualTo(0.36 * 60).Within(0.02), "capped at 60 s per winter");
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter);
            Assert.That(sim.State.Greenhouse.CoinsThisWinter, Is.EqualTo(21.6).Within(0.02));

            sim.DebugSetLevel("h_greenhouse_x2", 1);
            Assert.That(sim.State.Greenhouse.CoinsPerSecond, Is.EqualTo(0.72).Within(1e-9));
            sim.StartNextYear();
            Assert.AreEqual(0f, sim.State.Greenhouse.SecondsLeftThisWinter);
            sim.DebugForceRipeAll();
            sim.ReapAll(); // this year needs a take of its own before its winter pays
            sim.DebugSkipToWinter();
            double before = sim.State.Coins;
            Run(sim, 5f);
            Assert.That(sim.State.Coins - before, Is.EqualTo(0.72 * 5).Within(1e-3), "x2 node");

            // Heritage phase: nothing accrues.
            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            before = sim.State.Coins;
            Run(sim, 10f);
            Assert.AreEqual(before, sim.State.Coins);
        }

        // ---------------------------------------------------------------- small nodes

        [Test]
        public void LateFrost_RipeIsReapedByTheFrostInFull_GrowingAtLeast80PercentPaysHalf_TheRestWaits()
        {
            // Grow 1000 s so progress only moves by the cloud's bursts; 0.4 + 0.4 = 0.8 exactly in float.
            var sim = NewSim(c => { c.CrowFirstYear = 99; c.Crops[0].Grow = 1000f; c.CloudGrowBoost = 0.4f; });
            sim.DebugSetLevel("late_frost", 1);
            var s = sim.State;
            sim.DebugBreak(new GridPos(1, 1));
            sim.DebugSpawnCloud(); sim.TapCloud();               // (1,1) 0.4
            sim.DebugBreak(new GridPos(2, 2));
            sim.DebugSpawnCloud(); sim.TapCloud();               // (1,1) 0.8, (2,2) 0.4
            Assert.AreEqual(sim.Config.LateFrostThreshold, s.GetPlot(1, 1).Progress, "exactly the threshold");
            Assert.IsTrue(sim.DebugForceRipe(new GridPos(0, 0)));
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;
            double before = s.Coins;
            sim.DebugSkipToWinter();
            Assert.AreEqual(2, events.Count);
            var frost = events.Find(e => e.Source == HarvestSource.Frost);
            var late = events.Find(e => e.Source == HarvestSource.LateFrost);
            Assert.AreEqual(new GridPos(0, 0), frost.Pos);
            Assert.AreEqual(1, frost.Coins, 1e-9, "ripe: the plain price");
            Assert.AreEqual(new GridPos(1, 1), late.Pos);
            Assert.AreEqual(0.5, late.Coins, 1e-9, "nearly grown: half now");
            Assert.AreEqual(1.5, s.Coins - before, 1e-9);
            Assert.AreEqual(PlotState.Hard, s.GetPlot(0, 0).State);
            Assert.AreEqual(1, s.GetPlot(0, 0).Layer, "reaped: a layer deeper");
            Assert.AreEqual(PlotState.Hard, s.GetPlot(1, 1).State);
            Assert.AreEqual(1, s.GetPlot(1, 1).Layer);
            Assert.AreEqual(PlotState.Growing, s.GetPlot(2, 2).State, "0.4 waits for spring");
            Assert.AreEqual(0.4f, s.GetPlot(2, 2).Progress, 1e-6f);
            Assert.AreEqual(2, s.Generation.HarvestsLateFrost, "both reaps are the frost's doing: the stats screen counts them together");

            var without = NewSim(c => { c.CrowFirstYear = 99; c.Crops[0].Grow = 1000f; c.CloudGrowBoost = 0.9f; });
            without.DebugBreak(new GridPos(1, 1));
            without.DebugSpawnCloud(); without.TapCloud();
            int paid = 0;
            without.Harvested += _ => paid++;
            without.DebugSkipToWinter();
            Assert.AreEqual(0, paid, "without the node a growing crop only waits");
            Assert.AreEqual(PlotState.Growing, without.State.GetPlot(1, 1).State);
        }

        [Test]
        public void SpringHeadStart_OpensANewGenerationsFirstSpring_HalfCracked()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            sim.DebugSetLevel("spring_head_start", 1); // the Almanac reset on retire; grant it again
            sim.StartNewGeneration();
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Hard, p.State);
                Assert.AreEqual(p.MaxHp * sim.Config.SpringHeadStartProgress, p.Hp, 1e-9, p.Pos.ToString());
            }
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
        public void CrowBounty_ScareDrop_2PlusLevel_TimesValue_OnTopOfTheCrop()
        {
            var sim = NewSim();
            sim.DebugSetLevel("crow_bounty", 3);
            Assert.IsTrue(sim.DebugSpawnCrow());
            var pos = sim.State.Crows[0].Pos;
            double bounty = 0;
            sim.CrowScared += e => bounty = e.Coins;
            Assert.IsTrue(sim.TapAt(pos));
            Assert.AreEqual(5, bounty, 1e-9, "(2 + 3) x 1");
            Assert.AreEqual(5 + 1, sim.State.Coins, 1e-9, "and the carrot it sat on is reaped");
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(pos).State);
        }

        [Test]
        public void ScarecrowImmunity_NeedsScarecrow2_ThenAQuarterOfTheCrows()
        {
            var cfg = TestConfig.Classic();
            var only = StatResolver.Resolve(cfg, new Dictionary<string, int>(), new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            Assert.AreEqual(cfg.CrowSpawnChance, only.CrowSpawnChance, "node alone does nothing");
            var one = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["scarecrow"] = 1 }, new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            Assert.AreEqual(cfg.CrowSpawnChance, one.CrowSpawnChance, "a scarecrow guards an area; it does not lower the chance (v2.0)");
            Assert.AreEqual(1, one.ScarecrowCount);
            var both = StatResolver.Resolve(cfg, new Dictionary<string, int> { ["scarecrow"] = 2 }, new Dictionary<string, int> { ["h_scarecrow_immunity"] = 1 });
            // (v2.8) A quarter, never none: at zero the crow bounty and a watchful heir had nothing left to act on.
            Assert.AreEqual(cfg.CrowSpawnChance * cfg.ScarecrowImmunityCrowFactor, both.CrowSpawnChance, 1e-6f);

            // In a running sim the stat lands on the state (two scarecrows guard all of a 3x3 field, so a landing
            // could not be observed here anyway).
            var sim = NewSim(c => c.CrowSpawnChance = 1f);
            sim.DebugSetLevel("scarecrow", 2);
            sim.DebugSetLevel("h_scarecrow_immunity", 1);
            Assert.AreEqual(sim.Config.ScarecrowImmunityCrowFactor, sim.State.Stats.CrowSpawnChance, 1e-6f, "crows still come, just rarely");
        }
    }
}
