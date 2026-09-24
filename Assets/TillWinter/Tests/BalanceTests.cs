using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Core.Balance;

namespace TillWinter.Tests
{
    /// <summary>
    /// The balance targets (GDD §15, re-measured for core loop v3), measured by the AutoPlayer playing to the ending.
    /// The targets live here: a tuning change that breaks one fails this test. Tolerances are a small margin around
    /// the target ranges; the averages are over seeds 1-3. The acceptance rule of GDD §2v3.13 is a test here too.
    /// </summary>
    public class BalanceTests
    {
        private static BalanceSummary[] _runs;

        private static BalanceSummary[] Runs()
        {
            if (_runs != null) return _runs;
            _runs = new BalanceSummary[3];
            for (int i = 0; i < 3; i++)
            {
                var p = new AutoPlayer(new FarmSim(new FarmConfig(), i + 1), i + 1) { MaxTicks = 20_000_000 };
                p.RunToEnding();
                _runs[i] = p.Summary;
            }
            return _runs;
        }

        private static double Avg(System.Func<BalanceSummary, double> f)
        {
            double sum = 0;
            foreach (var r in Runs()) sum += f(r);
            return sum / Runs().Length;
        }

        [Test]
        public void EveryRun_ReachesTheEnding()
        {
            foreach (var r in Runs()) Assert.IsTrue(r.ReachedEnding);
        }

        [Test]
        public void Year1_Earns380To560_AndTheFirstWinterBuysTwoOrThreeRootNodes()
        {
            // v3.3 (seeds 1-3): 472, 467, 434 coins; the hoe earns about four times what the ring did, and the Almanac
            // costs four times as much, so the first winter still buys a couple of roots and not the whole tree.
            Assert.That(Avg(r => r.Year1Coins), Is.InRange(380, 560));
            foreach (var r in Runs()) Assert.That(r.Year1RootNodesBought, Is.InRange(1, 4));
        }

        [Test]
        public void FirstApprentice_InYear3To5()
        {
            Assert.That(Avg(r => r.FirstApprenticeYear), Is.InRange(3, 5.5)); // v3.3: year 4 on every seed
        }

        [Test]
        public void FirstRetire_InYear5To7_With8To13Seeds()
        {
            Assert.That(Avg(r => r.FirstCanRetireYear), Is.InRange(4.5, 7.5)); // v3.3: years 6, 5, 6
            Assert.That(Avg(r => r.SeedsAtFirstRetire), Is.InRange(8, 13));     // 9, 10, 9
        }

        [Test]
        public void Heritage_MaxedIn5To7Generations_Ending_In6To9Hours()
        {
            Assert.That(Avg(r => r.GenerationsToMaxHeritage), Is.InRange(5, 7.5)); // v3.3: 6 on every seed
            Assert.That(Avg(r => r.SimSecondsToEnding / 3600.0), Is.InRange(6, 9)); // 7.31, 7.27, 7.38 h
        }

        [Test]
        public void HandShare_StaysTheLargestPartOfTheHarvest_Throughout()
        {
            // The point of v3 (GDD §2v3.1): the active layer never becomes irrelevant. The hand breaks every layer, so
            // its share of the reaping is what the helpers leave it; it must not fall to the ring's 20-30 %.
            // v3.3: 100 % in year 1, 100 % at the first retire, 73-76 % in generation 4 with seven helpers.
            Assert.That(Avg(r => r.HandShareYear1), Is.GreaterThanOrEqualTo(0.90));
            Assert.That(Avg(r => r.HandShareAtFirstRetire), Is.GreaterThanOrEqualTo(0.80));
            Assert.That(Avg(r => r.HandShareGen4), Is.GreaterThanOrEqualTo(0.55));
        }

        [Test]
        public void NoFiniteNode_TakesMoreThan35PercentOfAGenerationsCoins()
        {
            foreach (var r in Runs()) Assert.That(r.MaxNodeSpendShare, Is.LessThanOrEqualTo(0.35), r.MaxNodeSpendId + " in generation " + r.MaxNodeSpendGeneration);
        }

        /// <summary>GDD §2v3.13: play must matter. The smart player out-earns random tapping, and timing out-earns not timing.</summary>
        [Test]
        public void Acceptance_SmartBeatsDumbByHalfAgain_AndTimingPaysFifteenPercent()
        {
            double Play(bool dumb, float skill)
            {
                var p = new AutoPlayer(new FarmSim(new FarmConfig(), 1), 1) { Dumb = dumb, Skill = skill, MaxTicks = 4_000_000 };
                p.RunYears(5);
                return p.TotalCoins;
            }
            double smart = Play(false, 0.8f), dumb = Play(true, 0f), clumsy = Play(false, 0.3f);
            Assert.That(smart, Is.GreaterThanOrEqualTo(dumb * 1.5), "smart " + smart + " vs dumb " + dumb);
            Assert.That(smart, Is.GreaterThan(clumsy * 1.15), "on-beat " + smart + " vs off-beat " + clumsy);
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
