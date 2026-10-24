using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>v3.7 fixes from the full test pass: helpers while away, and a respec after a Heritage start field.</summary>
    public class HelpersAwayTests
    {
        private static FarmSim NewSim(int seed = 1) => CoreLoopTests.NewSim(null, seed);

        private static FarmSim WithHelpers(int count)
        {
            var sim = NewSim();
            sim.DebugAddCoins(1e9);
            sim.DebugSkipToWinter();
            for (int i = 0; i < count; i++) Assert.IsTrue(sim.TryBuy("apprentice_count"), "apprentice_count " + (i + 1));
            sim.StartNextYear();
            Assert.AreEqual(count, sim.State.Apprentices.Count);
            return sim;
        }

        private static int HardPlots(FarmSim sim)
        {
            int n = 0;
            foreach (var p in sim.State.Plots) if (p.IsHard) n++;
            return n;
        }

        [Test]
        public void Away_PickersWithNothingRipe_DigTheHardGround_AndTheFieldPays()
        {
            var sim = WithHelpers(2);
            foreach (var a in sim.State.Apprentices) Assert.AreEqual(ApprenticeRole.Picker, a.Role, "new helpers pick");
            Assert.AreEqual(sim.State.Plots.Count, HardPlots(sim), "a fresh spring is all hard ground");

            var report = sim.SimulateOffline(3600);

            Assert.Greater(report.Harvests, 0, "an hour away on hard ground earns something: idle pickers dug, the crops grew, they picked them");
            foreach (var a in sim.State.Apprentices) Assert.AreEqual(ApprenticeRole.Picker, a.Role, "their own role is untouched");
        }

        [Test]
        public void InPlay_APickerNeverDigs()
        {
            var sim = WithHelpers(2);
            double hp = 0;
            foreach (var p in sim.State.Plots) hp += p.Hp;
            CoreLoopTests.Run(sim, 20f, 0.05f);
            double after = 0;
            foreach (var p in sim.State.Plots) after += p.Hp;
            Assert.AreEqual(sim.State.Plots.Count, HardPlots(sim), "with the player here, only diggers dig");
            Assert.AreEqual(hp, after, 1e-9);
        }

        [Test]
        public void Respec_AfterAHeritageStartFieldBoughtInWinter_GoesBackToTheFieldThisGenerationStartedOn()
        {
            var sim = NewSim();
            int start = sim.State.GridSize;
            sim.DebugAddCoins(1e9);
            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.AreEqual(start + 2, sim.State.GridSize);
            sim.DebugAddSeeds(100000);
            Assert.IsTrue(sim.TryBuy("h_start_field"), "Heritage can be bought from banked seeds in a winter");
            Assert.AreEqual(start + 2, sim.State.GridSize, "a start bonus waits for the next generation");

            Assert.IsTrue(sim.RespecAlmanac());

            Assert.AreEqual(start, sim.State.GridSize, "the respec takes the field back to where this generation began, not to next generation's start");
            int guard = 0;
            while (sim.CanBuy("expand_field") && guard++ < 10) sim.TryBuy("expand_field");
            Assert.AreEqual(sim.Config.MaxGridSize, sim.State.GridSize, "and it can still grow to the full field");
        }
    }
}
