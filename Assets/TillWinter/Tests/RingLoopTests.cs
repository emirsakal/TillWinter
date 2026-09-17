using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.1, the ring and the loop (GDD §2.1/§2.5 v1.6): crops lose value when left standing, a long combo pays a
    /// milestone bonus, a moving ring works faster, a tap can finish a Ripe plot, and the ring has shapes.
    /// </summary>
    public class RingLoopTests
    {
        private static FarmSim Sim(FarmConfig cfg = null) => new FarmSim(cfg ?? new FarmConfig(), 5);

        private static void RipenAll(FarmSim sim)
        {
            sim.DebugForceRipeAll();
        }

        [Test]
        public void ARipePlot_KeepsFullValueDuringTheGrace_ThenLosesItDownToTheFloor()
        {
            var sim = Sim();
            RipenAll(sim);
            var plot = sim.State.GetPlot(0, 0);
            Assert.AreEqual(1.0, sim.Freshness(plot), 1e-6, "fresh at once");

            // Stand for the whole grace: still full value.
            for (float t = 0; t < sim.Config.RipeGraceSeconds - 0.5f; t += 0.5f) sim.Tick(0.5f, null);
            Assert.AreEqual(1.0, sim.Freshness(plot), 1e-6, "inside the grace");

            for (float t = 0; t < sim.Config.OverripeDecaySeconds * 0.5f; t += 0.5f) sim.Tick(0.5f, null);
            double half = sim.Freshness(plot);
            Assert.That(half, Is.LessThan(1.0).And.GreaterThan(sim.Config.OverripeMinValue), "falling: " + half);

            for (float t = 0; t < sim.Config.OverripeDecaySeconds; t += 0.5f) sim.Tick(0.5f, null);
            Assert.AreEqual(sim.Config.OverripeMinValue, sim.Freshness(plot), 1e-6, "never below the floor");
        }

        [Test]
        public void AnOverripeCrop_PaysLessThanAFreshOne()
        {
            var fresh = Sim();
            RipenAll(fresh);
            fresh.DebugSetLevel("tap_harvest", 1);
            double before = fresh.State.Coins;
            fresh.TapAt(new GridPos(0, 0));
            double freshCoins = fresh.State.Coins - before;

            var stale = Sim();
            RipenAll(stale);
            stale.DebugSetLevel("tap_harvest", 1);
            for (float t = 0; t < stale.Config.RipeGraceSeconds + stale.Config.OverripeDecaySeconds; t += 0.5f) stale.Tick(0.5f, null);
            before = stale.State.Coins;
            stale.TapAt(new GridPos(0, 0));
            double staleCoins = stale.State.Coins - before;

            Assert.That(staleCoins, Is.LessThan(freshCoins));
            Assert.AreEqual(freshCoins * stale.Config.OverripeMinValue, staleCoins, 1e-6);
        }

        [Test]
        public void RipeAge_DoesNotRun_WhileOffline()
        {
            var sim = Sim();
            RipenAll(sim);
            var plot = sim.State.GetPlot(0, 0);
            sim.SimulateOffline(600);
            Assert.AreEqual(1.0, sim.Freshness(plot), 1e-6, "a closed game must not spoil the crop");
        }

        [Test]
        public void TapHarvest_NeedsTheNode_AndThenRespectsItsCooldown()
        {
            var sim = Sim();
            RipenAll(sim);
            Assert.IsFalse(sim.TapAt(new GridPos(0, 0)), "without the node a tap on a ripe plot does nothing");

            sim.DebugSetLevel("tap_harvest", 1);
            Assert.IsTrue(sim.TapAt(new GridPos(0, 0)));
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(0, 0).State, "harvested and replanted");
            Assert.IsFalse(sim.TapAt(new GridPos(1, 0)), "still cooling down");

            for (float t = 0; t < sim.Config.TapHarvestCooldownByLevel[1] + 0.1f; t += 0.1f) sim.Tick(0.1f, null);
            Assert.IsTrue(sim.TapAt(new GridPos(1, 0)), "ready again");
        }

        [Test]
        public void ComboMilestone_PaysABonus_Once()
        {
            var sim = Sim();
            sim.DebugSetLevel("tap_harvest", 2);
            int fired = 0;
            double paid = 0;
            sim.ComboMilestone += (combo, coins) => { fired++; paid += coins; Assert.AreEqual(sim.Config.ComboMilestones[0], combo); };
            for (int i = 0; i < sim.Config.ComboMilestones[0]; i++)
            {
                RipenAll(sim);
                sim.DebugClearTapCooldown();
                Assert.IsTrue(sim.TapAt(new GridPos(i % 3, (i / 3) % 3)), "harvest " + i);
                sim.Tick(0.05f, null); // inside the combo window
            }
            Assert.AreEqual(1, fired, "the milestone pays once");
            Assert.That(paid, Is.GreaterThan(0));
        }

        [Test]
        public void ACrowEating_BreaksTheCombo()
        {
            var sim = Sim();
            sim.DebugSetLevel("tap_harvest", 2);
            RipenAll(sim);
            sim.TapAt(new GridPos(0, 0));
            Assert.AreEqual(1, sim.State.Combo);

            RipenAll(sim);
            Assert.IsTrue(sim.DebugSpawnCrow());
            for (float t = 0; t < sim.Config.CrowEatTime + 0.2f; t += 0.1f) sim.Tick(0.1f, null);
            Assert.AreEqual(0, sim.State.Combo, "a stolen crop ends the streak");
        }

        [Test]
        public void AMovingRing_WorksFasterThanAStillOne()
        {
            var still = Sim();
            var moving = Sim();
            float x = 1f;
            for (int i = 0; i < 40; i++)
            {
                still.Tick(0.05f, new RingInput(1f, 1f));
                x += (i % 2 == 0 ? 1f : -1f) * 0.3f; // a finger that keeps sweeping over the same plot
                moving.Tick(0.05f, new RingInput(x, 1f));
            }
            Assert.That(moving.State.Flow, Is.GreaterThan(0.5f));
            Assert.That(still.State.Flow, Is.LessThan(0.1f));
        }

        [Test]
        public void RingShapes_NeedTheNode_AndChangeWhichPlotsAreUnderTheRing()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            sim.DebugSetLevel("expand_field", 2); // 5x5, so a shape can reach further than a round ring
            Assert.IsFalse(sim.SetRingShape(RingShape.Rake), "the rake needs the node");

            sim.DebugSetLevel("ring_shape", 1);
            Assert.IsTrue(sim.SetRingShape(RingShape.Rake));
            Assert.IsFalse(sim.SetRingShape(RingShape.Cross), "the cross needs level 2");
            sim.DebugSetLevel("ring_shape", 2);
            Assert.IsTrue(sim.SetRingShape(RingShape.Cross));

            var s = sim.State;
            sim.SetRingShape(RingShape.Round);
            sim.Tick(0.001f, new RingInput(2f, 2f));
            bool roundReachesSide = s.IsUnderRing(new GridPos(3, 2));
            sim.SetRingShape(RingShape.Rake);
            sim.Tick(0.001f, new RingInput(2f, 2f));
            bool rakeReachesSide = s.IsUnderRing(new GridPos(3, 2));
            bool rakeReachesUp = s.IsUnderRing(new GridPos(2, 3));
            Assert.IsFalse(roundReachesSide, "a 0.7 radius ring covers one plot");
            Assert.IsTrue(rakeReachesSide, "the rake is wide");
            Assert.IsFalse(rakeReachesUp, "and thin");

            sim.SetRingShape(RingShape.Cross);
            sim.Tick(0.001f, new RingInput(2f, 2f));
            Assert.IsTrue(s.IsUnderRing(new GridPos(3, 2)) && s.IsUnderRing(new GridPos(2, 3)), "the cross reaches both ways");
        }
    }
}
