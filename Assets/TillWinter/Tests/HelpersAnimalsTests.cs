using NUnit.Framework;
using TillWinter.Core;
using static TillWinter.Tests.CoreLoopTests;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.4, helpers and animals (GDD §4/§5.1 v2.0, re-themed v3): placed scarecrows guard an area, apprentices take
    /// roles, the farm dog chases crows, and the tractor can be sent by hand. (The beehive's columns and the digger
    /// are in <see cref="CoreLoopTests"/>.)
    /// </summary>
    public class HelpersAnimalsTests
    {
        private static FarmSim SecondYear(System.Action<FarmConfig> tweak, int seed = 2)
        {
            var sim = NewSim(tweak, seed);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            return sim;
        }

        [Test]
        public void EachScarecrowLevel_PlacesOne_WhereItGuardsTheMost()
        {
            var sim = NewSim();
            Assert.AreEqual(0, sim.State.Scarecrows.Count);
            sim.DebugSetLevel("scarecrow", 1);
            Assert.AreEqual(1, sim.State.Scarecrows.Count);
            int guarded = 0;
            foreach (var p in sim.State.Plots) if (sim.IsGuarded(p.Pos)) guarded++;
            Assert.AreEqual(8, guarded, "one scarecrow on a 3x3 field leaves one corner plot open");
            sim.DebugSetLevel("scarecrow", 2);
            Assert.AreEqual(2, sim.State.Scarecrows.Count);
            Assert.AreNotEqual(sim.State.Scarecrows[0], sim.State.Scarecrows[1]);
            foreach (var p in sim.State.Plots) Assert.IsTrue(sim.IsGuarded(p.Pos), "two cover it all");
        }

        [Test]
        public void CrowsNeverLand_OnAGuardedPlot()
        {
            var sim = SecondYear(c => c.CrowSpawnChance = 1f);
            sim.DebugSetLevel("scarecrow", 1);
            Assert.IsTrue(sim.MoveScarecrow(0, new GridPos(0, 0)), "to the bottom-left corner");
            int landed = 0;
            sim.CrowLanded += e =>
            {
                landed++;
                Assert.IsFalse(sim.IsGuarded(e.Pos), "a crow landed on " + e.Pos);
            };
            for (int i = 0; i < 10; i++)
            {
                sim.DebugForceRipeAll();
                Run(sim, 4f); // a crop must have stood ripe CrowMinRipe (3 s) before a crow comes
            }
            Assert.That(landed, Is.GreaterThan(0), "the unguarded plots still draw crows");
        }

        [Test]
        public void MovingAScarecrow_StaysOnTheFieldsCorners()
        {
            var sim = NewSim();
            sim.DebugSetLevel("scarecrow", 2);
            Assert.IsTrue(sim.MoveScarecrow(0, new GridPos(3, 3)), "the far corner of a 3x3 field");
            Assert.AreEqual(new GridPos(3, 3), sim.State.Scarecrows[0]);
            Assert.IsFalse(sim.MoveScarecrow(0, new GridPos(4, 0)), "off the field");
            Assert.IsFalse(sim.MoveScarecrow(1, new GridPos(3, 3)), "not onto another scarecrow");
            Assert.IsFalse(sim.MoveScarecrow(2, new GridPos(0, 0)), "there is no third");
        }

        [Test]
        public void TheFarmDog_ChasesASettledCrow_WithoutABounty_ThenRests()
        {
            var sim = SecondYear(c => c.CrowSpawnChance = 0f); // only the crows this test places
            sim.DebugSetLevel("farm_dog", 1);
            int chased = 0;
            sim.DogChased += _ => chased++;
            Assert.IsTrue(sim.DebugSpawnCrow());
            double coins = sim.State.Coins;
            Run(sim, sim.Config.DogReactSeconds + 0.2f);
            Assert.AreEqual(1, chased);
            Assert.AreEqual(0, sim.State.Crows.Count);
            Assert.AreEqual(coins, sim.State.Coins, 1e-9, "the bounty is the tap's");

            Assert.IsTrue(sim.DebugSpawnCrow());
            Run(sim, sim.Config.DogReactSeconds + 0.5f);
            Assert.AreEqual(1, sim.State.Crows.Count, "still resting");
        }

        [Test]
        public void AWaterer_VisitsGrowingCrops_AndAPickerDoesNot()
        {
            int Watered(ApprenticeRole role)
            {
                var sim = NewSim(c => c.Crops[0].Grow = 1000f); // nothing ripens by itself
                sim.DebugSetLevel("apprentice_count", 1);
                Assert.IsTrue(role == ApprenticeRole.Picker || sim.SetApprenticeRole(0, role));
                sim.DebugBreakAll();
                int n = 0;
                sim.PlotWatered += _ => n++;
                Run(sim, 8f);
                return n;
            }

            Assert.AreEqual(0, Watered(ApprenticeRole.Picker), "nothing ripe: nothing to do");
            Assert.That(Watered(ApprenticeRole.Waterer), Is.GreaterThan(1));
        }

        [Test]
        public void TheTractor_CanBeSentByHand_OnceHalfCharged()
        {
            var sim = NewSim();
            sim.DebugSetLevel("tractor", 1);
            sim.DebugForceRipeAll();
            Assert.IsFalse(sim.TriggerTractor(), "just bought: not charged");
            float interval = sim.Config.TractorIntervalByLevel[1];
            Run(sim, interval * sim.Config.TractorManualReady + 0.2f);
            sim.DebugForceRipeAll();
            Assert.That(sim.TractorCharge, Is.GreaterThanOrEqualTo(sim.Config.TractorManualReady));
            Assert.IsTrue(sim.TriggerTractor());
            Assert.IsTrue(sim.State.Tractor.Sweeping);
            Assert.IsFalse(sim.TriggerTractor(), "already on its way");
        }
    }
}
