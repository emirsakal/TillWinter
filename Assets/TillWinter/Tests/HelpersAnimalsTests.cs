using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.4, helpers and animals (GDD §4/§5.1 v2.0): placed scarecrows guard an area, apprentices can water, the farm
    /// dog chases crows, the beehive speeds the Sun by the sunflowers, and the tractor can be sent by hand.
    /// </summary>
    public class HelpersAnimalsTests
    {
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.WeatherFirstYear = int.MaxValue; // fog and storms keep crows away; the rules here need them
            cfg.GoalFirstYear = int.MaxValue;
            cfg.StonyChance = cfg.FertileChance = 0;
            return cfg;
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring = null)
        {
            for (float t = 0f; t < seconds; t += 0.05f) sim.Tick(0.05f, ring);
        }

        private static FarmSim SecondYear(FarmConfig cfg, int seed = 2)
        {
            var sim = new FarmSim(cfg, seed);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            return sim;
        }

        [Test]
        public void EachScarecrowLevel_PlacesOne_WhereItGuardsTheMost()
        {
            var sim = new FarmSim(Cfg(), 1);
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
            var cfg = Cfg();
            cfg.CrowSpawnChance = 1f;
            var sim = SecondYear(cfg);
            sim.DebugSetLevel("scarecrow", 1);
            Assert.IsTrue(sim.MoveScarecrow(0, new GridPos(0, 0)), "to the bottom-left corner");
            int landed = 0;
            sim.CrowLanded += e =>
            {
                landed++;
                Assert.IsFalse(sim.IsGuarded(e.Pos), "a crow landed on " + e.Pos);
            };
            for (int i = 0; i < 20; i++)
            {
                sim.DebugForceRipeAll();
                Run(sim, 2f);
            }
            Assert.That(landed, Is.GreaterThan(0), "the unguarded plots still draw crows");
        }

        [Test]
        public void MovingAScarecrow_StaysOnTheFieldsCorners()
        {
            var sim = new FarmSim(Cfg(), 1);
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
            var cfg = Cfg();
            cfg.CrowSpawnChance = 0f; // only the crows this test places
            var sim = SecondYear(cfg);
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
        public void TheBeehive_SpeedsTheSun_OnlyByTheSunflowers()
        {
            float Grown(bool hive, GridPos at)
            {
                var sim = new FarmSim(Cfg(), 1);
                sim.DebugSetLevel("sun", 1);
                if (hive) sim.DebugSetLevel("beehive", 1);
                var plot = sim.State.GetPlot(at);
                while (plot.State == PlotState.Dry) sim.Tick(0.05f, new RingInput(at.X, at.Y));
                float before = plot.Progress;
                Run(sim, 0.5f);
                return plot.Progress - before;
            }

            var edge = new GridPos(2, 0);
            var far = new GridPos(0, 0);
            Assert.AreEqual(Grown(false, edge) * new FarmConfig().BeeSunBoost, Grown(true, edge), 1e-3);
            Assert.AreEqual(Grown(false, far), Grown(true, far), 1e-5, "the left side is out of the bees' reach");
        }

        [Test]
        public void AWaterer_WatersDryPlots_AndAHarvesterDoesNot()
        {
            int Watered(ApprenticeRole role)
            {
                var sim = new FarmSim(Cfg(), 1);
                sim.DebugSetLevel("apprentice_count", 1);
                Assert.IsTrue(role == ApprenticeRole.Harvester || sim.SetApprenticeRole(0, role));
                int n = 0;
                sim.PlotWatered += _ => n++;
                Run(sim, 8f);
                return n;
            }

            Assert.AreEqual(0, Watered(ApprenticeRole.Harvester), "no irrigation, no ring, nothing ripe: nothing to do");
            Assert.That(Watered(ApprenticeRole.Waterer), Is.GreaterThan(1));
        }

        [Test]
        public void TheTractor_CanBeSentByHand_OnceHalfCharged()
        {
            var sim = new FarmSim(Cfg(), 1);
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
