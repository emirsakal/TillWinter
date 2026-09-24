using NUnit.Framework;
using TillWinter.Core;
using static TillWinter.Tests.CoreLoopTests;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.9, feel and accessibility (GDD §10.5–§10.7 v2.5, re-themed v3): the first generation's checklist and the
    /// plan the apprentices follow while the game is closed.
    /// </summary>
    public class ComfortTests
    {
        private static FarmSim Sim() => NewSim(c => c.CrowSpawnChance = 0f);

        [Test]
        public void TheChecklist_FollowsTheFirstYear_StepByStep()
        {
            var sim = Sim();
            Assert.IsTrue(sim.ChecklistActive);
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Strike));
            var centre = new GridPos(1, 1);
            WaitForBeat(sim, false);
            Assert.IsTrue(sim.Strike(centre));
            sim.Tick(0.01f);
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Strike), "a crack is not yet the ground broken");
            Assert.IsTrue(sim.DebugBreak(centre));
            sim.Tick(0.01f);
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Strike));
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Grow));
            Run(sim, 3f); // a carrot ripens in 2.5 s
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Grow));
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Harvest5));
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.Combo3));

            sim.DebugBreakAll();
            Run(sim, 3f);
            Assert.AreEqual(9, sim.ReapAll(), "nine in one swipe");
            sim.Tick(0.01f);
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Harvest5));
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.Combo3));
            Assert.IsFalse(sim.ChecklistDone(ChecklistStep.BuyNode));

            sim.DebugSkipToWinter();
            sim.DebugAddCoins(100);
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            sim.StartNextYear();
            sim.Tick(0.01f);
            Assert.IsTrue(sim.ChecklistDone(ChecklistStep.BuyNode));
            Assert.IsFalse(sim.ChecklistActive, "every step done");
        }

        [Test]
        public void TheChecklist_IsForTheFirstGenerationOnly()
        {
            var sim = Sim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            Assert.IsFalse(sim.ChecklistActive, "the second generation knows the farm");
            Assert.IsFalse(AfterEnding.Daily(20260919).ChecklistActive, "nor is it for the daily farm");
        }

        [Test]
        public void TheAwayPlan_SetsTheRoles_ForTheTimeAwayOnly()
        {
            int OfflineHarvests(AwayPlan plan)
            {
                var sim = Sim();
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

            Assert.AreEqual(0, OfflineHarvests(AwayPlan.AsTheyAre), "two waterers reap nothing");
            Assert.That(OfflineHarvests(AwayPlan.AllHarvest), Is.GreaterThan(0));
            Assert.That(OfflineHarvests(AwayPlan.Balanced), Is.GreaterThan(0), "the first picks, the second waters");
        }
    }
}
