using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class FarmSimTests
    {
        private const float Dt = 0.01f;

        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = new FarmConfig();
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        /// <summary>Skips to Winter, buys an upgrade n times with unlimited coins, starts the next year.</summary>
        private static void Grant(FarmSim sim, UpgradeId id, int times = 1)
        {
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e12);
            for (int i = 0; i < times; i++)
                Assert.IsTrue(sim.TryBuy(id), "buy " + id + " #" + (i + 1));
            sim.DebugAddCoins(-sim.State.Coins); // leave the sim broke again
        }

        private static float TimeToFirstHarvest(FarmSim sim, GridPos pos, RingInput? ring, float maxSeconds = 60f)
        {
            float t = -1f;
            float elapsed = 0f;
            sim.Harvested += e =>
            {
                if (t < 0f && e.Pos == pos) t = elapsed;
            };
            while (t < 0f && elapsed < maxSeconds)
            {
                sim.Tick(Dt, ring);
                elapsed += Dt;
            }
            return t;
        }

        // ---------------------------------------------------------------- growth

        [Test]
        public void Carrot_RipensIn3Seconds_UnderRing_AtSoil0()
        {
            var sim = NewSim();
            float t = TimeToFirstHarvest(sim, new GridPos(1, 1), new RingInput(1, 1));
            Assert.That(t, Is.EqualTo(3f).Within(0.05f));
        }

        [Test]
        public void Carrot_RipensIn2Point4Seconds_AtSoil1()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, UpgradeId.Soil);
            sim.StartNextYear();
            float t = TimeToFirstHarvest(sim, new GridPos(1, 1), new RingInput(1, 1));
            Assert.That(t, Is.EqualTo(2.4f).Within(0.05f));
        }

        [Test]
        public void NoGrowth_OutsideRing_AtIrrigation0()
        {
            var sim = NewSim();
            Run(sim, 10f, null);
            foreach (var p in sim.State.Plots) Assert.AreEqual(0f, p.Growth);
            Assert.AreEqual(0, sim.State.Coins);
        }

        [Test]
        public void Irrigation2_GrowsAt30PercentOfRingSpeed_OutsideRing()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, UpgradeId.Irrigation, 2);
            sim.StartNextYear();
            Run(sim, 5f, null);
            // ring speed for carrot = 1/3 per s; natural = 0.3 * 1/3 = 0.1 per s -> 0.5 after 5 s
            foreach (var p in sim.State.Plots)
                Assert.That(p.Growth, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void RipePlotUnderRing_HarvestsExactlyOnce_AndReplantsAtZero()
        {
            var sim = NewSim();
            sim.DebugSetRingRadiusOverride(0.5f); // only the plot under the centre
            var pos = new GridPos(0, 0);
            var events = new List<HarvestEvent>();
            sim.Harvested += events.Add;

            Run(sim, 2.9f, new RingInput(0, 0));
            Assert.AreEqual(0, events.Count);
            Assert.IsFalse(sim.State.GetPlot(pos).IsRipe);

            Run(sim, 0.2f, new RingInput(0, 0));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(pos, events[0].Pos);
            Assert.AreEqual(CropTier.Carrot, events[0].Tier);
            Assert.AreEqual(1, events[0].Coins);
            Assert.AreEqual(HarvestSource.Ring, events[0].Source);
            Assert.AreEqual(1, sim.State.Coins);
            Assert.That(sim.State.GetPlot(pos).Growth, Is.LessThan(0.1f));
            foreach (var p in sim.State.Plots)
                if (p.Pos != pos) Assert.AreEqual(0f, p.Growth, "other plots untouched");
        }

        [Test]
        public void RipePlotOutsideRing_Waits_ThenHarvestsWhenRingArrives()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, UpgradeId.Irrigation, 3); // 0.45x ring speed -> carrot ripe in 6.67 s
            sim.StartNextYear();
            Run(sim, 8f, null);
            Assert.IsTrue(sim.State.GetPlot(2, 2).IsRipe);
            Assert.AreEqual(0, sim.State.Coins);
            int harvests = 0;
            sim.Harvested += _ => harvests++;
            sim.Tick(0f, new RingInput(1, 1)); // zero-dt "tap frame" still harvests
            Assert.AreEqual(9, harvests);
            Assert.AreEqual(9, sim.State.Coins);
        }

        // ---------------------------------------------------------------- year

        [Test]
        public void Winter_StartsAt90Seconds()
        {
            var sim = NewSim();
            bool winter = false;
            sim.WinterStarted += () => winter = true;
            Run(sim, 89.9f, null);
            Assert.IsFalse(winter);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Assert.IsTrue(sim.State.FrostWarning);
            Run(sim, 0.2f, null);
            Assert.IsTrue(winter);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void Winter_StartsAt105Seconds_WithOneCalendarLevel()
        {
            var sim = NewSim();
            Grant(sim, UpgradeId.Calendar);
            sim.StartNextYear();
            Assert.AreEqual(2, sim.State.Year);
            Assert.AreEqual(105f, sim.State.YearLength);
            Run(sim, 104.9f, null);
            Assert.IsFalse(sim.State.IsWinter);
            Run(sim, 0.2f, null);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void Seasons_ChangeAtThirds_AndFrostWarningInLast10Seconds()
        {
            var sim = NewSim();
            var seasons = new List<Season>();
            sim.SeasonChanged += seasons.Add;
            bool frost = false;
            sim.FrostWarningStarted += () => frost = true;
            Run(sim, 29.9f, null);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Run(sim, 0.2f, null);
            Assert.AreEqual(Season.Summer, sim.State.Season);
            Run(sim, 30f, null);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Assert.IsFalse(frost);
            Run(sim, 20f, null); // t = 80.1
            Assert.IsTrue(frost);
            Assert.IsTrue(sim.State.FrostWarning);
            Run(sim, 10f, null);
            CollectionAssert.AreEqual(new[] { Season.Summer, Season.Autumn, Season.Winter }, seasons);
        }

        [Test]
        public void Winter_ClearsUnharvestedCrops_ButKeepsCoins()
        {
            var sim = NewSim();
            sim.DebugSetRingRadiusOverride(0.5f);
            Run(sim, 2.9f, new RingInput(0, 0)); // almost ripe, not harvested
            Run(sim, 3.1f, new RingInput(2, 2)); // harvested once -> 1 coin
            Assert.AreEqual(1, sim.State.Coins);
            Assert.That(sim.State.GetPlot(0, 0).Growth, Is.GreaterThan(0.9f));

            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.State.IsWinter);
            Assert.AreEqual(1, sim.State.Coins);
            foreach (var p in sim.State.Plots) Assert.AreEqual(0f, p.Growth);

            // Frozen: ring does nothing, timer does not move.
            Run(sim, 5f, new RingInput(1, 1));
            Assert.AreEqual(1, sim.State.Coins);
            Assert.IsNull(sim.State.Ring);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void StartNextYear_IncrementsYear_ReplantsSameTiers_StartsSpring()
        {
            var sim = NewSim();
            Grant(sim, UpgradeId.UpgradePlot, 2);
            Assert.AreEqual(CropTier.Tomato, sim.State.GetPlot(0, 0).Tier);
            Assert.AreEqual(CropTier.Tomato, sim.State.GetPlot(1, 0).Tier);
            sim.StartNextYear();
            Assert.AreEqual(2, sim.State.Year);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Assert.AreEqual(0f, sim.State.YearTime);
            Assert.AreEqual(CropTier.Tomato, sim.State.GetPlot(0, 0).Tier);
            Assert.AreEqual(CropTier.Carrot, sim.State.GetPlot(2, 2).Tier);
            foreach (var p in sim.State.Plots) Assert.AreEqual(0f, p.Growth);
        }

        // ---------------------------------------------------------------- shop

        [Test]
        public void Shop_RejectsPurchases_OutsideWinter()
        {
            var sim = NewSim();
            sim.DebugAddCoins(100000);
            Assert.IsFalse(sim.CanBuy(UpgradeId.Soil));
            Assert.IsFalse(sim.TryBuy(UpgradeId.Soil));
            Assert.AreEqual(0, sim.State.GetLevel(UpgradeId.Soil));
            Assert.AreEqual(100000, sim.State.Coins);
        }

        [Test]
        public void Shop_RejectsUnaffordable_AndDeductsCost()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(24);
            Assert.IsFalse(sim.TryBuy(UpgradeId.Soil)); // costs 25
            sim.DebugAddCoins(1);
            Assert.IsTrue(sim.TryBuy(UpgradeId.Soil));
            Assert.AreEqual(0, sim.State.Coins);
            Assert.AreEqual(1, sim.State.GetLevel(UpgradeId.Soil));
        }

        [Test]
        public void Shop_CostCurve_Matches1Point6PowN()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            for (int n = 0; n < 5; n++)
            {
                double expected = Math.Round(25 * Math.Pow(1.6, n));
                Assert.AreEqual(expected, sim.GetCost(UpgradeId.Soil), "cost at level " + n);
                Assert.IsTrue(sim.TryBuy(UpgradeId.Soil));
            }
            Assert.IsTrue(sim.IsMaxed(UpgradeId.Soil));
            Assert.IsFalse(sim.TryBuy(UpgradeId.Soil));
            Assert.AreEqual(60, sim.GetCost(UpgradeId.ExpandField));
            Assert.AreEqual(15, sim.GetCost(UpgradeId.UpgradePlot));
        }

        [Test]
        public void UpgradePlot_PicksLowestTier_RowMajorTies_AndMaxesWhenAllCorn()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e12);

            Assert.IsTrue(sim.TryBuy(UpgradeId.UpgradePlot));
            Assert.AreEqual(CropTier.Tomato, sim.State.GetPlot(0, 0).Tier);
            Assert.IsTrue(sim.TryBuy(UpgradeId.UpgradePlot));
            Assert.AreEqual(CropTier.Tomato, sim.State.GetPlot(1, 0).Tier); // row-major: next in the first row
            Assert.AreEqual(CropTier.Carrot, sim.State.GetPlot(0, 1).Tier);

            for (int i = 2; i < 9; i++) Assert.IsTrue(sim.TryBuy(UpgradeId.UpgradePlot));
            foreach (var p in sim.State.Plots) Assert.AreEqual(CropTier.Tomato, p.Tier);

            Assert.IsTrue(sim.TryBuy(UpgradeId.UpgradePlot));
            Assert.AreEqual(CropTier.Corn, sim.State.GetPlot(0, 0).Tier); // lowest is tomato again; first one wins

            for (int i = 10; i < 18; i++) Assert.IsTrue(sim.TryBuy(UpgradeId.UpgradePlot));
            foreach (var p in sim.State.Plots) Assert.AreEqual(CropTier.Corn, p.Tier);
            Assert.IsTrue(sim.IsMaxed(UpgradeId.UpgradePlot));
            Assert.IsFalse(sim.TryBuy(UpgradeId.UpgradePlot));

            // Expanding the field adds carrot plots and un-maxes the upgrade.
            Assert.IsTrue(sim.TryBuy(UpgradeId.ExpandField));
            Assert.AreEqual(4, sim.State.GridSize);
            Assert.IsFalse(sim.IsMaxed(UpgradeId.UpgradePlot));
            Assert.AreEqual(CropTier.Corn, sim.State.GetPlot(2, 2).Tier);
            Assert.AreEqual(CropTier.Carrot, sim.State.GetPlot(3, 3).Tier);
        }

        [Test]
        public void ExpandField_GrowsTo4Then5_AndStops()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            Assert.AreEqual(3, sim.State.GridSize);
            Assert.IsTrue(sim.TryBuy(UpgradeId.ExpandField));
            Assert.AreEqual(4, sim.State.GridSize);
            Assert.AreEqual(16, sim.State.Plots.Count);
            Assert.IsTrue(sim.TryBuy(UpgradeId.ExpandField));
            Assert.AreEqual(5, sim.State.GridSize);
            Assert.IsTrue(sim.IsMaxed(UpgradeId.ExpandField));
            Assert.IsFalse(sim.TryBuy(UpgradeId.ExpandField));
            for (int i = 0; i < 25; i++) Assert.AreEqual(new GridPos(i % 5, i / 5), sim.State.Plots[i].Pos);
        }

        [Test]
        public void RingRadius_And_YearLength_AreCapped()
        {
            var sim = NewSim();
            Assert.AreEqual(1.5f, sim.State.RingRadius);
            Grant(sim, UpgradeId.RingRadius, 3);
            Assert.AreEqual(3f, sim.State.RingRadius);
            Assert.IsTrue(sim.IsMaxed(UpgradeId.RingRadius));
            Grant(sim, UpgradeId.Calendar, 4);
            Assert.AreEqual(150f, sim.State.YearLength);
        }

        // ---------------------------------------------------------------- crows

        [Test]
        public void Crow_EatsCropAfter4Seconds_IfNotTapped()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.DebugSpawnCrow());
            var crow = sim.State.Crows[0];
            var plot = sim.State.GetPlot(crow.Pos);
            Assert.IsTrue(plot.HasCrow);
            Assert.IsTrue(plot.IsRipe);
            var ate = new List<CrowEvent>();
            sim.CrowAte += ate.Add;

            Run(sim, 3.9f, null);
            Assert.IsTrue(plot.HasCrow);
            Assert.AreEqual(0, ate.Count);
            Run(sim, 0.2f, null);
            Assert.IsFalse(plot.HasCrow);
            Assert.AreEqual(0f, plot.Growth);
            Assert.AreEqual(1, ate.Count);
            Assert.AreEqual(plot.Pos, ate[0].Pos);
            Assert.AreEqual(0, sim.State.Crows.Count);
        }

        [Test]
        public void Crow_TapBeforeItEats_ScaresIt_CropStays()
        {
            var sim = NewSim();
            sim.DebugSpawnCrow();
            var pos = sim.State.Crows[0].Pos;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;

            Run(sim, 2f, null);
            Assert.IsFalse(sim.TapAt(new GridPos(-1, 0)), "out of bounds tap ignored");
            Assert.IsTrue(sim.TapAt(pos));
            Assert.AreEqual(1, scared.Count);
            Assert.IsFalse(sim.State.GetPlot(pos).HasCrow);
            Assert.IsTrue(sim.State.GetPlot(pos).IsRipe);
            Run(sim, 5f, null);
            Assert.IsTrue(sim.State.GetPlot(pos).IsRipe, "crop still there");
            Assert.IsFalse(sim.TapAt(pos), "nothing to scare now");
        }

        [Test]
        public void Crows_DoNotSpawnInYear1()
        {
            var sim = NewSim(c => c.CrowSpawnChance = 1f);
            int landed = 0;
            sim.CrowLanded += _ => landed++;
            sim.DebugSpawnCrow(); // leaves a ripe plot behind once the crow is scared off
            landed = 0;
            var ripePos = sim.State.Crows[0].Pos;
            Assert.IsTrue(sim.TapAt(ripePos));
            Assert.IsTrue(sim.State.GetPlot(ripePos).IsRipe, "a ripe, unprotected plot exists");
            Run(sim, 30f, null);
            Assert.AreEqual(0, landed, "no crows in year 1 even with a ripe target");
        }

        [Test]
        public void Crows_SpawnFromYear2_OnRipePlotsOutsideRing_MaxTwo()
        {
            var sim = NewSim(c => c.CrowSpawnChance = 1f);
            int landed = 0;
            sim.CrowLanded += _ => landed++;
            Grant(sim, UpgradeId.Irrigation, 3); // crops ripen outside the ring in ~6.7 s
            sim.StartNextYear();
            Assert.AreEqual(2, sim.State.Year);
            Run(sim, 20f, null);
            Assert.That(landed, Is.GreaterThanOrEqualTo(1));
            Assert.That(sim.State.Crows.Count, Is.LessThanOrEqualTo(2));
            foreach (var crow in sim.State.Crows) Assert.IsTrue(sim.State.GetPlot(crow.Pos).HasCrow);

            // A crow never lands under the ring: park the ring over everything, scare all, wait.
            foreach (var crow in new List<Crow>(sim.State.Crows)) sim.TapAt(crow.Pos);
            landed = 0;
            Run(sim, 20f, new RingInput(1, 1)); // radius 1.5 covers the whole 3x3 -> no ripe plot survives
            Assert.AreEqual(0, landed);
        }

        [Test]
        public void Scarecrow_PreventsCrows()
        {
            var sim = NewSim(c => c.CrowSpawnChance = 1f);
            int landed = 0;
            sim.CrowLanded += _ => landed++;
            Grant(sim, UpgradeId.Irrigation, 3);
            sim.DebugAddCoins(1e6);
            Assert.IsTrue(sim.TryBuy(UpgradeId.Scarecrow));
            sim.StartNextYear();
            Run(sim, 30f, null);
            Assert.AreEqual(0, landed);
            Assert.AreEqual(0, sim.State.Crows.Count);
        }

        [Test]
        public void Ring_OverCrowPlot_HarvestsAndScaresCrow()
        {
            var sim = NewSim();
            sim.DebugSpawnCrow();
            var pos = sim.State.Crows[0].Pos;
            int scared = 0, harvested = 0;
            sim.CrowScared += _ => scared++;
            sim.Harvested += _ => harvested++;
            sim.DebugSetRingRadiusOverride(0.5f);
            sim.Tick(0.01f, new RingInput(pos.X, pos.Y));
            Assert.AreEqual(1, harvested);
            Assert.AreEqual(1, scared);
            Assert.AreEqual(0, sim.State.Crows.Count);
        }

        // ---------------------------------------------------------------- apprentice

        [Test]
        public void Apprentice_WalksToNearestRipePlot_AndHarvestsIt()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Assert.IsFalse(sim.State.Apprentice.Owned);
            Grant(sim, UpgradeId.Irrigation, 3);
            sim.DebugAddCoins(1e6);
            Assert.IsTrue(sim.TryBuy(UpgradeId.Apprentice));
            Assert.IsTrue(sim.State.Apprentice.Owned);
            sim.DebugAddCoins(-sim.State.Coins);
            sim.StartNextYear();

            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            Run(sim, 6.6f, null); // carrots ripen at 3 / 0.45 = 6.67 s
            Assert.AreEqual(0, harvests.Count);
            Assert.IsFalse(sim.State.Apprentice.HasTarget);

            // Apprentice starts at (1, -1.2): nearest ripe plot is (1,0), 1.2 plots away at 1.5 plots/s = 0.8 s, + 0.5 s harvest.
            float t = 0f;
            while (harvests.Count == 0 && t < 10f)
            {
                sim.Tick(Dt, null);
                t += Dt;
            }
            Assert.AreEqual(1, harvests.Count);
            Assert.AreEqual(HarvestSource.Apprentice, harvests[0].Source);
            Assert.AreEqual(new GridPos(1, 0), harvests[0].Pos);
            Assert.That(t, Is.EqualTo(0.07f + 1.3f).Within(0.1f)); // 0.07 s until ripe, then 0.8 + 0.5
            Assert.AreEqual(1, sim.State.Coins);
            Assert.That(sim.State.Apprentice.X, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(sim.State.Apprentice.Y, Is.EqualTo(0f).Within(1e-3f));

            // It keeps going: more harvests follow.
            Run(sim, 10f, null);
            Assert.That(harvests.Count, Is.GreaterThan(3));
            foreach (var h in harvests) Assert.AreEqual(HarvestSource.Apprentice, h.Source);
        }

        [Test]
        public void Apprentice_Retargets_WhenRingHarvestsItsTarget()
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, UpgradeId.Irrigation, 3);
            sim.DebugAddCoins(1e6);
            Assert.IsTrue(sim.TryBuy(UpgradeId.Apprentice));
            sim.StartNextYear();
            Run(sim, 7f, null);
            Assert.IsTrue(sim.State.Apprentice.HasTarget);
            var target = sim.State.Apprentice.Target;
            sim.DebugSetRingRadiusOverride(0.5f);
            sim.Tick(Dt, new RingInput(target.X, target.Y)); // ring steals it
            Assert.IsFalse(sim.State.GetPlot(target).IsRipe);
            sim.Tick(Dt, null);
            Assert.IsTrue(sim.State.Apprentice.HasTarget);
            Assert.AreNotEqual(target, sim.State.Apprentice.Target);
        }

        [Test]
        public void Apprentice_SpeedIncreasesPerLevel()
        {
            // Level 1 walks 1.5 plots/s, level 3 walks 2.5 plots/s: measure distance covered in 0.4 s.
            float DistanceAfter(int level)
            {
                var sim = NewSim(c => c.CrowFirstYear = 99);
                Grant(sim, UpgradeId.Irrigation, 3);
                sim.DebugAddCoins(1e6);
                for (int i = 0; i < level; i++) Assert.IsTrue(sim.TryBuy(UpgradeId.Apprentice));
                sim.StartNextYear();
                Run(sim, 6.7f, null); // carrots ripe
                float x0 = sim.State.Apprentice.X, y0 = sim.State.Apprentice.Y;
                Run(sim, 0.4f, null);
                float dx = sim.State.Apprentice.X - x0, dy = sim.State.Apprentice.Y - y0;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
            Assert.That(DistanceAfter(1), Is.EqualTo(0.6f).Within(0.03f));
            Assert.That(DistanceAfter(3), Is.EqualTo(1.0f).Within(0.03f));
        }

        // ---------------------------------------------------------------- misc

        [Test]
        public void Determinism_SameSeedSameInputs_SameResult()
        {
            var a = NewSim(c => c.CrowSpawnChance = 1f, 42);
            var b = NewSim(c => c.CrowSpawnChance = 1f, 42);
            foreach (var s in new[] { a, b })
            {
                Grant(s, UpgradeId.Irrigation, 2);
                s.StartNextYear();
                Run(s, 40f, null);
                Run(s, 5f, new RingInput(1, 1));
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins);
            Assert.AreEqual(a.State.Crows.Count, b.State.Crows.Count);
            for (int i = 0; i < a.State.Plots.Count; i++)
                Assert.AreEqual(a.State.Plots[i].Growth, b.State.Plots[i].Growth);
        }

        [Test]
        public void Config_Defaults_MatchDesignDoc()
        {
            var c = new FarmConfig();
            Assert.AreEqual(3, c.StartGridSize);
            Assert.AreEqual(new[] { 3f, 6f, 10f }, c.RipeTimes);
            Assert.AreEqual(new[] { 1.0, 4.0, 12.0 }, c.CropValues);
            Assert.AreEqual(60, c.GetUpgrade(UpgradeId.ExpandField).BaseCost);
            Assert.AreEqual(80, c.GetUpgrade(UpgradeId.Apprentice).BaseCost);
            Assert.AreEqual(1, c.GetUpgrade(UpgradeId.Scarecrow).MaxLevel);
        }
    }
}
