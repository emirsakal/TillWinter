using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Core.Balance;

namespace TillWinter.Tests
{
    /// <summary>Sanity only: the headless AutoPlayer runs three generations at a fixed seed without exceptions.</summary>
    public class BalanceTests
    {
        [Test]
        public void AutoPlayer_ThreeGenerations_Seed7_SanityOnly()
        {
            var sim = new FarmSim(new FarmConfig(), 7);
            var player = new AutoPlayer(sim, 7) { MaxTicks = 1_500_000 };
            player.Run(3);
            var s = sim.State;
            Assert.That(s.Generation.Generation, Is.GreaterThanOrEqualTo(4), "three retirements happened within the tick budget");
            Assert.That(player.Rows.Count, Is.GreaterThan(3));
            Assert.That(s.Generation.LifetimeCoinsTotal, Is.GreaterThan(3 * sim.Config.HeritageThreshold));
            Assert.That(s.Generation.SeedsEarnedTotal, Is.GreaterThanOrEqualTo(30));
            double prevTotal = -1;
            foreach (var r in player.Rows)
            {
                Assert.That(r.TotalCoins, Is.GreaterThanOrEqualTo(prevTotal), "total coins never decrease");
                prevTotal = r.TotalCoins;
                Assert.That(r.CoinsThisYear, Is.GreaterThanOrEqualTo(0));
            }
            string csv = player.ToCsv();
            StringAssert.StartsWith("generation,year", csv);
            Assert.That(player.ToTable().Split('\n').Length, Is.GreaterThan(4));
        }

        [Test]
        public void AutoPlayer_IsDeterministic()
        {
            string Table(int seed)
            {
                var sim = new FarmSim(new FarmConfig(), seed);
                var p = new AutoPlayer(sim, seed) { MaxTicks = 200_000 };
                p.Run(1);
                return p.ToCsv();
            }
            Assert.AreEqual(Table(3), Table(3));
        }
    }
}
