using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Short interruptions (a call, an app switch) resume exactly where the player left: no offline simulation below OfflineMinSeconds.</summary>
    public class OfflineMinTests
    {
        [Test]
        public void ShortInterruption_DoesNotSimulate()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            double coins = sim.State.Coins;
            var r = sim.SimulateOffline(59);
            Assert.AreEqual(0, r.SecondsSimulated);
            Assert.AreEqual(0, r.Harvests);
            Assert.AreEqual(coins, sim.State.Coins);
        }

        [Test]
        public void AtTheMinimum_Simulates()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            Assert.AreEqual(60, sim.SimulateOffline(60).SecondsSimulated, 1e-6);
        }

        [Test]
        public void MinimumDefaultsTo60s_AndIsConfigurable()
        {
            Assert.AreEqual(60, new FarmConfig().OfflineMinSeconds, 1e-9);
            var sim = new FarmSim(new FarmConfig { OfflineMinSeconds = 0 }, 3);
            Assert.AreEqual(10, sim.SimulateOffline(10).SecondsSimulated, 1e-6);
        }
    }
}
