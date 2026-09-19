using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.5, events and threats (GDD §5.5–§5.7 v2.1): the mole, the rabbit and the locusts, the hens that eat them,
    /// the clover, the golden egg and the shooting star, and the travelling trader.
    /// </summary>
    public class EventsThreatsTests
    {
        /// <summary>Only what a test puts on the field: no crows, weather, goals, special ground or random events.</summary>
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.CrowSpawnChance = 0f;
            cfg.WeatherFirstYear = cfg.GoalFirstYear = int.MaxValue;
            cfg.PestFirstYear = cfg.LuckyFirstYear = cfg.TraderFirstYear = int.MaxValue;
            cfg.StonyChance = cfg.FertileChance = 0;
            cfg.InSeasonValue = 1;
            return cfg;
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring = null)
        {
            for (float t = 0f; t < seconds; t += 0.05f) sim.Tick(0.05f, ring);
        }

        [Test]
        public void AMole_DigsItsPlotUp_UnlessBonked()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            var at = new GridPos(1, 1);
            int struck = 0;
            sim.PestStruck += (k, p) => { struck++; Assert.AreEqual(PestKind.Mole, k); };
            sim.DebugSpawnPest(PestKind.Mole, at);
            Run(sim, sim.Config.MoleDigSeconds + 0.1f);
            Assert.AreEqual(1, struck);
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(at).State, "dug up");
            Assert.AreEqual(1, sim.State.CropsLostThisYear, "a ripe crop lost counts against the grade");

            var tapped = new FarmSim(Cfg(), 1);
            tapped.DebugSpawnPest(PestKind.Mole, at);
            double before = tapped.State.Coins;
            Assert.IsTrue(tapped.TapAt(at));
            Assert.AreEqual(PestKind.None, tapped.State.Pest.Kind);
            Assert.That(tapped.State.Coins, Is.GreaterThan(before), "a bonked mole drops a crop's worth");
        }

        [Test]
        public void ARabbit_EatsACarrot_UnlessTheRingSendsItOff()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            var at = new GridPos(2, 2);
            sim.DebugSpawnPest(PestKind.Rabbit, at);
            Run(sim, sim.Config.RabbitEatSeconds + 0.1f);
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(at).State, "eaten");

            var chased = new FarmSim(Cfg(), 1);
            chased.DebugForceRipeAll();
            chased.DebugSpawnPest(PestKind.Rabbit, at);
            int scared = 0;
            chased.PestScared += (k, p, c) => scared++;
            chased.Tick(0.05f, new RingInput(at.X, at.Y));
            Assert.AreEqual(1, scared);
            Assert.AreEqual(PestKind.None, chased.State.Pest.Kind);
        }

        [Test]
        public void Locusts_StopGrowthInTheirPatch_AndTheRingDrivesThemOff()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("sun", 3);
            var edge = sim.State.GetPlot(2, 2);
            while (edge.State == PlotState.Dry) sim.Tick(0.05f, new RingInput(2f, 2f));
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(1, 1)); // 3x3 around the centre: the whole field
            Assert.IsTrue(sim.InLocusts(edge.Pos));
            float grown = edge.Progress;
            Run(sim, 1f);
            Assert.AreEqual(grown, edge.Progress, 1e-6, "nothing grows under the swarm");

            Run(sim, sim.Config.LocustShooSeconds + 0.1f, new RingInput(0f, 0f));
            Assert.AreEqual(PestKind.None, sim.State.Pest.Kind, "the ring worked over the swarm drove it off");
        }

        [Test]
        public void LeftAlone_LocustsStripTheRipeCropsOfTheirPatch()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(0, 0));
            Run(sim, sim.Config.LocustSeconds + 0.1f);
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(1, 1).State, "inside the patch");
            Assert.AreEqual(PlotState.Ripe, sim.State.GetPlot(2, 2).State, "outside it");
        }

        [Test]
        public void TheHens_EatALingeringPest()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("hens", 1);
            int ate = 0;
            sim.HensAte += _ => ate++;
            sim.DebugSpawnPest(PestKind.Mole, new GridPos(0, 0));
            Run(sim, sim.Config.HenReactSeconds + 0.1f);
            Assert.AreEqual(1, ate);
            Assert.AreEqual(PestKind.None, sim.State.Pest.Kind);
            Assert.That(sim.State.HenCooldown, Is.GreaterThan(0f));
        }

        [Test]
        public void Pests_ComeFromTheThirdYear()
        {
            var cfg = Cfg();
            cfg.PestFirstYear = 3;
            cfg.PestChance = 1;
            var sim = new FarmSim(cfg, 3);
            int arrived = 0;
            sim.PestArrived += (k, p) => arrived++;
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Run(sim, 40f);
            Assert.AreEqual(0, arrived, "year 2");
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Run(sim, 40f);
            Assert.That(arrived, Is.GreaterThan(0), "year 3");
        }

        [Test]
        public void AClover_PaysWhenTheRingSweepsOverIt()
        {
            var sim = new FarmSim(Cfg(), 1);
            var at = new GridPos(2, 0);
            sim.DebugSpawnLucky(LuckyKind.Clover, at);
            double paid = 0;
            sim.LuckyFound += (k, c) => paid = c;
            Run(sim, 0.5f);
            Assert.AreEqual(0, paid, 1e-9, "not yet found");
            sim.Tick(0.05f, new RingInput(at.X, at.Y));
            Assert.That(paid, Is.GreaterThan(0));
            Assert.AreEqual(0f, sim.State.Luck.CloverLeft);
        }

        [Test]
        public void CatchingTheShootingStar_MakesHarvestsPayDouble()
        {
            var plain = new FarmSim(Cfg(), 1);
            var lucky = new FarmSim(Cfg(), 1);
            foreach (var s in new[] { plain, lucky })
            {
                s.DebugSetLevel("tap_harvest", 2);
                s.DebugForceRipeAll();
            }
            Assert.IsFalse(lucky.TapStar(), "no star in the sky");
            lucky.DebugSpawnLucky(LuckyKind.ShootingStar, new GridPos(0, 0));
            Assert.IsTrue(lucky.TapStar());
            double a = plain.State.Coins, b = lucky.State.Coins;
            Assert.IsTrue(plain.TapAt(new GridPos(0, 0)));
            Assert.IsTrue(lucky.TapAt(new GridPos(0, 0)));
            Assert.AreEqual((plain.State.Coins - a) * lucky.Config.StarRushValue, lucky.State.Coins - b, 1e-9);
        }

        [Test]
        public void TheTrader_SellsASeedAndRareSeed_OnceEach()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugBringTrader();
            var t = sim.State.Trader;
            Assert.IsTrue(t.Active);
            Assert.IsFalse(sim.TraderBuy(TraderOffer.Seed), "no coins yet");
            sim.DebugAddCoins(t.SeedPrice + t.RarePrice + 5);
            int seeds = sim.State.Seeds;
            Assert.IsTrue(sim.TraderBuy(TraderOffer.Seed));
            Assert.AreEqual(seeds + 1, sim.State.Seeds);
            Assert.IsFalse(sim.TraderBuy(TraderOffer.Seed), "once a visit");
            Assert.IsTrue(sim.TraderBuy(TraderOffer.RareSeed));
            int golden = 0;
            foreach (var p in sim.State.Plots) if (p.IsGolden) golden++;
            Assert.AreEqual(sim.Config.TraderRarePlots, golden);
            Assert.AreEqual(5, sim.State.Coins, 1e-9);

            Run(sim, sim.Config.TraderSeconds + 0.1f);
            Assert.IsFalse(t.Active, "moved on");
        }

        [Test]
        public void TheTrader_ComesInSummer()
        {
            var cfg = Cfg();
            cfg.TraderFirstYear = 2;
            cfg.TraderChance = 1;
            var sim = new FarmSim(cfg, 5);
            Season? seen = null;
            sim.TraderArrived += () => seen = sim.State.Season;
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            while (sim.State.Phase == Phase.Year && seen == null) sim.Tick(0.1f, null);
            Assert.AreEqual(Season.Summer, seen);
        }
    }
}
