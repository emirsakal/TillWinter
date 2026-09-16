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
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        [Test]
        public void EndYearNow_DuringAYear_EntersWinterImmediately()
        {
            var sim = NewSim();
            Run(sim, 5f, null);
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
            Run(sim, 3f, null);
            sim.EndYearNow();
            int year = sim.State.Year;

            sim.StartNextYear();

            Assert.AreEqual(Phase.Year, sim.State.Phase);
            Assert.AreEqual(year + 1, sim.State.Year);
            Assert.AreEqual(Season.Spring, sim.State.Season);
        }

        [Test]
        public void EndYearNow_LosesTheStandingCropJustAsFrostWould()
        {
            var sim = NewSim();
            Run(sim, 4f, new RingInput(1, 1));
            sim.EndYearNow();
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Dry, p.State);
                Assert.AreEqual(0f, p.Progress, 1e-4f);
            }
        }
    }
}
