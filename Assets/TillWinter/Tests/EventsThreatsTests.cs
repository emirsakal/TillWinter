using NUnit.Framework;
using TillWinter.Core;
using static TillWinter.Tests.CoreLoopTests;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.5, events and threats (GDD §5.5–§5.7 v2.1, re-themed v3): the mole, the rabbit and the locusts, the hens that
    /// eat them, the clover, the golden egg and the shooting star, and the travelling trader.
    /// </summary>
    public class EventsThreatsTests
    {
        /// <summary>Only what a test puts on the field: no crows, weather, goals, special ground or random events.</summary>
        private static FarmSim Sim(System.Action<FarmConfig> tweak = null, int seed = 1) =>
            NewSim(c => { c.CrowSpawnChance = 0f; tweak?.Invoke(c); }, seed);

        [Test]
        public void AMole_DigsItsPlotUp_UnlessBonked()
        {
            var sim = Sim();
            sim.DebugForceRipeAll();
            var at = new GridPos(1, 1);
            int struck = 0;
            sim.PestStruck += (k, p) => { struck++; Assert.AreEqual(PestKind.Mole, k); };
            sim.DebugSpawnPest(PestKind.Mole, at);
            Run(sim, sim.Config.MoleDigSeconds + 0.1f);
            Assert.AreEqual(1, struck);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(at).State, "dug up");
            Assert.AreEqual(1, sim.State.GetPlot(at).Layer, "the ground returns a layer deeper all the same");
            Assert.AreEqual(1, sim.State.CropsLostThisYear, "a ripe crop lost counts against the grade");

            var tapped = Sim();
            tapped.DebugSpawnPest(PestKind.Mole, at);
            double before = tapped.State.Coins;
            Assert.IsTrue(tapped.TapAt(at));
            Assert.AreEqual(PestKind.None, tapped.State.Pest.Kind);
            Assert.AreEqual(before + tapped.Config.MoleBounty * 1, tapped.State.Coins, 1e-9, "a bonked mole drops a crop's worth");
            Assert.AreEqual(1, tapped.State.Generation.PestsStopped);
        }

        [Test]
        public void ARabbit_EatsACarrot_UnlessTapped()
        {
            var sim = Sim();
            sim.DebugForceRipeAll();
            var at = new GridPos(2, 2);
            sim.DebugSpawnPest(PestKind.Rabbit, at);
            Run(sim, sim.Config.RabbitEatSeconds + 0.1f);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(at).State, "eaten");
            Assert.AreEqual(1, sim.State.GetPlot(at).Layer);

            var chased = Sim();
            chased.DebugForceRipeAll();
            chased.DebugSpawnPest(PestKind.Rabbit, at);
            int scared = 0;
            chased.PestScared += (k, p, c) => scared++;
            Assert.IsTrue(chased.TapAt(at));
            Assert.AreEqual(1, scared);
            Assert.AreEqual(PestKind.None, chased.State.Pest.Kind);
            Assert.AreEqual(PlotState.Ripe, chased.State.GetPlot(at).State, "the tap shooed the rabbit, it did not reap");
        }

        [Test]
        public void Locusts_StopGrowthInTheirPatch_AndStrikesInsideDriveThemOff()
        {
            var sim = Sim();
            var edge = sim.State.GetPlot(2, 2);
            sim.DebugBreak(edge.Pos);
            Run(sim, 0.5f);
            Assert.That(edge.Progress, Is.GreaterThan(0f));
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(1, 1)); // 3x3 around the centre: the whole field
            Assert.IsTrue(sim.InLocusts(edge.Pos));
            float grown = edge.Progress;
            Run(sim, 1f);
            Assert.AreEqual(grown, edge.Progress, 1e-6, "nothing grows under the swarm");

            // LocustShooStrikes (3) strikes on hard ground inside the swarm: each adds a third to Shoo.
            var corner = new GridPos(0, 0);
            for (int i = 0; i < sim.Config.LocustShooStrikes; i++)
            {
                Assert.AreEqual(PestKind.Locusts, sim.State.Pest.Kind, "still there before strike " + (i + 1));
                sim.DebugClearCooldown();
                WaitForBeat(sim, false);
                Assert.IsTrue(sim.Strike(corner));
            }
            Assert.AreEqual(PestKind.None, sim.State.Pest.Kind, "the strikes inside the swarm drove it off");
            Run(sim, 0.5f);
            Assert.That(edge.Progress, Is.GreaterThan(grown), "and the crops grow again");
        }

        [Test]
        public void LeftAlone_LocustsStripTheirPatch_RipeIsLost_GrowingStartsOver()
        {
            var sim = Sim(c => c.Crops[0].Grow = 1000f);
            sim.DebugForceRipeAll();
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(0, 0)); // the 2x2 corner of the field
            Run(sim, sim.Config.LocustSeconds + 0.1f);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(1, 1).State, "inside the patch: the crop is lost");
            Assert.AreEqual(1, sim.State.GetPlot(1, 1).Layer);
            Assert.AreEqual(PlotState.Ripe, sim.State.GetPlot(2, 2).State, "outside it");

            var growing = Sim(c => c.Crops[0].Grow = 1000f);
            growing.DebugBreak(new GridPos(0, 0));
            growing.DebugSpawnCloud(); growing.TapCloud(); // 0.25
            growing.DebugSpawnPest(PestKind.Locusts, new GridPos(0, 0));
            Run(growing, growing.Config.LocustSeconds + 0.1f);
            Assert.AreEqual(PlotState.Growing, growing.State.GetPlot(0, 0).State);
            // Reset to 0 at LocustSeconds; the last 0.1 s of a 1000 s crop adds 0.0001.
            Assert.That(growing.State.GetPlot(0, 0).Progress, Is.LessThan(0.01f), "a growing crop starts over");
        }

        [Test]
        public void TheHens_EatALingeringPest()
        {
            var sim = Sim();
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
            var sim = Sim(c => { c.PestFirstYear = 3; c.PestChance = 1; }, 3);
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
        public void AClover_IsTakenByAStrike_OrATap_OnItsPlot()
        {
            var sim = Sim();
            var at = new GridPos(2, 0);
            sim.DebugSpawnLucky(LuckyKind.Clover, at);
            double paid = 0;
            sim.LuckyFound += (k, c) => paid = c;
            Run(sim, 0.5f);
            Assert.AreEqual(0, paid, 1e-9, "not yet found");
            WaitForBeat(sim, false);
            Assert.IsTrue(sim.Strike(at));
            Assert.AreEqual(sim.Config.CloverValue * 1, paid, 1e-9, "a handful of crop values");
            Assert.AreEqual(0f, sim.State.Luck.CloverLeft);

            var tap = Sim();
            tap.DebugForceRipe(at);
            tap.DebugSpawnLucky(LuckyKind.Clover, at);
            double tapPaid = 0;
            tap.LuckyFound += (k, c) => tapPaid = c;
            Assert.IsTrue(tap.TapAt(at));
            Assert.AreEqual(tap.Config.CloverValue * 1, tapPaid, 1e-9);
            Assert.AreEqual(0f, tap.State.Luck.CloverLeft);
        }

        [Test]
        public void CatchingTheShootingStar_MakesHarvestsPayDouble()
        {
            var plain = Sim();
            var lucky = Sim();
            plain.DebugForceRipeAll();
            lucky.DebugForceRipeAll();
            Assert.IsFalse(lucky.TapStar(), "no star in the sky");
            lucky.DebugSpawnLucky(LuckyKind.ShootingStar, new GridPos(0, 0));
            Assert.IsTrue(lucky.TapStar());
            double a = plain.State.Coins, b = lucky.State.Coins;
            Assert.IsTrue(plain.TapAt(new GridPos(0, 0)));
            Assert.IsTrue(lucky.TapAt(new GridPos(0, 0)));
            Assert.AreEqual(1, plain.State.Coins - a, 1e-9);
            Assert.AreEqual((plain.State.Coins - a) * lucky.Config.StarRushValue, lucky.State.Coins - b, 1e-9);
        }

        [Test]
        public void TheTrader_SellsASeedAndRareSeed_OnceEach()
        {
            var sim = Sim();
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
            Assert.AreEqual(sim.Config.TraderRarePlots, golden, "a hard plot's next seed is golden");
            Assert.AreEqual(5, sim.State.Coins, 1e-9);

            Run(sim, sim.Config.TraderSeconds + 0.1f);
            Assert.IsFalse(t.Active, "moved on");
        }

        [Test]
        public void TheTrader_ComesInSummer()
        {
            var sim = Sim(c => { c.TraderFirstYear = 2; c.TraderChance = 1; }, 5);
            Season? seen = null;
            sim.TraderArrived += () => seen = sim.State.Season;
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            while (sim.State.Phase == Phase.Year && seen == null) sim.Tick(0.1f);
            Assert.AreEqual(Season.Summer, seen);
        }
    }
}
