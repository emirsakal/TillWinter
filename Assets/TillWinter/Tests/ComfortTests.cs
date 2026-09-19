using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.9, feel and accessibility (GDD §10.5–§10.7 v2.5): the first generation's checklist, the hands-free ring, and
    /// the plan the apprentices follow while the game is closed.
    /// </summary>
    public class ComfortTests
    {
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.CrowSpawnChance = 0f;
            cfg.WeatherFirstYear = cfg.GoalFirstYear = int.MaxValue;
            cfg.PestFirstYear = cfg.LuckyFirstYear = cfg.TraderFirstYear = int.MaxValue;
            return cfg;
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring)
        {
            for (float t = 0f; t < seconds; t += 0.05f) sim.Tick(0.05f, ring);
        }

        [Test]
        public void TheChecklist_FollowsTheFirstYear_StepByStep()
        {
            var sim = new FarmSim(Cfg(), 1);
            Assert.IsTrue(sim.ChecklistActive);
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Water));
            var plot = sim.State.GetPlot(1, 1);
            while (plot.State == PlotState.Dry) sim.Tick(0.05f, new RingInput(1f, 1f));
            sim.Tick(0.05f, null);
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Water));
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Grow));
            Run(sim, 20f, new RingInput(1f, 1f));
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Grow));
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Harvest5), "twenty seconds of ring on one carrot bed: a harvest every three");

            sim.DebugSkipToWinter();
            sim.DebugAddCoins(100);
            Assert.IsTrue(sim.TryBuy("ring_radius"));
            sim.StartNextYear();
            sim.Tick(0.05f, null);
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.BuyNode));
        }

        [Test]
        public void TheChecklist_IsForTheFirstGenerationOnly()
        {
            var cfg = Cfg();
            var sim = new FarmSim(cfg, 1);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            Assert.IsFalse(sim.ChecklistActive, "the second generation knows the farm");
            Assert.IsFalse(AfterEnding.Daily(20260919).ChecklistActive, "nor is it for the daily farm");
        }

        [Test]
        public void TheHandsFreeRing_StaysNearHome_AndHarvests()
        {
            var cfg = Cfg();
            cfg.StartGridSize = 3;
            var sim = new FarmSim(cfg, 1);
            sim.DebugSetLevel("expand_field", 2);
            var auto = new AutoRing();
            Assert.IsNull(auto.Step(sim.State, 0.05f), "nothing until a home is placed");
            auto.Place(1f, 1f);
            sim.DebugForceRipeAll();
            int harvested = 0;
            sim.Harvested += _ => harvested++;
            for (int i = 0; i < 300; i++)
            {
                var ring = auto.Step(sim.State, 0.05f);
                Assert.IsTrue(ring.HasValue);
                float dx = ring.Value.X - 1f, dy = ring.Value.Y - 1f;
                Assert.That(dx * dx + dy * dy, Is.LessThanOrEqualTo(auto.Reach * auto.Reach + 1e-3f), "within reach of home");
                sim.Tick(0.05f, ring);
            }
            Assert.That(harvested, Is.GreaterThan(3));
            foreach (var p in sim.State.Plots)
                if (p.Pos.X >= 4 || p.Pos.Y >= 4) Assert.AreEqual(PlotState.Ripe, p.State, "out of reach, still waiting: " + p.Pos);
        }

        [Test]
        public void TheAwayPlan_SetsTheRoles_ForTheTimeAwayOnly()
        {
            int OfflineHarvests(AwayPlan plan)
            {
                var sim = new FarmSim(Cfg(), 1);
                sim.DebugSetLevel("apprentice_count", 2);
                Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Waterer));
                Assert.IsTrue(sim.SetApprenticeRole(1, ApprenticeRole.Waterer));
                sim.SetAwayPlan(plan);
                sim.DebugForceRipeAll();
                var report = sim.SimulateOffline(300);
                Assert.AreEqual(ApprenticeRole.Waterer, sim.State.Apprentices[0].Role, "the player's roles come back");
                Assert.AreEqual(ApprenticeRole.Waterer, sim.State.Apprentices[1].Role);
                return report.Harvests;
            }

            Assert.AreEqual(0, OfflineHarvests(AwayPlan.AsTheyAre), "two waterers harvest nothing");
            Assert.That(OfflineHarvests(AwayPlan.AllHarvest), Is.GreaterThan(0));
            Assert.That(OfflineHarvests(AwayPlan.Balanced), Is.GreaterThan(0));
        }
    }
}
