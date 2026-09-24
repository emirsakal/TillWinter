using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Fixes from the first full play-test after mechanics round one (re-themed v3): offline stays passive, purchases
    /// do what they say, the daily farm stays apart, and the edge cases around retire, respec and New Game+ hold.
    /// </summary>
    public class PlaytestFixesTests
    {
        private static FarmConfig Cfg()
        {
            var cfg = TestConfig.Classic();
            cfg.CrowSpawnChance = 0f;
            cfg.BaseCritChance = 0;
            return cfg;
        }

        /// <summary>A year with some take: the greenhouse is capped to a share of it (v2.8), so an empty year earns none.</summary>
        private static void EarnAYear(FarmSim sim)
        {
            sim.DebugForceRipeAll();
            sim.ReapAll();
            Assert.That(sim.State.CoinsThisYear, Is.GreaterThan(0), "the swipe reaped something");
        }

        private static void Winter(FarmSim sim, double coins)
        {
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(coins);
        }

        [Test]
        public void Offline_TheSkyIsClear_AStormLeftBehindDoesNotSpeedTheCrops()
        {
            var cfg = Cfg();
            cfg.Crops[0].Grow = 100f;
            var sim = new FarmSim(cfg, 1);
            sim.DebugBreakAll();
            sim.DebugStartWeather(Weather.Storm);
            Assert.AreEqual(Weather.Storm, sim.State.Weather);
            sim.SimulateOffline(60);
            // 60 s of a 100 s crop at the plain rate; a storm frozen for the whole stretch would have made it 0.9.
            Assert.That(sim.State.GetPlot(1, 1).Progress, Is.EqualTo(0.6f).Within(0.01f), "an hour away is clear skies");
        }

        [Test]
        public void Offline_ALocustSwarmLeftBehind_DoesNotBlockGrowth()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugBreakAll();
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(1, 1));
            sim.SimulateOffline(600);
            Assert.IsTrue(sim.State.GetPlot(1, 1).IsRipe);
        }

        [Test]
        public void TheDailyFarm_GetsNoOfflineProgress()
        {
            var daily = AfterEnding.Daily(20260919);
            var report = daily.SimulateOffline(3600);
            Assert.AreEqual(0, report.SecondsSimulated);
            Assert.AreEqual(0, report.CoinsEarned);
        }

        [Test]
        public void TheDailyFarm_SurvivesASceneRebuild()
        {
            var daily = AfterEnding.Daily(20260919);
            var centre = new GridPos(1, 1);
            for (int i = 0; i < 200; i++)
            {
                if (daily.CanStrike) daily.Strike(centre);
                daily.Tick(0.05f);
            }
            Assert.That(daily.State.Generation.Strikes, Is.GreaterThan(0));
            var again = AfterEnding.ResumeDaily(daily.ToSave(), 20260919);
            Assert.IsNotNull(again);
            Assert.IsTrue(again.State.IsDaily);
            Assert.AreEqual(daily.State.Coins, again.State.Coins, 1e-9);
            Assert.AreEqual(daily.State.YearTime, again.State.YearTime, 1e-6);
            Assert.AreEqual(daily.State.GetPlot(centre).Hp, again.State.GetPlot(centre).Hp, 1e-9);
            Assert.IsFalse(again.HintPending(Hint.FirstTouch), "the daily still teaches nothing");
        }

        [Test]
        public void ExpandField_IsMaxed_OnceTheFieldIsFull()
        {
            var cfg = Cfg();
            cfg.StartGridSize = 4; // as with the Heritage head start
            var sim = new FarmSim(cfg, 1);
            Winter(sim, 1e6);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.AreEqual(cfg.MaxGridSize, sim.State.GridSize);
            double coins = sim.State.Coins;
            Assert.IsFalse(sim.TryBuy("expand_field"), "a third level would add nothing");
            Assert.AreEqual(coins, sim.State.Coins);
            Assert.IsTrue(sim.IsMaxed("expand_field"));
            Assert.AreEqual(2, sim.GetMaxLevel("expand_field"));
        }

        [Test]
        public void BulkUpgrade_RaisesTwoDifferentPlots()
        {
            var sim = new FarmSim(Cfg(), 1);
            Winter(sim, 1e6);
            sim.DebugSetLevel("unlock_tomato", 1);
            sim.DebugSetLevel("bulk_upgrade", 1);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            int raised = 0;
            foreach (var p in sim.State.Plots)
            {
                Assert.That(p.BedTier, Is.LessThanOrEqualTo(1), "never one bed twice");
                raised += p.BedTier;
            }
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void ChangingACrop_SendsItsCrowAway()
        {
            var sim = new FarmSim(Cfg(), 1);
            Winter(sim, 1e7);
            sim.DebugSetLevel("unlock_tomato", 1);
            while (sim.TryBuy("upgrade_plot")) { }
            sim.StartNextYear();
            sim.DebugForceRipeAll();
            Assert.IsTrue(sim.DebugSpawnCrow());
            Plot crowed = null;
            foreach (var p in sim.State.Plots) if (p.HasCrow) crowed = p;
            Assert.IsNotNull(crowed);
            int other = crowed.Tier == 0 ? 1 : 0;
            Assert.IsTrue(sim.SetPlotCrop(crowed.Pos, other));
            Assert.IsFalse(crowed.HasCrow);
            Assert.AreEqual(0, sim.State.Crows.Count);
            int lost = sim.State.CropsLostThisYear;
            for (int i = 0; i < 200; i++) sim.Tick(0.05f);
            Assert.AreEqual(lost, sim.State.CropsLostThisYear, "no crow left to eat the replanted bed");
        }

        [Test]
        public void TheTractor_StaysCharged_WhenNothingIsRipeYet()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("tractor", 1);
            for (int i = 0; i < 2000 && sim.TractorCharge < 1f; i++) sim.Tick(0.05f);
            foreach (var p in sim.State.Plots) Assert.IsFalse(p.IsRipe, "nothing grows on unbroken ground");
            for (int i = 0; i < 100; i++) sim.Tick(0.05f);
            Assert.AreEqual(1f, sim.TractorCharge, 1e-4, "the bar waits full for a ripe row");
        }

        [Test]
        public void NewGamePlus_KeepsTheLessonsAndTheFamilyRecord()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.MarkHint(Hint.FirstTouch);
            sim.DebugBreakAll();
            for (int i = 0; i < 60; i++) sim.Tick(0.05f); // 3 s: the carrots ripen
            Assert.AreEqual(9, sim.ReapAll());
            sim.DebugMarkEndingSeen();
            var plus = AfterEnding.NewGamePlus(sim, Cfg(), 2);
            Assert.IsNotNull(plus);
            Assert.IsFalse(plus.HintPending(Hint.FirstTouch), "a hint never fires twice");
            Assert.AreEqual(sim.State.Generation.Harvests, plus.State.Generation.Harvests);
            Assert.AreEqual(sim.State.Generation.HarvestsHand, plus.State.Generation.HarvestsHand);
            Assert.AreEqual(sim.State.Generation.LifetimeCoinsTotal, plus.State.Generation.LifetimeCoinsTotal, 1e-9);
            Assert.AreEqual(0, plus.State.Generation.LifetimeCoinsThisGeneration);
            Assert.AreEqual(1, plus.State.Generation.Generation);
        }

        [Test]
        public void Respec_TakesBackTheWintersGreenhouseIncome()
        {
            var sim = new FarmSim(Cfg(), 1);
            EarnAYear(sim);
            Winter(sim, 1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            for (int i = 0; i < 200; i++) sim.Tick(0.05f);
            double earned = sim.State.Greenhouse.CoinsThisWinter;
            Assert.That(earned, Is.GreaterThan(0));
            double coins = sim.State.Coins, spent = sim.State.Generation.AlmanacSpent;
            Assert.IsTrue(sim.RespecAlmanac());
            Assert.AreEqual(coins + spent - earned, sim.State.Coins, 1e-6);
        }

        [Test]
        public void GreenhouseCoins_AreNotTheYearsTake()
        {
            var sim = new FarmSim(Cfg(), 1);
            EarnAYear(sim);
            Winter(sim, 1e6);
            sim.DebugSetLevel("year_length", 1);
            Assert.IsTrue(sim.TryBuy("greenhouse"));
            double take = sim.State.CoinsThisYear;
            for (int i = 0; i < 200; i++) sim.Tick(0.05f);
            Assert.AreEqual(take, sim.State.CoinsThisYear, 1e-9);
        }

        [Test]
        public void ALoadedSave_NeverKeepsTwoScarecrowsOnOneCorner()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("scarecrow", 2);
            var d = sim.ToSave();
            d.ScarecrowX = new[] { 1, 1 };
            d.ScarecrowY = new[] { 1, 1 };
            var loaded = FarmSim.FromSave(d, Cfg());
            Assert.AreEqual(2, loaded.State.Scarecrows.Count);
            Assert.AreNotEqual(loaded.State.Scarecrows[0], loaded.State.Scarecrows[1]);
        }

        [Test]
        public void Respec_CannotReRollTheGround()
        {
            var cfg = Cfg();
            cfg.FertileChance = 0.3;
            var sim = new FarmSim(cfg, 1);
            Winter(sim, 1e6);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            var before = new PlotKind[sim.State.GridSize * sim.State.GridSize];
            foreach (var p in sim.State.Plots) before[p.Pos.Y * sim.State.GridSize + p.Pos.X] = p.Kind;
            Assert.IsTrue(sim.RespecAlmanac());
            Assert.AreEqual(cfg.StartGridSize, sim.State.GridSize);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            foreach (var p in sim.State.Plots)
                Assert.AreEqual(before[p.Pos.Y * sim.State.GridSize + p.Pos.X], p.Kind, "same ground at " + p.Pos);
        }

        [Test]
        public void TheGoalReward_IsTheYearsMoney_ButNotNextYearsYardstick()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetGoal(GoalType.Coins, 3, 0, 50);
            sim.DebugForceRipeAll();
            sim.ReapAll(); // 9 carrots x 1.8 combo = 16.2 coins: the goal is met and pays its 50
            Assert.IsTrue(sim.State.Goal.Done);
            double take = sim.State.CoinsThisYear;
            Assert.That(take, Is.GreaterThan(50));
            sim.DebugSkipToWinter();
            Assert.AreEqual(take - 50, sim.State.LastYearCoins, 1e-6);
        }

        private static FarmSim RetiredIntoHeritage(FarmConfig cfg)
        {
            var sim = new FarmSim(cfg, 1);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            return sim;
        }

        [Test]
        public void TomatoSeeds_StartTheGenerationWithTomatoUnlocked()
        {
            var cfg = Cfg();
            var sim = RetiredIntoHeritage(cfg);
            sim.DebugSetLevel("h_start_field", 1);
            sim.DebugSetLevel("h_start_tomato", 1);
            sim.StartNewGeneration();
            Assert.AreEqual(1, sim.State.GetLevel("unlock_tomato"), "owned, as if bought");
            Assert.AreEqual(0, sim.State.Generation.AlmanacSpent, "but free");
            Winter(sim, 1e6);
            Assert.IsTrue(sim.CanBuy("upgrade_plot"), "the beds can be raised without buying tomato again");
        }

        [Test]
        public void NoHelpers_ClosesTheHelperNodes_ButNotTheScarecrowPath()
        {
            var cfg = Cfg();
            var sim = RetiredIntoHeritage(cfg);
            Assert.IsTrue(sim.SetChallenge(ChallengeKind.NoHelpers));
            sim.StartNewGeneration();
            Winter(sim, 1e6);
            Assert.IsFalse(sim.CanBuy("apprentice_count"));
            Assert.IsFalse(sim.IsAvailable("apprentice_count"));
            Assert.IsTrue(sim.CanBuy("scarecrow"), "the scarecrow behind it stays open");
            Assert.AreNotEqual("apprentice_count", AlmanacAdvisor.Suggest(sim));
        }

        [Test]
        public void YearsTotal_CountsEveryGenerationsFirstYear()
        {
            var cfg = Cfg();
            var sim = new FarmSim(cfg, 1);
            Assert.AreEqual(1, sim.State.Generation.YearsTotal);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Assert.AreEqual(2, sim.State.Generation.YearsTotal);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            Assert.AreEqual(3, sim.State.Generation.YearsTotal);
        }

        [Test]
        public void UpgradePlotMax_CountsTwoBedsAPurchase_WithBulkUpgrade()
        {
            var sim = new FarmSim(Cfg(), 1);
            Winter(sim, 1e6);
            sim.DebugSetLevel("unlock_tomato", 1);
            Assert.AreEqual(9, sim.GetMaxLevel("upgrade_plot"));
            sim.DebugSetLevel("bulk_upgrade", 1);
            Assert.AreEqual(5, sim.GetMaxLevel("upgrade_plot"), "nine beds, two a purchase");
        }

        [Test]
        public void WholeNumbers_UseTheLanguagesSeparator()
        {
            var style = NumberFormat.Style;
            try
            {
                NumberFormat.Style = NumberStyle.English;
                Assert.AreEqual("1,234", NumberFormat.Whole(1234));
                NumberFormat.Style = NumberStyle.Turkish;
                Assert.AreEqual("1.234", NumberFormat.Whole(1234));
                Assert.AreEqual("12", NumberFormat.Whole(12));
            }
            finally { NumberFormat.Style = style; }
        }

        [Test]
        public void TheRainCloud_DoesNotGrowUnderASwarm()
        {
            var sim = new FarmSim(Cfg(), 1);
            var plot = sim.State.GetPlot(1, 1);
            Assert.IsTrue(sim.DebugBreak(plot.Pos));
            sim.DebugSpawnPest(PestKind.Locusts, new GridPos(1, 1));
            Assert.IsTrue(sim.DebugSpawnCloud());
            float before = plot.Progress;
            Assert.IsTrue(sim.TapCloud());
            Assert.AreEqual(PlotState.Growing, plot.State);
            Assert.AreEqual(before, plot.Progress, 1e-6);
        }

        [Test]
        public void ACorruptPhase_StartsFresh_AndAnUndefinedChallengeIsRefused()
        {
            var cfg = Cfg();
            var d = new FarmSim(cfg, 1).ToSave();
            d.Phase = 9;
            Assert.IsNull(FarmSim.FromSave(d, cfg));

            var sim = new FarmSim(cfg, 1);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            Assert.IsFalse(sim.SetChallenge((ChallengeKind)99));
            Assert.AreEqual(ChallengeKind.None, sim.State.Generation.Challenge);
        }
    }
}
