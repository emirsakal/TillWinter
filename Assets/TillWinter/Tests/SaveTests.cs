using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class SaveTests
    {
        private static FarmSim NewSim(int seed = 7) => new FarmSim(new FarmConfig(), seed);

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = 0.01f)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        /// <summary>A busy mid-year sim in generation 2 with both trees, crows, apprentices and mixed plot states.</summary>
        private static FarmSim BusySim()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(6000);
            sim.DebugAddSeeds(50);
            Assert.IsTrue(sim.Retire());
            Assert.IsTrue(sim.TryBuy("h_start_field"));
            Assert.IsTrue(sim.TryBuy("h_free_apprentice"));
            sim.StartNewGeneration();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(2000);
            Assert.IsTrue(sim.TryBuy("irrigation"));
            Assert.IsTrue(sim.TryBuy("sun"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("apprentice_count"));
            sim.StartNextYear();
            // A seed-bag pick survives the round trip; picking the crop already growing keeps the timings of this fixture.
            Assert.IsTrue(sim.SetPlotCrop(new GridPos(0, 0), 1));
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Choice);
            sim.DebugSetPlotKind(new GridPos(2, 2), PlotKind.Fertile); // special ground survives too (v9)
            Run(sim, 12f, new RingInput(1, 1));
            Run(sim, 3f, null);
            sim.DebugSpawnCrow();
            Run(sim, 1.3f, null);
            // v10: a goal part-way and a heat wave under way.
            sim.DebugSetGoal(GoalType.HarvestCrop, 500, 0, 40);
            sim.DebugStartWeather(Weather.HeatWave);
            Run(sim, 0.4f, null);
            // v11: two scarecrows, one moved; a waterer (switched last, so it carries no half-done work into the save).
            sim.DebugSetLevel("scarecrow", 2);
            Assert.IsTrue(sim.MoveScarecrow(1, new GridPos(0, 0)));
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Waterer));
            // v12: a mole, a clover, the trader.
            sim.DebugSpawnPest(PestKind.Mole, new GridPos(1, 1));
            sim.DebugSpawnLucky(LuckyKind.Clover, new GridPos(0, 1));
            sim.DebugBringTrader();
            // v13: a barn taking half the harvest (the Almanac spend is already counted by the purchases above).
            sim.DebugSetLevel("barn", 1);
            Assert.IsTrue(sim.SetStoreShare(0.5f));
            return sim;
        }

        private static void AssertSameState(FarmSim a, FarmSim b)
        {
            var sa = a.State;
            var sb = b.State;
            Assert.AreEqual(sa.Phase, sb.Phase);
            Assert.AreEqual(sa.Year, sb.Year);
            Assert.AreEqual(sa.Season, sb.Season);
            Assert.AreEqual(sa.YearTime, sb.YearTime);
            Assert.AreEqual(sa.FrostWarning, sb.FrostWarning);
            Assert.AreEqual(sa.Coins, sb.Coins);
            Assert.AreEqual(sa.GridSize, sb.GridSize);
            Assert.AreEqual(sa.RingRadius, sb.RingRadius);
            var ga = sa.Generation;
            var gb = sb.Generation;
            Assert.AreEqual(ga.Generation, gb.Generation);
            Assert.AreEqual(ga.LifetimeCoinsThisGeneration, gb.LifetimeCoinsThisGeneration);
            Assert.AreEqual(ga.LifetimeCoinsTotal, gb.LifetimeCoinsTotal);
            Assert.AreEqual(ga.YearsThisGeneration, gb.YearsThisGeneration);
            Assert.AreEqual(ga.SeedsBanked, gb.SeedsBanked);
            Assert.AreEqual(ga.SeedsEarnedTotal, gb.SeedsEarnedTotal);
            Assert.AreEqual(ga.CrowsScared, gb.CrowsScared);
            Assert.AreEqual(ga.Harvests, gb.Harvests);
            Assert.AreEqual(sa.Plots.Count, sb.Plots.Count);
            for (int i = 0; i < sa.Plots.Count; i++)
            {
                Assert.AreEqual(sa.Plots[i].Pos, sb.Plots[i].Pos);
                Assert.AreEqual(sa.Plots[i].Tier, sb.Plots[i].Tier, "tier " + i);
                Assert.AreEqual(sa.Plots[i].BedTier, sb.Plots[i].BedTier, "bed " + i);
                Assert.AreEqual(sa.Plots[i].Choice, sb.Plots[i].Choice, "choice " + i);
                Assert.AreEqual(sa.Plots[i].Kind, sb.Plots[i].Kind, "kind " + i);
                Assert.AreEqual(sa.Plots[i].LastYearTier, sb.Plots[i].LastYearTier, "last year " + i);
                Assert.AreEqual(sa.Plots[i].State, sb.Plots[i].State, "state " + i);
                Assert.AreEqual(sa.Plots[i].Progress, sb.Plots[i].Progress, "progress " + i);
                Assert.AreEqual(sa.Plots[i].HasCrow, sb.Plots[i].HasCrow, "crow " + i);
                Assert.AreEqual(sa.Plots[i].IsGolden, sb.Plots[i].IsGolden, "golden " + i);
                Assert.AreEqual(sa.Plots[i].RipeAge, sb.Plots[i].RipeAge, "ripe age " + i);
                Assert.AreEqual(sa.Plots[i].DryTimer, sb.Plots[i].DryTimer, "dry timer " + i);
            }
            Assert.AreEqual(sa.Crows.Count, sb.Crows.Count);
            // A save lists crows by plot, not by landing order: compare them as a set keyed by plot.
            foreach (var ca in sa.Crows)
            {
                Crow match = null;
                foreach (var cb in sb.Crows) if (cb.Pos == ca.Pos) match = cb;
                Assert.IsNotNull(match, "crow at " + ca.Pos);
                Assert.AreEqual(ca.Timer, match.Timer);
            }
            Assert.AreEqual(sa.Apprentices.Count, sb.Apprentices.Count);
            for (int i = 0; i < sa.Apprentices.Count; i++)
            {
                Assert.AreEqual(sa.Apprentices[i].X, sb.Apprentices[i].X);
                Assert.AreEqual(sa.Apprentices[i].Y, sb.Apprentices[i].Y);
            }
            CollectionAssert.AreEquivalent(sa.AlmanacLevels, sb.AlmanacLevels);
            CollectionAssert.AreEquivalent(sa.HeritageLevels, sb.HeritageLevels);
            Assert.AreEqual(sa.Stats.ApprenticeCount, sb.Stats.ApprenticeCount);
            Assert.AreEqual(sa.Stats.TargetGridSize, sb.Stats.TargetGridSize);
            Assert.AreEqual(sa.RingShape, sb.RingShape);
            CollectionAssert.AreEqual(sa.Scarecrows, sb.Scarecrows);
            Assert.AreEqual(sa.DogCooldown, sb.DogCooldown);
            Assert.AreEqual(sa.Pest.Kind, sb.Pest.Kind);
            Assert.AreEqual(sa.Pest.Pos, sb.Pest.Pos);
            Assert.AreEqual(sa.Pest.Timer, sb.Pest.Timer);
            Assert.AreEqual(sa.Pest.Shoo, sb.Pest.Shoo);
            Assert.AreEqual(sa.HenCooldown, sb.HenCooldown);
            Assert.AreEqual(sa.Luck.CloverPos, sb.Luck.CloverPos);
            Assert.AreEqual(sa.Luck.CloverLeft, sb.Luck.CloverLeft);
            Assert.AreEqual(sa.Luck.StarLeft, sb.Luck.StarLeft);
            Assert.AreEqual(sa.Luck.RushLeft, sb.Luck.RushLeft);
            Assert.AreEqual(sa.Trader.Active, sb.Trader.Active);
            Assert.AreEqual(sa.Trader.TimeLeft, sb.Trader.TimeLeft);
            Assert.AreEqual(sa.Trader.PlannedTime, sb.Trader.PlannedTime);
            Assert.AreEqual(sa.Trader.SeedPrice, sb.Trader.SeedPrice);
            Assert.AreEqual(sa.Trader.RarePrice, sb.Trader.RarePrice);
            Assert.AreEqual(sa.Barn.Stock, sb.Barn.Stock);
            Assert.AreEqual(sa.Barn.Count, sb.Barn.Count);
            Assert.AreEqual(sa.Barn.StoreShare, sb.Barn.StoreShare);
            Assert.AreEqual(sa.Barn.StoreAcc, sb.Barn.StoreAcc);
            Assert.AreEqual(sa.Barn.MarketPrice, sb.Barn.MarketPrice);
            Assert.AreEqual(sa.Barn.Jars, sb.Barn.Jars);
            Assert.AreEqual(sa.Generation.AlmanacSpent, sb.Generation.AlmanacSpent);
            Assert.AreEqual(sa.Generation.RespecUsed, sb.Generation.RespecUsed);
            Assert.AreEqual(sa.Generation.Trait, sb.Generation.Trait);
            CollectionAssert.AreEqual(sa.Generation.HeirOffer, sb.Generation.HeirOffer);
            Assert.AreEqual(sa.Generation.Challenge, sb.Generation.Challenge);
            Assert.AreEqual(sa.Generation.Achievements, sb.Generation.Achievements);
            Assert.AreEqual(sa.Generation.GoalsMet, sb.Generation.GoalsMet);
            Assert.AreEqual(sa.Generation.PestsStopped, sb.Generation.PestsStopped);
            for (int i = 0; i < sa.Apprentices.Count; i++) Assert.AreEqual(sa.Apprentices[i].Role, sb.Apprentices[i].Role, "role " + i);
            Assert.AreEqual(sa.Goal.Type, sb.Goal.Type);
            Assert.AreEqual(sa.Goal.Tier, sb.Goal.Tier);
            Assert.AreEqual(sa.Goal.Target, sb.Goal.Target);
            Assert.AreEqual(sa.Goal.Progress, sb.Goal.Progress);
            Assert.AreEqual(sa.Goal.Done, sb.Goal.Done);
            Assert.AreEqual(sa.Goal.Reward, sb.Goal.Reward);
            Assert.AreEqual(sa.Weather, sb.Weather);
            Assert.AreEqual(sa.WeatherLeft, sb.WeatherLeft);
            Assert.AreEqual(sa.PlannedWeather, sb.PlannedWeather);
            Assert.AreEqual(sa.PlannedWeatherTime, sb.PlannedWeatherTime);
            Assert.AreEqual(sa.YearFreshSum, sb.YearFreshSum);
            Assert.AreEqual(sa.CropsLostThisYear, sb.CropsLostThisYear);
            Assert.AreEqual(sa.LastGrade, sb.LastGrade);
            Assert.AreEqual(sa.LastGradeBonus, sb.LastGradeBonus);
            Assert.AreEqual(sa.LastYearCoins, sb.LastYearCoins);
            Assert.AreEqual(sa.Combo, sb.Combo);
        }

        [Test]
        public void RoundTrip_MidYear_ReproducesStateFieldByField()
        {
            var sim = BusySim();
            Assert.AreEqual(Phase.Year, sim.State.Phase);
            Assert.That(sim.State.Crows.Count, Is.GreaterThan(0));
            var data = sim.ToSave();
            Assert.AreEqual(SaveData.CurrentSchemaVersion, data.SchemaVersion);
            var loaded = FarmSim.FromSave(data, new FarmConfig());
            Assert.IsNotNull(loaded);
            AssertSameState(sim, loaded);
        }

        [Test]
        public void RoundTrip_InWinter_AndInHeritage()
        {
            var winter = BusySim();
            winter.DebugSkipToWinter();
            var w = FarmSim.FromSave(winter.ToSave(), new FarmConfig());
            AssertSameState(winter, w);
            Assert.AreEqual(Phase.Winter, w.State.Phase);
            Assert.IsTrue(w.CanBuy("ring_radius") == winter.CanBuy("ring_radius"));

            var heritage = BusySim();
            heritage.DebugSkipToWinter();
            heritage.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(heritage.Retire());
            var h = FarmSim.FromSave(heritage.ToSave(), new FarmConfig());
            AssertSameState(heritage, h);
            Assert.AreEqual(Phase.Heritage, h.State.Phase);
            h.DebugAddSeeds(10);
            Assert.IsTrue(h.TryBuy("h_start_radius"));
            h.StartNewGeneration();
            Assert.AreEqual(Phase.Year, h.State.Phase);
        }

        [Test]
        public void UnknownSchemaVersion_ReturnsNull()
        {
            var data = BusySim().ToSave();
            data.SchemaVersion = SaveData.CurrentSchemaVersion + 1;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
            data.SchemaVersion = 0;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
            Assert.IsNull(FarmSim.FromSave(null, new FarmConfig()));
            Assert.IsNull(SaveMigrations.Migrate(new SaveData { SchemaVersion = 99 }));
        }

        [Test]
        public void MalformedPlots_ReturnNull()
        {
            var data = BusySim().ToSave();
            data.Plots = new PlotSave[2];
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
            data = BusySim().ToSave();
            data.Plots[0].X = 99;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
        }

        [Test]
        public void Determinism_SameInputsAfterLoad_SameCoins()
        {
            var a = BusySim();
            var b = FarmSim.FromSave(a.ToSave(), new FarmConfig());
            for (int i = 0; i < 4000; i++)
            {
                var ring = i % 700 < 400 ? new RingInput(1 + (i / 200) % 2, 1) : (RingInput?)null;
                a.Tick(0.01f, ring);
                b.Tick(0.01f, ring);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins);
            Assert.AreEqual(a.State.Crows.Count, b.State.Crows.Count);
            AssertSameState(a, b);
        }

        [Test]
        public void SaveData_IsPlainArrays_NoDictionaries()
        {
            var data = BusySim().ToSave();
            Assert.That(data.AlmanacLevels.Length, Is.GreaterThan(0));
            Assert.That(data.HeritageLevels.Length, Is.GreaterThan(0));
            foreach (var f in typeof(SaveData).GetFields())
                Assert.IsFalse(typeof(System.Collections.IDictionary).IsAssignableFrom(f.FieldType), f.Name + " must not be a dictionary");
        }

        // ---------------------------------------------------------------- offline (GDD §9)

        [Test]
        public void Offline_NothingWithoutPassiveChain_AndNoOpOutsideYear()
        {
            var sim = NewSim();
            var r = sim.SimulateOffline(3600);
            Assert.AreEqual(0, r.CoinsEarned);
            Assert.AreEqual(0, r.Harvests);
            Assert.That(r.SecondsSimulated, Is.EqualTo(3600).Within(1));
            foreach (var p in sim.State.Plots) Assert.AreEqual(PlotState.Dry, p.State);

            sim.DebugSkipToWinter();
            var w = sim.SimulateOffline(3600);
            Assert.AreEqual(0, w.SecondsSimulated);
            Assert.AreEqual(0, w.CoinsEarned);
            Assert.AreEqual(0, sim.SimulateOffline(-5).SecondsSimulated);
        }

        [Test]
        public void Offline_PassiveChainEarns_MatchesDirectSimulation_YearTimerUnchanged()
        {
            FarmSim Make()
            {
                var s = NewSim(3);
                s.DebugSetLevel("irrigation", 3);
                s.DebugSetLevel("sun", 3);
                s.DebugSetLevel("apprentice_count", 2);
                Run(s, 5f, null);
                return s;
            }

            var offline = Make();
            float yearTime = offline.State.YearTime;
            var season = offline.State.Season;
            var report = offline.SimulateOffline(600);
            Assert.That(report.SecondsSimulated, Is.EqualTo(600).Within(1));
            Assert.IsFalse(report.Capped);
            Assert.That(report.CoinsEarned, Is.GreaterThan(0));
            Assert.That(report.Harvests, Is.GreaterThan(0));
            Assert.AreEqual(yearTime, offline.State.YearTime, "year timer frozen");
            Assert.AreEqual(season, offline.State.Season, "season unchanged");
            Assert.AreEqual(Phase.Year, offline.State.Phase);
            Assert.AreEqual(0, offline.State.Crows.Count, "no crows offline");

            // Direct simulation of the same passive systems at the same step (year timer would run, so compare coins only).
            var direct = Make();
            direct.DebugSetLevel("year_length", 6); // 180 s; still shorter than 600 -> use a config with a long year instead
            var cfgLong = new FarmConfig { BaseYearLength = 100000f, MaxYearLength = 100000f, CrowFirstYear = 99 };
            var direct2 = new FarmSim(cfgLong, 3);
            direct2.DebugSetLevel("irrigation", 3);
            direct2.DebugSetLevel("sun", 3);
            direct2.DebugSetLevel("apprentice_count", 2);
            Run(direct2, 5f, null);
            double before = direct2.State.Coins;
            for (int i = 0; i < 600; i++) direct2.Tick(1f, null);
            Assert.That(report.CoinsEarned, Is.EqualTo(direct2.State.Coins - before).Within(1e-6));
        }

        [Test]
        public void Offline_CapsAtEightHours()
        {
            var sim = NewSim();
            sim.DebugSetLevel("irrigation", 1);
            var r = sim.SimulateOffline(20 * 3600);
            Assert.IsTrue(r.Capped);
            Assert.That(r.SecondsSimulated, Is.EqualTo(8 * 3600).Within(1));
            var small = new FarmSim(new FarmConfig { OfflineCapSeconds = 100, OfflineStepSeconds = 1f }, 1);
            var r2 = small.SimulateOffline(1000);
            Assert.IsTrue(r2.Capped);
            Assert.AreEqual(100, r2.SecondsSimulated);
        }
    }
}
