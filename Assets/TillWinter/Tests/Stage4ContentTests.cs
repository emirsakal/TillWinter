using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>The content half of the v2.8 pass: what happened while the player was away is reported, not just applied.</summary>
    public class Stage4ContentTests
    {
        [Test]
        public void Offline_ReportsHeirloomsFoundWhileAway()
        {
            var cfg = TestConfig.Classic();
            cfg.CrowSpawnChance = 0f;
            var sim = new FarmSim(cfg, 1);
            sim.DebugSetLevel("apprentice_count", 1);
            sim.DebugForceRipeAll();
            Assert.AreEqual(0, sim.State.Generation.Achievements, "nothing found yet");
            var report = sim.SimulateOffline(600);
            Assert.That(report.HarvestsApprentice, Is.GreaterThan(0), "the apprentice picked while away");
            Assert.That(sim.State.Generation.Achievements, Is.Not.EqualTo(0), "the first harvest is an achievement");
            Assert.AreEqual(1, report.HeirloomsFound, "and the card is told about it");

            // Already found: a second stretch away reports nothing new.
            sim.DebugForceRipeAll();
            Assert.AreEqual(0, sim.SimulateOffline(600).HeirloomsFound);
        }
    }
}
