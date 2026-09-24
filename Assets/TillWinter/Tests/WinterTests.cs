using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.6, winter (GDD §3.5/§6.3 v2.2): the barn keeps part of the harvest for the winter market or for preserves,
    /// the Almanac suggests a node, and one free respec a generation refunds what the Almanac cost.
    /// </summary>
    public class WinterTests
    {
        private static FarmConfig Cfg()
        {
            var cfg = TestConfig.Classic();
            cfg.CrowSpawnChance = 0f;
            return cfg;
        }

        private static readonly GridPos Origin = new GridPos(0, 0);

        /// <summary>Coins from one hand reap of plot (0,0), ripened on its own so nothing else stands at the frost.</summary>
        private static double HandReap(FarmSim sim)
        {
            Assert.IsTrue(sim.DebugForceRipe(Origin));
            double before = sim.State.Coins;
            Assert.IsTrue(sim.ReapOne(Origin));
            return sim.State.Coins - before;
        }

        private static FarmSim WithBarn(float share = 0.5f)
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("barn", 1);
            Assert.IsTrue(sim.SetStoreShare(share));
            return sim;
        }

        [Test]
        public void TheStoreShare_NeedsABarn()
        {
            var sim = new FarmSim(Cfg(), 1);
            Assert.IsFalse(sim.SetStoreShare(0.5f));
            sim.DebugSetLevel("barn", 1);
            Assert.IsFalse(sim.SetStoreShare(0.3f), "only the offered shares");
            Assert.IsTrue(sim.SetStoreShare(0.25f));
        }

        [Test]
        public void HalfTheHarvest_GoesToTheBarn()
        {
            var sim = WithBarn();
            int stored = 0;
            sim.Stored += (p, v) => stored++;
            double paid = 0;
            for (int i = 0; i < 4; i++) paid += HandReap(sim);
            Assert.AreEqual(2, stored);
            Assert.AreEqual(2, sim.State.Barn.Count);
            Assert.That(sim.State.Barn.Stock, Is.GreaterThan(0));
            Assert.AreEqual(sim.State.Barn.Stock, paid, 1e-9, "every other harvest paid, the rest were stored at the same value");
        }

        [Test]
        public void TheBarn_StopsAtItsCapacity()
        {
            var sim = WithBarn();
            int cap = sim.Config.BarnCapacityByLevel[1];
            for (int i = 0; i < cap * 2 + 10; i++) HandReap(sim);
            Assert.AreEqual(cap, sim.State.Barn.Count);
        }

        [Test]
        public void TheWinterMarket_SellsAtItsPrice_OutsideTheYearsTake()
        {
            var sim = WithBarn();
            for (int i = 0; i < 6; i++) HandReap(sim);
            Assert.IsFalse(sim.SellBarn(), "the market is a winter thing");
            sim.DebugSkipToWinter();
            var barn = sim.State.Barn;
            Assert.That(barn.MarketPrice, Is.InRange(sim.Config.MarketMin, sim.Config.MarketMax));
            double stock = barn.Stock, coins = sim.State.Coins, year = sim.State.CoinsThisYear;
            Assert.IsTrue(sim.SellBarn());
            Assert.AreEqual(coins + stock * barn.MarketPrice, sim.State.Coins, 1e-9);
            Assert.AreEqual(year, sim.State.CoinsThisYear, 1e-9, "not part of the year's take");
            Assert.AreEqual(0, barn.Count);
        }

        [Test]
        public void Preserves_PayAFixedMultiple_NextSpring()
        {
            var sim = WithBarn();
            for (int i = 0; i < 6; i++) HandReap(sim);
            sim.DebugSkipToWinter();
            double stock = sim.State.Barn.Stock;
            Assert.IsTrue(sim.MakePreserves());
            Assert.AreEqual(stock * sim.Config.PreserveValue, sim.State.Barn.Jars, 1e-9);
            double coins = sim.State.Coins;
            sim.StartNextYear();
            Assert.AreEqual(coins + stock * sim.Config.PreserveValue, sim.State.Coins, 1e-9);
            Assert.AreEqual(stock * sim.Config.PreserveValue, sim.State.CoinsThisYear, 1e-9, "spring's first coins");
            Assert.AreEqual(0, sim.State.Barn.Jars, 1e-9);
        }

        [Test]
        public void StockHeldIntoANewYear_Spoils()
        {
            var sim = WithBarn();
            for (int i = 0; i < 6; i++) HandReap(sim);
            sim.DebugSkipToWinter();
            double stock = sim.State.Barn.Stock;
            sim.StartNextYear();
            Assert.AreEqual(stock * (1 - sim.Config.BarnSpoil), sim.State.Barn.Stock, 1e-9);
        }

        [Test]
        public void TheRespec_RefundsTheAlmanac_OnceAGeneration()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(2000);
            Assert.IsFalse(sim.CanRespec, "nothing bought yet");
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            double spent = 2000 - sim.State.Coins;
            Assert.AreEqual(spent, sim.State.Generation.AlmanacSpent, 1e-9);
            Assert.AreEqual(4, sim.State.GridSize);

            Assert.IsTrue(sim.RespecAlmanac());
            Assert.AreEqual(2000, sim.State.Coins, 1e-9, "every coin back");
            Assert.AreEqual(0, sim.Almanac.Levels.Count);
            Assert.AreEqual(3, sim.State.GridSize, "the field goes back to its size");
            foreach (var p in sim.State.Plots) Assert.AreEqual(0, p.BedTier);
            Assert.AreEqual(sim.Config.BaseStrikeDamage, sim.State.Stats.StrikeDamage, 1e-9, "the hoe is plain again");
            Assert.IsFalse(sim.CanRespec, "once a generation");
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            Assert.IsFalse(sim.RespecAlmanac());

            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            Assert.IsFalse(sim.State.Generation.RespecUsed, "a new generation gets its own");
            Assert.AreEqual(0, sim.State.Generation.AlmanacSpent, 1e-9);
        }

        [Test]
        public void TheAdvisor_SuggestsAnAffordableNode_InWinterOnly()
        {
            var sim = new FarmSim(Cfg(), 1);
            Assert.IsNull(AlmanacAdvisor.Suggest(sim), "not during the year");
            sim.DebugSkipToWinter();
            Assert.IsNull(AlmanacAdvisor.Suggest(sim), "nothing affordable");
            sim.DebugAddCoins(400);
            string id = AlmanacAdvisor.Suggest(sim);
            Assert.IsNotNull(id);
            Assert.IsTrue(sim.CanBuy(id));
        }
    }
}
