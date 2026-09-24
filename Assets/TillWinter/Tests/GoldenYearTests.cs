using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>GDD §8 ending: the Golden Year follows a fully maxed Heritage tree, once; stats counters.</summary>
    public class GoldenYearTests
    {
        /// <summary>A sim retired once and sitting on the Heritage screen.</summary>
        private static FarmSim InHeritage(FarmConfig config = null, bool maxHeritage = true)
        {
            var sim = new FarmSim(config ?? new FarmConfig(), 5);
            if (maxHeritage) sim.DebugMaxHeritage();
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.Retire(), "retire");
            Assert.AreEqual(Phase.Heritage, sim.State.Phase);
            return sim;
        }

        /// <summary>Ticks the current year until Winter; returns the number of ticks.</summary>
        private static int PlayToWinter(FarmSim sim, System.Action<FarmSim> eachTick = null)
        {
            int ticks = 0;
            while (sim.State.Phase == Phase.Year && ticks < 100_000)
            {
                sim.Tick(0.5f);
                eachTick?.Invoke(sim);
                ticks++;
            }
            return ticks;
        }

        /// <summary>The counter the Heritage screen signposts the ending with agrees with the ending itself.</summary>
        [Test]
        public void HeritageProgress_CountsDoneNodes_AndIsFullOnlyWhenComplete()
        {
            var fresh = new FarmSim(new FarmConfig(), 5);
            Assert.AreEqual(fresh.Heritage.Nodes.Count, fresh.HeritageTotal);
            Assert.AreEqual(0, fresh.HeritageDone);
            Assert.IsFalse(fresh.HeritageComplete);

            var sim = InHeritage();
            Assert.IsTrue(sim.HeritageComplete);
            Assert.AreEqual(sim.HeritageTotal, sim.HeritageDone, "a complete tree counts every node");

            sim.DebugSetLevel("h_golden_crop", 4); // one node short of max
            Assert.IsFalse(sim.HeritageComplete);
            Assert.AreEqual(sim.HeritageTotal - 1, sim.HeritageDone);
        }

        [Test]
        public void NotTriggered_UnlessEveryHeritageNodeIsMaxed()
        {
            var sim = InHeritage();
            sim.DebugSetLevel("h_golden_crop", 4); // one node short of max
            Assert.IsFalse(sim.HeritageComplete);
            sim.StartNewGeneration();
            Assert.IsFalse(sim.State.GoldenYearActive);
        }

        [Test]
        public void Triggered_WhenHeritageIsFullyMaxed_WithTheGoldenField()
        {
            var sim = InHeritage();
            Assert.IsTrue(sim.HeritageComplete);
            bool started = false;
            sim.GoldenYearStarted += () => started = true;
            sim.StartNewGeneration();

            Assert.IsTrue(started);
            Assert.IsTrue(sim.State.GoldenYearActive);
            Assert.AreEqual(Phase.Year, sim.State.Phase);
            Assert.AreEqual(sim.Config.MaxGridSize, sim.State.GridSize, "6x6");
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(sim.Config.MaxTier, p.Tier, "golden wheat");
                Assert.IsTrue(p.IsGolden, "all golden");
                Assert.AreEqual(PlotState.Growing, p.State, "already planted: no ground to break");
                Assert.AreEqual(0f, p.Progress);
            }
            Assert.AreEqual(300f, sim.State.Stats.YearLength, 1e-4f);
        }

        [Test]
        public void NoCrows_NoFrost_DuringTheGoldenYear()
        {
            var cfg = new FarmConfig { CrowFirstYear = 1 }; // crows would be allowed in year 1; the Golden Year must still stop them
            var sim = InHeritage(cfg);
            sim.StartNewGeneration();
            bool frost = false;
            sim.FrostWarningStarted += () => frost = true;
            int maxCrows = 0;
            PlayToWinter(sim, s => { if (s.State.Crows.Count > maxCrows) maxCrows = s.State.Crows.Count; if (s.State.FrostWarning) frost = true; });
            Assert.AreEqual(0, maxCrows, "no crows");
            Assert.IsFalse(frost, "no frost warning");
        }

        [Test]
        public void GoldenYear_EndsIntoANormalWinter_AndHappensOnlyOnce()
        {
            var sim = InHeritage();
            sim.StartNewGeneration();
            bool ended = false, endedBeforeWinter = false, winter = false;
            sim.GoldenYearEnded += () => { ended = true; endedBeforeWinter = !winter; };
            sim.WinterStarted += () => winter = true;
            PlayToWinter(sim);

            Assert.IsTrue(ended);
            Assert.IsTrue(endedBeforeWinter, "the ending runs before the Winter screen");
            Assert.AreEqual(Phase.Winter, sim.State.Phase);
            Assert.IsTrue(sim.State.EndingSeen);
            Assert.IsFalse(sim.State.GoldenYearActive);
            Assert.AreEqual(sim.State.Stats.TargetGridSize, sim.State.GridSize, "normal starting field again");
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(0, p.Tier);
                Assert.AreEqual(PlotState.Hard, p.State, "a fresh first layer");
                Assert.AreEqual(0, p.Layer);
                Assert.IsFalse(p.IsGolden);
            }
            Assert.Less(sim.State.Stats.YearLength, 300f, "normal year length again");

            // Next generation: still fully maxed, but the Golden Year never comes back.
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            Assert.IsFalse(sim.State.GoldenYearActive);
        }

        [Test]
        public void Stats_CountHarvestsBySource_Golden_BestCombo_Strikes_AndPlayTime()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            var centre = new GridPos(1, 1);
            sim.DebugNextHarvestGolden();
            Assert.IsTrue(sim.DebugBreak(centre), "the golden seed drops at the break");
            Assert.IsTrue(sim.State.GetPlot(centre).IsGolden);
            sim.DebugForceRipeAll();
            Assert.AreEqual(9, sim.ReapAll());

            var g = sim.State.Generation;
            Assert.AreEqual(9, g.HarvestsHand, "hand harvests");
            Assert.AreEqual(g.Harvests, g.HarvestsHand + g.HarvestsApprentice + g.HarvestsTractor + g.HarvestsLateFrost, "split sums to the total");
            Assert.AreEqual(1, g.GoldenHarvests, "golden harvest counted");
            Assert.AreEqual(9, g.BestCombo);
            Assert.GreaterOrEqual(g.BestCombo, sim.State.Combo);

            // The hoe's counters: one break so far, every plot now at layer 1, one strike on it.
            Assert.AreEqual(1, g.Breaks);
            Assert.AreEqual(0, g.DeepestLayer, "the break was at layer 0");
            Assert.IsTrue(sim.Strike(centre));
            Assert.AreEqual(1, g.Strikes);
            Assert.LessOrEqual(g.Crits, g.Strikes);
            Assert.IsTrue(sim.DebugBreak(centre));
            Assert.AreEqual(2, g.Breaks);
            Assert.AreEqual(1, g.DeepestLayer);

            sim.AddPlayTime(12.5);
            sim.AddPlayTime(-3);
            sim.AddPlayTime(double.NaN);
            Assert.AreEqual(12.5, g.TimePlayedSeconds, 1e-9);
        }

        [Test]
        public void Stats_SurviveARetire()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            var centre = new GridPos(1, 1);
            sim.DebugForceRipeAll();
            Assert.AreEqual(9, sim.ReapAll());
            Assert.IsTrue(sim.Strike(centre));
            Assert.IsTrue(sim.DebugBreak(centre));
            sim.AddPlayTime(30);
            var g = sim.State.Generation;
            int hand = g.HarvestsHand, best = g.BestCombo, strikes = g.Strikes, crits = g.Crits, breaks = g.Breaks, deepest = g.DeepestLayer;
            Assert.AreEqual(1, deepest);
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.Retire());
            Assert.AreEqual(hand, g.HarvestsHand);
            Assert.AreEqual(best, g.BestCombo);
            Assert.AreEqual(strikes, g.Strikes);
            Assert.AreEqual(crits, g.Crits);
            Assert.AreEqual(breaks, g.Breaks);
            Assert.AreEqual(deepest, g.DeepestLayer, "the deepest layer is the family's record, not the field's");
            Assert.AreEqual(30, g.TimePlayedSeconds, 1e-9);
        }
    }
}
