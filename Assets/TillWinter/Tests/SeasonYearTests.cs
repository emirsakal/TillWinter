using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.3, seasons, year and weather (GDD §3/§5.4 v1.9, re-themed v3): spring rain softens the ground, summer
    /// drought bakes it and slows growth, the autumn festival stretches the swipe combo, the frost rush, the year's
    /// grade, yearly goals, and storms, heat waves and fog.
    /// </summary>
    public class SeasonYearTests
    {
        /// <summary>
        /// Only the rule under test moves: no crop preferences, no weather or goals drawn, no special ground, no depth
        /// bonus, and a hoe that never crits by chance. The season and weather numbers stay at their defaults.
        /// </summary>
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.InSeasonValue = 1;
            cfg.FertileChance = 0;
            cfg.ChestChance = cfg.HardpanChance = 0;
            cfg.DepthValue = 0;
            cfg.BaseCritChance = 0;
            cfg.WeatherFirstYear = int.MaxValue;
            cfg.GoalFirstYear = int.MaxValue;
            cfg.HeirloomsEnabled = false; // a first harvest must not nudge the crop value mid-test
            return cfg;
        }

        private static void Run(FarmSim sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / 0.05f);
            for (int i = 0; i < ticks; i++) sim.Tick(0.05f);
        }

        private static readonly GridPos Origin = new GridPos(0, 0);

        /// <summary>Coins from one fresh hand reap of plot (0,0), ripened on its own so nothing else stands at the frost.</summary>
        private static double HandReap(FarmSim sim)
        {
            Assert.IsTrue(sim.DebugForceRipe(Origin));
            double before = sim.State.Coins;
            Assert.IsTrue(sim.ReapOne(Origin));
            return sim.State.Coins - before;
        }

        /// <summary>HP taken off (0,0) by one rested, off-beat strike now (a fresh or just-jumped sim is off the beat).</summary>
        private static double StrikeDamage(FarmSim sim)
        {
            var plot = sim.State.GetPlot(Origin);
            Assert.IsFalse(sim.State.OnBeat);
            double before = plot.Hp;
            Assert.IsTrue(sim.Strike(Origin));
            return before - plot.Hp;
        }

        [Test]
        public void SpringRain_SoftensTheGround()
        {
            var spring = new FarmSim(Cfg(), 1);
            var cfgOff = Cfg();
            cfgOff.SpringSoftness = 1f;
            var plain = new FarmSim(cfgOff, 1);
            Assert.AreEqual(Season.Spring, spring.State.Season);
            Assert.AreEqual(3, StrikeDamage(plain), 1e-9);
            Assert.AreEqual(3 * Cfg().SpringSoftness, StrikeDamage(spring), 1e-9);
        }

        [Test]
        public void SummerDrought_BakesTheGround_AndSlowsGrowth()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetSeason(Season.Summer);
            Assert.AreEqual(Season.Summer, sim.State.Season);
            Assert.AreEqual(3 * Cfg().SummerHardness, StrikeDamage(sim), 1e-6);

            var plot = sim.State.GetPlot(1, 1);
            sim.DebugBreak(plot.Pos);
            Run(sim, 1f);
            // Carrot 2.5 s at 0.8×: 0.32 after a second.
            Assert.That(plot.Progress, Is.EqualTo(Cfg().SummerGrowth / sim.Config.Crops[0].Grow).Within(0.02f));
        }

        [Test]
        public void TheAutumnFestival_PaysMorePerCropInASwipe()
        {
            double SwipeAll(Season season)
            {
                var cfg = Cfg();
                cfg.FrostRushValue = 1; // the autumn jump lands inside the frost warning: keep the rush out of this sum
                var sim = new FarmSim(cfg, 1);
                if (season != Season.Spring) sim.DebugSetSeason(season);
                sim.DebugForceRipeAll();
                double before = sim.State.Coins;
                Assert.AreEqual(9, sim.ReapAll());
                return sim.State.Coins - before;
            }

            var c = Cfg();
            double carrot = c.Crops[0].Value; // 2
            // Nine in one swipe: each pays 1 + 0.1 × 8 = 1.8× in spring, 1 + 0.1 × 1.5 × 8 = 2.2× at the festival.
            Assert.AreEqual(9 * carrot * (1 + c.ComboPerCrop * 8), SwipeAll(Season.Spring), 1e-9);
            Assert.AreEqual(9 * carrot * (1 + c.ComboPerCrop * c.AutumnCombo * 8), SwipeAll(Season.Autumn), 1e-9);
        }

        [Test]
        public void TheFrostRush_PaysMore()
        {
            var spring = new FarmSim(Cfg(), 1);
            double normal = HandReap(spring);

            var frost = new FarmSim(Cfg(), 1);
            frost.DebugSetSeason(Season.Autumn);
            frost.Tick(0.01f);
            Assert.IsTrue(frost.State.FrostWarning);
            Assert.AreEqual(normal * frost.Config.FrostRushValue, HandReap(frost), 1e-9);
        }

        [Test]
        public void AYearOfFreshHarvests_EarnsThreeStars_AndTheirBonus()
        {
            var sim = new FarmSim(Cfg(), 1);
            for (int i = 0; i < 5; i++) HandReap(sim);
            double year = sim.State.CoinsThisYear;
            int stars = 0;
            double paid = -1;
            sim.YearGraded += (s, b) => { stars = s; paid = b; };
            double before = sim.State.Coins;
            sim.DebugSkipToWinter();
            Assert.AreEqual(3, stars);
            Assert.AreEqual(3, sim.State.LastGrade);
            Assert.AreEqual(year * sim.Config.GradeBonusByStars[3], paid, 1e-9);
            Assert.AreEqual(before + paid, sim.State.Coins, 1e-9);
            Assert.AreEqual(sim.State.CoinsThisYear, sim.State.LastYearCoins, 1e-9);
        }

        [Test]
        public void StaleHarvests_EarnOneStar_AndNoBonus()
        {
            var sim = new FarmSim(Cfg(), 1);
            Assert.IsTrue(sim.DebugForceRipe(Origin));
            Run(sim, sim.Config.RipeGraceSeconds + sim.Config.OverripeDecaySeconds);
            Assert.IsTrue(sim.ReapOne(Origin));
            sim.DebugSkipToWinter();
            Assert.AreEqual(1, sim.State.LastGrade);
            Assert.AreEqual(0, sim.State.LastGradeBonus, 1e-9);
        }

        [Test]
        public void AYearWithNoHarvest_EarnsOneStar()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSkipToWinter();
            Assert.AreEqual(1, sim.State.LastGrade);
        }

        [Test]
        public void Goals_StartInTheSecondYear_AndScaleFromTheFirst()
        {
            var cfg = new FarmConfig();
            var sim = new FarmSim(cfg, 4);
            Assert.IsFalse(sim.State.Goal.Active, "year 1 is for learning the field");
            YearGoal set = null;
            sim.GoalSet += g => set = g;
            for (int i = 0; i < 4; i++) HandReap(sim);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Assert.IsNotNull(set);
            var goal = sim.State.Goal;
            Assert.IsTrue(goal.Active);
            Assert.That(goal.Target, Is.GreaterThan(0));
            Assert.AreEqual(System.Math.Max(cfg.GoalMinReward, sim.State.LastYearCoins * cfg.GoalRewardShare), goal.Reward, 1e-9);
        }

        [Test]
        public void AHarvestGoal_PaysOnce_WhenMet()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetGoal(GoalType.HarvestCrop, 2, 0, 50);
            int done = 0;
            sim.GoalCompleted += g => done++;
            double first = HandReap(sim);
            Assert.IsFalse(sim.State.Goal.Done);
            double second = HandReap(sim);
            Assert.IsTrue(sim.State.Goal.Done);
            Assert.AreEqual(first + 50, second, 1e-9, "the reward lands with the harvest that meets it");
            HandReap(sim);
            Assert.AreEqual(1, done);
        }

        [Test]
        public void ComboAndCoinGoals_TrackTheirOwnNumbers()
        {
            var combo = new FarmSim(Cfg(), 1);
            combo.DebugSetGoal(GoalType.Combo, 3);
            combo.DebugForceRipeAll();
            Assert.AreEqual(2, combo.Reap(new[] { new GridPos(0, 0), new GridPos(1, 0) }));
            Assert.IsFalse(combo.State.Goal.Done, "two in a swipe is not three");
            Assert.AreEqual(3, combo.Reap(new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) }));
            Assert.IsTrue(combo.State.Goal.Done, "three in one swipe");

            var coins = new FarmSim(Cfg(), 1);
            coins.DebugSetGoal(GoalType.Coins, 5);
            while (!coins.State.Goal.Done && coins.State.CoinsThisYear < 100) HandReap(coins);
            Assert.IsTrue(coins.State.Goal.Done);
        }

        [Test]
        public void AStorm_GrowsEveryCropFaster_SoftensTheGround_AndPasses()
        {
            var sim = new FarmSim(Cfg(), 1);
            var growing = sim.State.GetPlot(1, 1);
            sim.DebugBreak(growing.Pos);
            Weather last = Weather.Clear;
            sim.WeatherChanged += w => last = w;
            sim.DebugStartWeather(Weather.Storm);
            Assert.AreEqual(Weather.Storm, last);
            // Spring rain × the storm: 3 × 1.25 × 1.25.
            Assert.AreEqual(3 * Cfg().SpringSoftness * Cfg().StormSoftness, StrikeDamage(sim), 1e-6);
            Run(sim, 1f);
            Assert.That(growing.Progress, Is.EqualTo(Cfg().StormGrowth / sim.Config.Crops[0].Grow).Within(0.02f), "1.5× growth in the rain");
            Run(sim, sim.Config.StormSeconds);
            Assert.AreEqual(Weather.Clear, sim.State.Weather);
            Assert.AreEqual(Weather.Clear, last);
        }

        [Test]
        public void Fog_KeepsRipeCropsFresh()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            sim.DebugStartWeather(Weather.Fog);
            Run(sim, sim.Config.FogSeconds - 1f);
            Assert.AreEqual(0f, sim.State.GetPlot(0, 0).RipeAge, 1e-6);
        }

        [Test]
        public void AHeatWave_SlowsGrowth_AndBakesTheGround()
        {
            var hot = new FarmSim(Cfg(), 1);
            var mild = new FarmSim(Cfg(), 1);
            foreach (var s in new[] { hot, mild }) s.DebugBreak(new GridPos(1, 1));
            hot.DebugStartWeather(Weather.HeatWave);
            Run(hot, 0.5f);
            Run(mild, 0.5f);
            float h = hot.State.GetPlot(1, 1).Progress, m = mild.State.GetPlot(1, 1).Progress;
            Assert.That(m, Is.GreaterThan(0f));
            Assert.AreEqual(m * hot.Config.HeatWaveGrowth, h, 1e-3);
            // Spring rain × the heat: 3 × 1.25 × 0.8.
            Assert.AreEqual(StrikeDamage(mild) * hot.Config.HeatWaveHardness, StrikeDamage(hot), 1e-6);
        }

        [Test]
        public void ThePlannedSpell_ComesInItsSeason()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var cfg = Cfg();
                cfg.WeatherFirstYear = 2;
                cfg.WeatherChance = 1;
                var sim = new FarmSim(cfg, seed);
                sim.DebugSkipToWinter();
                sim.StartNextYear();
                var kind = sim.State.PlannedWeather;
                Assert.AreNotEqual(Weather.Clear, kind, "seed " + seed);
                Season? seen = null;
                sim.WeatherChanged += w => { if (w != Weather.Clear && seen == null) seen = sim.State.Season; };
                while (sim.State.Phase == Phase.Year && seen == null) sim.Tick(0.1f);
                Assert.IsNotNull(seen, "the spell came, seed " + seed);
                if (kind == Weather.Fog) Assert.AreEqual(Season.Spring, seen.Value);
                if (kind == Weather.HeatWave) Assert.AreEqual(Season.Summer, seen.Value);
            }
        }
    }
}
