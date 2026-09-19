using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.8, ending and after (GDD §8.2–§8.4 v2.4): the family album, New Game+ and the daily farm.
    /// </summary>
    public class AfterEndingTests
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
        public void HandingTheFarmOn_WritesAPageInTheAlbum()
        {
            var cfg = Cfg();
            var sim = new FarmSim(cfg, 1);
            sim.DebugForceRipeAll();
            Run(sim, 2f, new RingInput(1f, 1f));
            int harvests = sim.State.Generation.Harvests;
            Assert.That(harvests, Is.GreaterThan(0));
            sim.DebugSkipToWinter();
            int stars = sim.State.LastGrade;
            sim.StartNextYear();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            int seeds = sim.SeedsIfRetiredNow;
            Assert.IsTrue(sim.Retire());

            Assert.AreEqual(1, sim.State.Album.Count);
            var page = sim.State.Album[0];
            Assert.AreEqual(1, page.Generation);
            Assert.AreEqual(2, page.Years);
            Assert.AreEqual(seeds, page.Seeds);
            Assert.AreEqual(harvests, page.Harvests);
            Assert.AreEqual(stars, page.BestGrade);
            Assert.That(page.Coins, Is.GreaterThanOrEqualTo(cfg.HeritageThreshold));

            sim.StartNewGeneration();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            Assert.AreEqual(2, sim.State.Album.Count);
            Assert.AreEqual(0, sim.State.Album[1].Harvests, "the second generation harvested nothing");
            Assert.AreEqual((int)sim.State.Generation.HeirOffer[0] != 0 ? 1 : 0, 1, "an heir was on offer for the second page");
        }

        [Test]
        public void TheAlbum_KeepsItsLastPages()
        {
            var cfg = Cfg();
            cfg.AlbumPages = 2;
            var sim = new FarmSim(cfg, 1);
            for (int i = 0; i < 3; i++)
            {
                sim.DebugSkipToWinter();
                sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
                Assert.IsTrue(sim.Retire());
                sim.StartNewGeneration();
            }
            Assert.AreEqual(2, sim.State.Album.Count);
            Assert.AreEqual(2, sim.State.Album[0].Generation, "the oldest page went");
        }

        [Test]
        public void NewGamePlus_StartsOverHarder_KeepingTheAlbumAndHeirlooms()
        {
            var cfg = Cfg();
            cfg.CrowSpawnChance = 0.25f;
            var ended = new FarmSim(cfg, 1);
            Assert.IsNull(AfterEnding.NewGamePlus(ended, cfg, 2), "only after the ending");
            ended.DebugSkipToWinter();
            ended.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(ended.Retire());
            ended.DebugUnlockAchievement(AchievementId.Golden10);
            ended.DebugMarkEndingSeen();

            var plus = AfterEnding.NewGamePlus(ended, cfg, 2);
            Assert.IsNotNull(plus);
            var s = plus.State;
            Assert.AreEqual(1, s.NgPlus);
            Assert.AreEqual(1, s.Generation.Generation);
            Assert.AreEqual(0, s.Coins, 1e-9);
            Assert.IsFalse(s.EndingSeen);
            Assert.AreEqual(ended.State.Album.Count, s.Album.Count);
            Assert.IsTrue(Legacy.Has(s.Generation.Achievements, AchievementId.Golden10));
            var fresh = new FarmSim(cfg, 2).State.Stats;
            Assert.That(s.Stats.CrowSpawnChance, Is.GreaterThan(fresh.CrowSpawnChance));
            Assert.That(s.Stats.YearLength, Is.LessThan(fresh.YearLength));
        }

        [Test]
        public void TheDailyFarm_IsTheSameForTheSameDay_AndPlaysOneYear()
        {
            var a = AfterEnding.Daily(20260919);
            var b = AfterEnding.Daily(20260919);
            Assert.IsTrue(a.State.IsDaily);
            Assert.AreEqual(1, a.State.Year);
            Assert.AreEqual(a.State.GridSize, b.State.GridSize);
            Assert.IsTrue(a.State.Goal.Active, "a goal from the first year");
            Assert.AreNotEqual(Weather.Clear, a.State.PlannedWeather, "a spell every day");
            for (int i = 0; i < 4000 && a.State.Phase == Phase.Year; i++)
            {
                var ring = new RingInput(1f + (i / 40) % 3, 1f + (i / 120) % 3);
                a.Tick(0.05f, ring);
                b.Tick(0.05f, ring);
            }
            Assert.AreEqual(Phase.Winter, a.State.Phase, "one year and it is over");
            Assert.AreEqual(a.State.CoinsThisYear, b.State.CoinsThisYear, 1e-9);
            Assert.That(a.State.CoinsThisYear, Is.GreaterThan(0));
        }

        [Test]
        public void DifferentDays_GiveDifferentFarms()
        {
            int differ = 0;
            var first = AfterEnding.Daily(20260101);
            for (int d = 2; d <= 20; d++)
            {
                var other = AfterEnding.Daily(20260100 + d);
                if (other.State.GridSize != first.State.GridSize || other.State.Stats.MaxTierUnlocked != first.State.Stats.MaxTierUnlocked
                    || other.State.Stats.ApprenticeCount != first.State.Stats.ApprenticeCount || other.State.Goal.Type != first.State.Goal.Type)
                    differ++;
            }
            Assert.That(differ, Is.GreaterThan(5));
            Assert.AreNotEqual(AfterEnding.DailySeed(20260101), AfterEnding.DailySeed(20260102));
        }
    }
}
