using System;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>GDD §3 v1.5: the player may end a running year early instead of waiting the clock out.</summary>
    public class EndYearTests
    {
        private const float Dt = 0.01f;

        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = TestConfig.Classic();
            cfg.BaseCritChance = 0;
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt);
        }

        [Test]
        public void EndYearNow_DuringAYear_EntersWinterImmediately()
        {
            var sim = NewSim();
            Run(sim, 5f);
            Assert.AreEqual(Phase.Year, sim.State.Phase);
            Assert.Greater(sim.State.SecondsUntilWinter, 0f, "the year is still running");

            sim.EndYearNow();

            Assert.AreEqual(Phase.Winter, sim.State.Phase);
            Assert.IsTrue(sim.State.IsWinter);
            Assert.AreEqual(0f, sim.State.SecondsUntilWinter, 1e-3f, "the clock is spent, as if it had run out");
        }

        [Test]
        public void EndYearNow_OutsideAYear_DoesNothing()
        {
            var sim = NewSim();
            sim.EndYearNow();
            Assert.AreEqual(Phase.Winter, sim.State.Phase);

            // Already in Winter: a second call must not re-enter it or disturb the year counter.
            int year = sim.State.Year;
            sim.EndYearNow();
            Assert.AreEqual(Phase.Winter, sim.State.Phase);
            Assert.AreEqual(year, sim.State.Year);
        }

        [Test]
        public void EndYearNow_ThenNextYear_ContinuesNormally()
        {
            var sim = NewSim();
            Run(sim, 3f);
            sim.EndYearNow();
            int year = sim.State.Year;

            sim.StartNextYear();

            Assert.AreEqual(Phase.Year, sim.State.Phase);
            Assert.AreEqual(year + 1, sim.State.Year);
            Assert.AreEqual(Season.Spring, sim.State.Season);
        }

        [Test]
        public void EndYearNow_ResolvesTheStandingFieldJustAsFrostWould()
        {
            var sim = NewSim(c => c.Crops[0].Grow = 1000f);
            var ripe = sim.State.GetPlot(0, 0);
            var growing = sim.State.GetPlot(1, 1);
            var cracked = sim.State.GetPlot(2, 2);
            sim.DebugBreak(ripe.Pos);
            sim.DebugForceRipe(ripe.Pos);
            sim.DebugBreak(growing.Pos);
            Run(sim, 1f);
            Assert.IsTrue(growing.IsGrowing);
            float progress = growing.Progress;
            CoreLoopTests.WaitForBeat(sim, false);
            Assert.IsTrue(sim.Strike(cracked.Pos));
            Assert.AreEqual(cracked.MaxHp - 3, cracked.Hp, 1e-9);
            double coins = sim.State.Coins;

            sim.EndYearNow();

            Assert.AreEqual(PlotState.Hard, ripe.State, "the frost reaped it");
            Assert.AreEqual(1, ripe.Layer, "and the ground came back a layer deeper");
            Assert.AreEqual(coins + 1, sim.State.Coins, 1e-9, "one carrot at the plain price");
            Assert.AreEqual(PlotState.Growing, growing.State, "a growing crop waits for spring");
            Assert.AreEqual(progress, growing.Progress, 1e-6f);
            Assert.AreEqual(0, cracked.Layer, "hard ground keeps its layer");
            Assert.AreEqual(cracked.MaxHp, cracked.Hp, 1e-9, "and the winter closes its cracks");
        }
    }
}
