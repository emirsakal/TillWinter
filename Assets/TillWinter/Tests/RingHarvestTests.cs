using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>GDD §2.1 (v1.4): the ring waters and grows every plot under it but harvests one Ripe plot at a time.</summary>
    public class RingHarvestTests
    {
        [Test]
        public void Ring_HarvestsOnePlotAtATime()
        {
            var sim = new FarmSim(new FarmConfig(), 1);
            sim.DebugSetRingRadiusOverride(2.5f);
            sim.DebugForceRipeAll();
            var ring = new RingInput(1f, 1f); // covers the whole 3x3 field
            sim.Tick(0.05f, ring);
            int inProgress = 0;
            foreach (var p in sim.State.Plots) if (p.IsRipe && p.Progress > 0f) inProgress++;
            Assert.AreEqual(1, inProgress, "only one Ripe plot makes harvest progress");
        }

        [Test]
        public void Ring_HarvestThroughput_IsOnePlotPerHarvestTime()
        {
            var cfg = new FarmConfig();
            var sim = new FarmSim(cfg, 1);
            sim.DebugSetRingRadiusOverride(2.5f);
            sim.DebugForceRipeAll();
            int harvests = 0;
            sim.Harvested += e => { if (e.Source == HarvestSource.Ring) harvests++; };
            float harvestTime = cfg.Crops[0].Harvest / sim.State.Stats.RingHarvestMult;
            float t = harvestTime * 3.5f;
            for (float x = 0; x < t; x += 0.01f) sim.Tick(0.01f, new RingInput(1f, 1f));
            Assert.AreEqual(3, harvests, "three full harvest times, three harvests");
        }

        [Test]
        public void Ring_StillWatersAndGrowsEveryPlotUnderIt()
        {
            var sim = new FarmSim(new FarmConfig(), 1);
            sim.DebugSetRingRadiusOverride(2.5f);
            sim.Tick(0.1f, new RingInput(1f, 1f));
            foreach (var p in sim.State.Plots)
                Assert.IsTrue(p.State != PlotState.Dry || p.Progress > 0f, "every plot under the ring is watered in parallel");
        }
    }
}
