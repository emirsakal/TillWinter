using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.3, seasons, year and weather (GDD §3/§5.4 v1.9): spring rain, summer drought, the autumn festival, the frost
    /// rush, the year's grade, yearly goals, and storms, heat waves and fog.
    /// </summary>
    public class SeasonYearTests
    {
        /// <summary>Only the rule under test moves: no crop preferences, no weather or goals drawn, no special ground.</summary>
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.InSeasonValue = 1;
            cfg.StonyChance = cfg.FertileChance = 0;
            cfg.WeatherFirstYear = int.MaxValue;
            cfg.GoalFirstYear = int.MaxValue;
            return cfg;
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring)
        {
            for (float t = 0f; t < seconds; t += 0.05f) sim.Tick(0.05f, ring);
        }

        /// <summary>Coins from one fresh tap-harvest of plot (0,0).</summary>
        private static double TapHarvest(FarmSim sim)
        {
            sim.DebugForceRipeAll();
            sim.DebugClearTapCooldown();
            double before = sim.State.Coins;
            Assert.IsTrue(sim.TapAt(new GridPos(0, 0)));
            return sim.State.Coins - before;
        }

        [Test]
        public void SpringRain_WatersFaster()
        {
            var spring = new FarmSim(Cfg(), 1);
            var cfgOff = Cfg();
            cfgOff.SpringWaterBoost = 1f;
            var plain = new FarmSim(cfgOff, 1);
            spring.DebugSetLevel("irrigation", 1);
            plain.DebugSetLevel("irrigation", 1);
            Run(spring, 0.5f, null);
            Run(plain, 0.5f, null);
            float a = spring.State.GetPlot(0, 0).Progress, b = plain.State.GetPlot(0, 0).Progress;
            Assert.That(b, Is.GreaterThan(0f));
            Assert.AreEqual(b * Cfg().SpringWaterBoost, a, 1e-3);
        }

        [Test]
        public void SummerDrought_DriesANeglectedWetPlot_ButNotOneTheSunGrows()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetSeason(Season.Summer);
            var at = new GridPos(0, 0);
            var plot = sim.State.GetPlot(at);
            while (plot.State == PlotState.Dry) sim.Tick(0.05f, new RingInput(0f, 0f));
            Assert.AreEqual(PlotState.Wet, plot.State);
            int dried = 0;
            sim.PlotDriedOut += p => dried++;
            Run(sim, sim.Config.SummerDryOutSeconds - 0.5f, null);
            Assert.AreEqual(PlotState.Wet, plot.State, "not yet");
            Run(sim, 1f, null);
            Assert.AreEqual(PlotState.Dry, plot.State, "dried out in the heat");
            Assert.That(dried, Is.GreaterThan(0));

            var sunny = new FarmSim(Cfg(), 1);
            sunny.DebugSetLevel("sun", 1);
            sunny.DebugSetSeason(Season.Summer);
            var p2 = sunny.State.GetPlot(at);
            while (p2.State == PlotState.Dry) sunny.Tick(0.05f, new RingInput(0f, 0f));
            Run(sunny, sunny.Config.SummerDryOutSeconds + 1f, null);
            Assert.AreNotEqual(PlotState.Dry, p2.State, "the sun keeps it growing");
        }

        [Test]
        public void TheAutumnFestival_GivesTheComboLongerToBreathe()
        {
            int ComboAfterTwo(Season season)
            {
                var sim = new FarmSim(Cfg(), 1);
                sim.DebugSetLevel("tap_harvest", 2);
                if (season != Season.Spring) sim.DebugSetSeason(season);
                sim.Tick(0.01f, null);
                TapHarvest(sim);
                Run(sim, 1.2f, null); // longer than the plain window, inside the festival's
                TapHarvest(sim);
                return sim.State.Combo;
            }

            Assert.AreEqual(1, ComboAfterTwo(Season.Spring));
            Assert.AreEqual(2, ComboAfterTwo(Season.Autumn));
        }

        [Test]
        public void TheFrostRush_PaysDouble()
        {
            var spring = new FarmSim(Cfg(), 1);
            spring.DebugSetLevel("tap_harvest", 2);
            double normal = TapHarvest(spring);

            var frost = new FarmSim(Cfg(), 1);
            frost.DebugSetLevel("tap_harvest", 2);
            frost.DebugSetSeason(Season.Autumn);
            frost.Tick(0.01f, null);
            Assert.IsTrue(frost.State.FrostWarning);
            Assert.AreEqual(normal * frost.Config.FrostRushValue, TapHarvest(frost), 1e-9);
        }

        [Test]
        public void AYearOfFreshHarvests_EarnsThreeStars_AndTheirBonus()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("tap_harvest", 2);
            for (int i = 0; i < 5; i++) TapHarvest(sim);
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
            sim.DebugSetLevel("tap_harvest", 2);
            sim.DebugForceRipeAll();
            Run(sim, sim.Config.RipeGraceSeconds + sim.Config.OverripeDecaySeconds, null);
            sim.DebugClearTapCooldown();
            Assert.IsTrue(sim.TapAt(new GridPos(0, 0)));
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
            sim.DebugSetLevel("tap_harvest", 2);
            for (int i = 0; i < 4; i++) TapHarvest(sim);
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
            sim.DebugSetLevel("tap_harvest", 2);
            sim.DebugSetGoal(GoalType.HarvestCrop, 2, 0, 50);
            int done = 0;
            sim.GoalCompleted += g => done++;
            double first = TapHarvest(sim);
            Assert.IsFalse(sim.State.Goal.Done);
            double second = TapHarvest(sim);
            Assert.IsTrue(sim.State.Goal.Done);
            Assert.AreEqual(first + 50, second, 1e-9, "the reward lands with the harvest that meets it");
            TapHarvest(sim);
            Assert.AreEqual(1, done);
        }

        [Test]
        public void ComboAndCoinGoals_TrackTheirOwnNumbers()
        {
            var combo = new FarmSim(Cfg(), 1);
            combo.DebugSetLevel("tap_harvest", 2);
            combo.DebugSetGoal(GoalType.Combo, 3);
            for (int i = 0; i < 3; i++) TapHarvest(combo);
            Assert.IsTrue(combo.State.Goal.Done, "three quick harvests make a combo of three");

            var coins = new FarmSim(Cfg(), 1);
            coins.DebugSetLevel("tap_harvest", 2);
            coins.DebugSetGoal(GoalType.Coins, 5);
            while (!coins.State.Goal.Done && coins.State.CoinsThisYear < 100) TapHarvest(coins);
            Assert.IsTrue(coins.State.Goal.Done);
        }

        [Test]
        public void AStorm_StopsTheSun_WatersEveryDryPlot_AndPasses()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugSetLevel("sun", 1);
            var dry = sim.State.GetPlot(2, 2);
            var wet = sim.State.GetPlot(0, 0);
            while (wet.State == PlotState.Dry) sim.Tick(0.05f, new RingInput(0f, 0f));
            float grown = wet.Progress;
            Weather last = Weather.Clear;
            sim.WeatherChanged += w => last = w;
            sim.DebugStartWeather(Weather.Storm);
            Run(sim, 1f, null);
            Assert.AreEqual(grown, wet.Progress, 1e-6, "no sun in a storm");
            Assert.That(dry.Progress, Is.GreaterThan(0f), "the rain waters without irrigation");
            Run(sim, sim.Config.StormSeconds, null);
            Assert.AreEqual(Weather.Clear, sim.State.Weather);
            Assert.AreEqual(Weather.Clear, last);
        }

        [Test]
        public void Fog_KeepsRipeCropsFresh()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            sim.DebugStartWeather(Weather.Fog);
            Run(sim, sim.Config.FogSeconds - 1f, null);
            Assert.AreEqual(0f, sim.State.GetPlot(0, 0).RipeAge, 1e-6);
        }

        [Test]
        public void AHeatWave_StrengthensTheSun()
        {
            var hot = new FarmSim(Cfg(), 1);
            var mild = new FarmSim(Cfg(), 1);
            foreach (var s in new[] { hot, mild })
            {
                s.DebugSetLevel("sun", 1);
                while (s.State.GetPlot(0, 0).State == PlotState.Dry) s.Tick(0.05f, new RingInput(0f, 0f));
            }
            hot.DebugStartWeather(Weather.HeatWave);
            float h0 = hot.State.GetPlot(0, 0).Progress, m0 = mild.State.GetPlot(0, 0).Progress;
            Run(hot, 0.5f, null);
            Run(mild, 0.5f, null);
            float h = hot.State.GetPlot(0, 0).Progress - h0, m = mild.State.GetPlot(0, 0).Progress - m0;
            Assert.AreEqual(m * hot.Config.HeatWaveSun, h, 1e-3);
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
                while (sim.State.Phase == Phase.Year && seen == null) sim.Tick(0.1f, null);
                Assert.IsNotNull(seen, "the spell came, seed " + seed);
                if (kind == Weather.Fog) Assert.AreEqual(Season.Spring, seen.Value);
                if (kind == Weather.HeatWave) Assert.AreEqual(Season.Summer, seen.Value);
            }
        }
    }
}
