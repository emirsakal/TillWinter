using NUnit.Framework;
using TillWinter.Core;
using TillWinter.Core.Balance;

namespace TillWinter.Tests
{
    /// <summary>
    /// The Session 9 balance targets (GDD §15), measured by the AutoPlayer playing to the ending.
    /// The targets live here: a tuning change that breaks one fails this test. Tolerances are a small margin
    /// around the target ranges; the averages are over seeds 1-3.
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
        public void Year1_Earns40To70_AndTheFirstWinterBuysExactlyOneRootNode()
        {
            Assert.That(Avg(r => r.Year1Coins), Is.InRange(40, 70));
            foreach (var r in Runs()) Assert.AreEqual(1, r.Year1RootNodesBought);
        }

        [Test]
        public void FirstApprentice_InYear3To4()
        {
            Assert.That(Avg(r => r.FirstApprenticeYear), Is.InRange(3, 4.5));
        }

        [Test]
        public void FirstRetire_InYear6To8_With8To12Seeds()
        {
            Assert.That(Avg(r => r.FirstCanRetireYear), Is.InRange(6, 8.5));
            Assert.That(Avg(r => r.SeedsAtFirstRetire), Is.InRange(8, 12));
        }

        [Test]
        public void Heritage_MaxedIn5To6Generations_Ending_In5To7Hours()
        {
            Assert.That(Avg(r => r.GenerationsToMaxHeritage), Is.InRange(5, 6.5));
            Assert.That(Avg(r => r.SimSecondsToEnding / 3600.0), Is.InRange(5, 7));
        }

        [Test]
        public void RingShare_FallsFromYear1_ToFirstRetire_ToGeneration4()
        {
            Assert.That(Avg(r => r.RingShareYear1), Is.GreaterThanOrEqualTo(0.70));
            Assert.That(Avg(r => r.RingShareAtFirstRetire), Is.InRange(0.40, 0.57));
            Assert.That(Avg(r => r.RingShareGen4), Is.LessThanOrEqualTo(0.32));
        }

        [Test]
        public void NoFiniteNode_TakesMoreThan35PercentOfAGenerationsCoins()
        {
            foreach (var r in Runs()) Assert.That(r.MaxNodeSpendShare, Is.LessThanOrEqualTo(0.35), r.MaxNodeSpendId + " in generation " + r.MaxNodeSpendGeneration);
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
