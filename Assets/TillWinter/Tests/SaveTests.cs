using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class SaveTests
    {
        private static FarmSim NewSim(int seed = 7) => new FarmSim(new FarmConfig(), seed);

        private static void Run(FarmSim sim, float seconds, float dt = 0.01f)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt);
        }

        /// <summary>A busy mid-year sim in generation 2 with both trees, crows, apprentices and every plot state.</summary>
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
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            Assert.IsTrue(sim.TryBuy("growth"));
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("apprentice_count"));
            sim.StartNextYear();
            // The two apprentices dig and water (a picker would reap the crow's crop before the save).
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Digger));
            Assert.IsTrue(sim.SetApprenticeRole(1, ApprenticeRole.Waterer));
            // A seed-bag pick survives the round trip.
            Assert.IsTrue(sim.SetPlotCrop(new GridPos(0, 0), 1));
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Choice);
            sim.DebugSetPlotKind(new GridPos(2, 2), PlotKind.Fertile); // special ground survives too
            // Cracks, a growing crop (the tomato: a carrot would ripen before the save), a ripe one, a deep layer, a
            // watered plot, half a depot.
            sim.Strike(new GridPos(0, 1));
            sim.DebugBreak(new GridPos(0, 0));
            sim.DebugSetLayer(new GridPos(2, 1), 5);
            sim.DebugBreak(new GridPos(2, 0));
            sim.DebugForceRipe(new GridPos(2, 0));
            sim.SetWatering(new GridPos(0, 0));
            Run(sim, 0.5f);
            sim.SetWatering(null);
            sim.DebugSetStamina(41f);
            sim.DebugSpawnCrow();
            Run(sim, 1.3f);
            // A goal part-way and a heat wave under way.
            sim.DebugSetGoal(GoalType.HarvestCrop, 500, 0, 40);
            sim.DebugStartWeather(Weather.HeatWave);
            Run(sim, 0.4f);
            // Two scarecrows, one moved; the roles switched last, so no apprentice carries half-done work into the save.
            sim.DebugSetLevel("scarecrow", 2);
            Assert.IsTrue(sim.MoveScarecrow(1, new GridPos(0, 0)));
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Picker));
            Assert.IsTrue(sim.SetApprenticeRole(0, ApprenticeRole.Digger));
            Assert.IsTrue(sim.SetApprenticeRole(1, ApprenticeRole.Picker));
            Assert.IsTrue(sim.SetApprenticeRole(1, ApprenticeRole.Waterer));
            // A mole, a clover, the trader.
            sim.DebugSpawnPest(PestKind.Mole, new GridPos(1, 2));
            sim.DebugSpawnLucky(LuckyKind.Clover, new GridPos(0, 1));
            sim.DebugBringTrader();
            // A barn taking half the harvest (the Almanac spend is already counted by the purchases above).
            sim.DebugSetLevel("barn", 1);
            Assert.IsTrue(sim.SetStoreShare(0.5f));
            sim.SetAwayPlan(AwayPlan.Balanced);
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
            Assert.AreEqual(sa.Stamina, sb.Stamina, 1e-6f);
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
            Assert.AreEqual(ga.HarvestsHand, gb.HarvestsHand);
            Assert.AreEqual(ga.Strikes, gb.Strikes);
            Assert.AreEqual(ga.Crits, gb.Crits);
            Assert.AreEqual(ga.Breaks, gb.Breaks);
            Assert.AreEqual(ga.DeepestLayer, gb.DeepestLayer);
            Assert.AreEqual(ga.BestCombo, gb.BestCombo);
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
                Assert.AreEqual(sa.Plots[i].Layer, sb.Plots[i].Layer, "layer " + i);
                Assert.AreEqual(sa.Plots[i].Ground, sb.Plots[i].Ground, "ground " + i);
                Assert.AreEqual(sa.Plots[i].Hardpan, sb.Plots[i].Hardpan, "hardpan " + i);
                Assert.AreEqual(sa.Plots[i].Chest, sb.Plots[i].Chest, "chest " + i);
                Assert.AreEqual(sa.Plots[i].Hp, sb.Plots[i].Hp, 1e-9, "hp " + i);
                Assert.AreEqual(sa.Plots[i].MaxHp, sb.Plots[i].MaxHp, 1e-9, "max hp " + i);
                Assert.AreEqual(sa.Plots[i].CropLayer, sb.Plots[i].CropLayer, "crop layer " + i);
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
                Assert.AreEqual(sa.Apprentices[i].Role, sb.Apprentices[i].Role, "role " + i);
            }
            CollectionAssert.AreEquivalent(sa.AlmanacLevels, sb.AlmanacLevels);
            CollectionAssert.AreEquivalent(sa.HeritageLevels, sb.HeritageLevels);
            Assert.AreEqual(sa.Stats.ApprenticeCount, sb.Stats.ApprenticeCount);
            Assert.AreEqual(sa.Stats.TargetGridSize, sb.Stats.TargetGridSize);
            Assert.AreEqual(sa.Stats.StrikeDamage, sb.Stats.StrikeDamage);
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
            Assert.AreEqual(sa.Generation.BestGradeThisGeneration, sb.Generation.BestGradeThisGeneration);
            Assert.AreEqual(sa.Generation.HarvestsAtGenerationStart, sb.Generation.HarvestsAtGenerationStart);
            Assert.AreEqual(sa.NgPlus, sb.NgPlus);
            Assert.AreEqual(sa.ChecklistBits, sb.ChecklistBits);
            Assert.AreEqual(sa.AwayPlan, sb.AwayPlan);
            Assert.AreEqual(sa.Album.Count, sb.Album.Count);
            for (int i = 0; i < sa.Album.Count; i++)
            {
                Assert.AreEqual(sa.Album[i].Generation, sb.Album[i].Generation);
                Assert.AreEqual(sa.Album[i].Years, sb.Album[i].Years);
                Assert.AreEqual(sa.Album[i].Coins, sb.Album[i].Coins);
                Assert.AreEqual(sa.Album[i].Seeds, sb.Album[i].Seeds);
                Assert.AreEqual(sa.Album[i].Harvests, sb.Album[i].Harvests);
                Assert.AreEqual(sa.Album[i].Trait, sb.Album[i].Trait);
            }
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
            bool hard = false, growing = false, ripe = false;
            foreach (var p in sim.State.Plots) { hard |= p.IsHard; growing |= p.IsGrowing; ripe |= p.IsRipe; }
            Assert.IsTrue(hard && growing && ripe, "every plot state is in the fixture");
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
            Assert.IsTrue(w.CanBuy("hoe_damage") == winter.CanBuy("hoe_damage"));

            var heritage = BusySim();
            heritage.DebugSkipToWinter();
            heritage.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(heritage.Retire());
            var h = FarmSim.FromSave(heritage.ToSave(), new FarmConfig());
            AssertSameState(heritage, h);
            Assert.AreEqual(Phase.Heritage, h.State.Phase);
            h.DebugAddSeeds(10);
            Assert.IsTrue(h.TryBuy("h_start_damage"));
            h.StartNewGeneration();
            Assert.AreEqual(Phase.Year, h.State.Phase);
        }

        [Test]
        public void UnknownSchemaVersion_ReturnsNull_AndSoDoesTheRingGamesSave()
        {
            var data = BusySim().ToSave();
            data.SchemaVersion = SaveData.CurrentSchemaVersion + 1;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
            data.SchemaVersion = 0;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
            data.SchemaVersion = 17; // the last save of the ring game: a different field, a different Almanac (v3 decision)
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
            var path = new List<GridPos>();
            for (int i = 0; i < 4000; i++)
            {
                var pos = a.State.Plots[i % a.State.Plots.Count].Pos;
                if (i % 40 == 0) { a.Strike(pos); b.Strike(pos); }
                if (i % 300 == 0)
                {
                    path.Clear();
                    foreach (var p in a.State.Plots) path.Add(p.Pos);
                    a.Reap(path);
                    b.Reap(path);
                }
                var water = i % 700 < 400 ? (GridPos?)a.State.Plots[(i / 200) % 4].Pos : null;
                a.SetWatering(water);
                b.SetWatering(water);
                a.Tick(0.01f);
                b.Tick(0.01f);
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

        // ---------------------------------------------------------------- offline (GDD §9, §2v3.10)

        [Test]
        public void Offline_NothingGrowsOnHardGround_AndNoOpOutsideYear()
        {
            var sim = NewSim();
            var r = sim.SimulateOffline(3600);
            Assert.AreEqual(0, r.CoinsEarned);
            Assert.AreEqual(0, r.Harvests);
            Assert.That(r.SecondsSimulated, Is.EqualTo(3600).Within(1));
            foreach (var p in sim.State.Plots) Assert.AreEqual(PlotState.Hard, p.State);

            sim.DebugSkipToWinter();
            var w = sim.SimulateOffline(3600);
            Assert.AreEqual(0, w.SecondsSimulated);
            Assert.AreEqual(0, w.CoinsEarned);
            Assert.AreEqual(0, sim.SimulateOffline(-5).SecondsSimulated);
        }

        [Test]
        public void Offline_HelpersEarn_MatchesDirectSimulation_YearTimerUnchanged()
        {
            FarmSim Make(FarmConfig cfg)
            {
                var s = new FarmSim(cfg, 3);
                s.DebugSetLevel("apprentice_count", 3);
                s.SetApprenticeRole(1, ApprenticeRole.Digger);
                s.SetApprenticeRole(2, ApprenticeRole.Waterer);
                s.DebugBreakAll();
                Run(s, 5f);
                return s;
            }

            // Ripe crops do not age while the game is closed (GDD §9), so both farms run without over-ripening: with it
            // the direct sim's waiting crops would pay less than the frozen offline ones.
            var offline = Make(new FarmConfig { RipeGraceSeconds = 1e9f });
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

            // Direct simulation of the same passive systems at the same step, on a year too long to end.
            var cfgLong = new FarmConfig { BaseYearLength = 100000f, MaxYearLength = 100000f, CrowFirstYear = 99, WeatherFirstYear = 99, PestFirstYear = 99, LuckyFirstYear = 99, TraderFirstYear = 99, RipeGraceSeconds = 1e9f };
            var direct = Make(cfgLong);
            double before = direct.State.Coins;
            for (int i = 0; i < 600; i++) direct.Tick(1f);
            Assert.That(report.CoinsEarned, Is.EqualTo(direct.State.Coins - before).Within(1e-6));
        }

        [Test]
        public void Offline_CapsAtEightHours()
        {
            var sim = NewSim();
            var r = sim.SimulateOffline(20 * 3600);
            Assert.IsTrue(r.Capped);
            Assert.That(r.SecondsSimulated, Is.EqualTo(8 * 3600).Within(1));
            var small = new FarmSim(new FarmConfig { OfflineCapSeconds = 100, OfflineStepSeconds = 1f }, 1);
            var r2 = small.SimulateOffline(1000);
            Assert.IsTrue(r2.Capped);
            Assert.AreEqual(100, r2.SecondsSimulated);
        }
    
        [Test]
        public void V19_ErrandsCooldownAndGreenhouseRate_SurviveALoad_SoTheFarmPlaysOnIdentically()
        {
            // v19: an apprentice bent over a plot, a swing still settling and this winter's greenhouse rate are saved.
            // Without them a loaded farm restarted its helpers idle, regenerated stamina at the rested rate and
            // recomputed the greenhouse from today's field, and drifted from the farm that was saved within seconds.
            var a = BusySim();
            // Catch a digger mid-errand and a swing mid-cooldown.
            for (int i = 0; i < 4000; i++)
            {
                a.Tick(0.01f);
                bool errand = false;
                foreach (var ap in a.State.Apprentices) if (ap.IsWorking && ap.WorkProgress > 0.2f) errand = true;
                if (errand) break;
            }
            bool caught = false;
            foreach (var ap in a.State.Apprentices) if (ap.IsWorking) caught = true;
            Assert.IsTrue(caught, "an apprentice is working when the save is taken");
            foreach (var p in a.State.Plots) if (p.IsHard && a.CanStrike) { a.Strike(p.Pos); break; }
            Assert.That(a.State.StrikeCooldownLeft, Is.GreaterThan(0f), "a swing is settling when the save is taken");

            var data = a.ToSave();
            Assert.AreEqual(19, data.SchemaVersion);
            Assert.That(data.StrikeCooldownLeft, Is.GreaterThan(0f));
            bool savedErrand = false;
            foreach (var ap in data.Apprentices) if (ap.IsWorking && ap.WorkProgress > 0f) savedErrand = true;
            Assert.IsTrue(savedErrand, "the errand is in the save");
            var b = FarmSim.FromSave(data, new FarmConfig());
            for (int i = 0; i < a.State.Apprentices.Count; i++)
            {
                Assert.AreEqual(a.State.Apprentices[i].IsWorking, b.State.Apprentices[i].IsWorking, "apprentice " + i + " working");
                Assert.AreEqual(a.State.Apprentices[i].HasTarget, b.State.Apprentices[i].HasTarget, "apprentice " + i + " target");
                Assert.AreEqual(a.State.Apprentices[i].WorkProgress, b.State.Apprentices[i].WorkProgress, 1e-6f, "apprentice " + i + " progress");
            }
            Assert.AreEqual(a.State.StrikeCooldownLeft, b.State.StrikeCooldownLeft, 1e-6f);
            for (int i = 0; i < 3000; i++) { a.Tick(0.01f); b.Tick(0.01f); }
            AssertSameState(a, b);
            for (int i = 0; i < a.State.Plots.Count; i++)
            {
                Assert.AreEqual(a.State.Plots[i].State, b.State.Plots[i].State, "plot " + i + " state after 30 s");
                Assert.AreEqual(a.State.Plots[i].Hp, b.State.Plots[i].Hp, 1e-6, "plot " + i + " hp after 30 s");
            }
        }

        [Test]
        public void V19_GreenhouseRate_IsTheSavedOne_NotRecomputedFromTodaysField()
        {
            var a = BusySim();
            a.DebugAddCoins(50000);
            a.DebugSkipToWinter();
            Assert.IsTrue(a.TryBuy("year_length"));
            Assert.IsTrue(a.TryBuy("greenhouse"), "a greenhouse for this winter");
            double rate = a.State.Greenhouse.CoinsPerSecond;
            Assert.That(rate, Is.GreaterThan(0));
            // The frost reaps what is ripe as the winter runs, so today's field is worth less than at the purchase.
            for (int i = 0; i < 300; i++) a.Tick(0.01f);
            var b = FarmSim.FromSave(a.ToSave(), new FarmConfig());
            Assert.AreEqual(rate, b.State.Greenhouse.CoinsPerSecond, 1e-9, "the loaded winter earns at the saved rate");
            for (int i = 0; i < 2000; i++) { a.Tick(0.01f); b.Tick(0.01f); }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-6);
        }

        [Test]
        public void V18Save_Migrates_ToV19_WithSettledDefaults()
        {
            var data = BusySim().ToSave();
            data.SchemaVersion = 18;
            data.StrikeCooldownLeft = 0.3f; // a v18 file never carries these; the migration must not invent them either
            var migrated = SaveMigrations.Migrate(data, new FarmConfig());
            Assert.IsNotNull(migrated);
            Assert.AreEqual(19, migrated.SchemaVersion);
            var sim = FarmSim.FromSave(migrated, new FarmConfig());
            Assert.IsNotNull(sim);
            Assert.AreEqual(Phase.Year, sim.State.Phase);
        }
    }
}
