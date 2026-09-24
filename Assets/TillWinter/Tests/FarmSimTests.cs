using System;
using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// The farm around the core loop (v3): crop timings, passive growth, the year, the Almanac, apprentices and crows.
    /// The loop itself (strike, beat, stamina, watering, swipe) is <see cref="CoreLoopTests"/>.
    /// </summary>
    public class FarmSimTests
    {
        private const float Dt = 0.01f;

        /// <summary>Classic numbers and a hoe that never crits by chance, so an off-beat strike is always 3 damage.</summary>
        private static FarmSim NewSim(Action<FarmConfig> tweak = null, int seed = 1)
        {
            var cfg = TestConfig.Classic();
            cfg.BaseCritChance = 0;
            tweak?.Invoke(cfg);
            return new FarmSim(cfg, seed);
        }

        private static void Run(FarmSim sim, float seconds, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt);
        }

        /// <summary>Sets node levels directly (debug hook), leaving the sim in its current season.</summary>
        private static void Grant(FarmSim sim, string nodeId, int level = 1) => sim.DebugSetLevel(nodeId, level);

        private static readonly GridPos Centre = new GridPos(1, 1);

        private static float TimeUntil(FarmSim sim, Func<bool> done, float max = 60f)
        {
            float t = 0f;
            while (!done() && t < max)
            {
                sim.Tick(Dt);
                t += Dt;
            }
            return t;
        }

        // ---------------------------------------------------------------- crop timings

        [Test]
        public void Corn_Level0_GrowsInEightSeconds_AndPaysTwelve()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            Grant(sim, "unlock_corn");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            for (int i = 0; i < 18; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot"), "upgrade #" + i);
            Assert.AreEqual(2, sim.State.GetPlot(Centre).Tier);
            sim.StartNextYear();
            var plot = sim.State.GetPlot(Centre);

            Assert.IsTrue(sim.DebugBreak(Centre));
            double before = sim.State.Coins;
            Assert.That(TimeUntil(sim, () => plot.State == PlotState.Ripe), Is.EqualTo(8.0f).Within(0.03f));
            Assert.IsTrue(sim.ReapOne(Centre));
            Assert.AreEqual(12, sim.State.Coins - before, 1e-9);
            Assert.AreEqual(PlotState.Hard, plot.State, "back to the ground, a layer deeper");
            Assert.AreEqual(1, plot.Layer);
        }

        // ---------------------------------------------------------------- passive systems

        [Test]
        public void HardGround_NeverBreaksByItself()
        {
            var sim = NewSim();
            Run(sim, 10f);
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Hard, p.State);
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
            }
            Assert.AreEqual(0, sim.State.Coins);
            Assert.AreEqual(sim.Config.BaseStaminaMax, sim.State.Stamina, 1e-4f);
        }

        [Test]
        public void SoilQuality_MultipliesGrowth_NotTheHoe()
        {
            var sim = NewSim();
            Grant(sim, "soil_quality", 2); // 1 + 2 × 0.25 = 1.5×
            Assert.IsTrue(sim.Strike(new GridPos(0, 0)));
            Assert.AreEqual(sim.Config.BaseHp - sim.Config.BaseStrikeDamage, sim.State.GetPlot(0, 0).Hp, 1e-9, "soil does not swing the hoe");
            var plot = sim.State.GetPlot(Centre);
            sim.DebugBreak(Centre);
            // Carrot 2.5 s / 1.5 = 1.667 s.
            Assert.That(TimeUntil(sim, () => plot.State == PlotState.Ripe), Is.EqualTo(1.667f).Within(0.03f));
        }

        [Test]
        public void CropValue_MultipliesHandReaps()
        {
            var sim = NewSim();
            Grant(sim, "crop_value", 5); // 1 + 5 × 0.1 = 1.5×
            sim.DebugForceRipe(Centre);
            Assert.IsTrue(sim.ReapOne(Centre));
            Assert.That(sim.State.Coins, Is.EqualTo(1 * 1.5).Within(1e-9));
        }

        // ---------------------------------------------------------------- year

        [Test]
        public void Winter_StartsAt90Seconds()
        {
            var sim = NewSim();
            bool winter = false;
            sim.WinterStarted += () => winter = true;
            Run(sim, 89.9f);
            Assert.IsFalse(winter);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Assert.IsTrue(sim.State.FrostWarning);
            Run(sim, 0.2f);
            Assert.IsTrue(winter);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void YearLength_PlusFifteenPerLevel_CapsAt180()
        {
            var sim = NewSim();
            Grant(sim, "year_length", 1);
            Assert.AreEqual(105f, sim.State.YearLength);
            Grant(sim, "year_length", 6);
            Assert.AreEqual(180f, sim.State.YearLength);
            Run(sim, 179.9f);
            Assert.IsFalse(sim.State.IsWinter);
            Run(sim, 0.2f);
            Assert.IsTrue(sim.State.IsWinter);
        }

        [Test]
        public void FrostWarning_Last10Seconds_Plus5PerLevel()
        {
            var sim = NewSim();
            Run(sim, 79.9f);
            Assert.IsFalse(sim.State.FrostWarning);
            Run(sim, 0.2f);
            Assert.IsTrue(sim.State.FrostWarning);

            var longer = NewSim();
            Grant(longer, "frost_warning", 1);
            Assert.AreEqual(15f, longer.State.Stats.FrostWarningSeconds);
            bool frost = false;
            longer.FrostWarningStarted += () => frost = true;
            Run(longer, 74.9f);
            Assert.IsFalse(frost);
            Run(longer, 0.2f);
            Assert.IsTrue(frost);
        }

        [Test]
        public void Seasons_ChangeAtThirds()
        {
            var sim = NewSim();
            var seasons = new List<Season>();
            sim.SeasonChanged += seasons.Add;
            Run(sim, 29.9f);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Run(sim, 0.2f);
            Assert.AreEqual(Season.Summer, sim.State.Season);
            Run(sim, 30f);
            Assert.AreEqual(Season.Autumn, sim.State.Season);
            Run(sim, 30f);
            CollectionAssert.AreEqual(new[] { Season.Summer, Season.Autumn, Season.Winter }, seasons);
        }

        [Test]
        public void Winter_FrostReapsTheStandingField_KeepsCoinsAndTiers_AndFreezesTheClock()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(400);
            Assert.IsTrue(sim.TryBuy("upgrade_plot")); // 100 coins: (0,0) is a tomato bed
            sim.StartNextYear();
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);

            sim.DebugForceRipeAll();
            Assert.IsTrue(sim.ReapOne(Centre)); // one carrot by hand
            double coins = sim.State.Coins;
            Assert.AreEqual(301, coins, 1e-9);
            Assert.IsTrue(sim.DebugSpawnCrow());

            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.State.IsWinter);
            // The frost reaps the eight crops still standing at the plain price: one tomato (4) and seven carrots (1).
            // The crow it flushes drops no bounty.
            Assert.AreEqual(coins + 4 + 7, sim.State.Coins, 1e-9);
            Assert.AreEqual(0, sim.State.Crows.Count);
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotState.Hard, p.State, p.Pos.ToString());
                Assert.AreEqual(1, p.Layer, "the reaped ground came back a layer deeper");
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
                Assert.IsFalse(p.HasCrow);
            }
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier, "tiers kept");

            // Frozen: the hoe and the swipe do nothing, the timer does not move.
            float yearTime = sim.State.YearTime;
            Run(sim, 5f);
            Assert.IsFalse(sim.Strike(Centre));
            Assert.AreEqual(0, sim.ReapAll());
            Assert.AreEqual(coins + 11, sim.State.Coins, 1e-9);
            Assert.AreEqual(yearTime, sim.State.YearTime);

            sim.StartNextYear();
            Assert.AreEqual(3, sim.State.Year);
            Assert.AreEqual(Season.Spring, sim.State.Season);
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);
        }

        // ---------------------------------------------------------------- almanac

        [Test]
        public void Almanac_RejectsOutsideWinter()
        {
            var sim = NewSim();
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.CanBuy("hoe_damage"));
            Assert.IsFalse(sim.TryBuy("hoe_damage"));
            Assert.AreEqual(0, sim.State.GetLevel("hoe_damage"));
            Assert.AreEqual(1e6, sim.State.Coins);
        }

        [Test]
        public void Almanac_EnforcesPrerequisites_AnyOf()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            Assert.IsFalse(sim.IsAvailable("stamina_depot"));
            Assert.IsFalse(sim.TryBuy("stamina_depot"));
            Assert.IsTrue(sim.TryBuy("hoe_damage"));
            Assert.IsTrue(sim.IsAvailable("stamina_depot"));
            Assert.IsTrue(sim.TryBuy("steady_hand"));
            // strike_speed needs steady_hand OR lucky_hoe (GDD §6: at least one prerequisite).
            Assert.IsTrue(sim.IsAvailable("strike_speed"));
            Assert.AreEqual(0, sim.State.GetLevel("lucky_hoe"));
            Assert.IsTrue(sim.TryBuy("strike_speed"));
            Assert.IsFalse(sim.TryBuy("no_such_node"));
        }

        [Test]
        public void Almanac_CostCurve_And_Maxed_And_Unaffordable()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            var node = AlmanacData.Get("hoe_damage");
            for (int n = 0; n < node.MaxLevel; n++)
            {
                double expected = Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, n));
                Assert.AreEqual(expected, sim.CostOf("hoe_damage"), "cost at level " + n);
                sim.DebugAddCoins(expected - 1 - sim.State.Coins);
                Assert.IsFalse(sim.TryBuy("hoe_damage"), "unaffordable by 1");
                sim.DebugAddCoins(1);
                Assert.IsTrue(sim.TryBuy("hoe_damage"));
                Assert.AreEqual(0, sim.State.Coins);
            }
            Assert.IsTrue(sim.IsMaxed("hoe_damage"));
            sim.DebugAddCoins(1e9);
            Assert.IsFalse(sim.TryBuy("hoe_damage"));
            Assert.AreEqual(sim.Config.BaseStrikeDamage + node.MaxLevel * node.ValuePerLevel, sim.State.Stats.StrikeDamage, 1e-9);
        }

        [Test]
        public void HoeDamage_AddsOnePerLevel_AndAtEightBreaksClayInOneSwing()
        {
            var one = NewSim();
            Grant(one, "hoe_damage", 1);
            Assert.AreEqual(4, one.State.Stats.StrikeDamage, 1e-9);
            Assert.IsTrue(one.Strike(Centre));
            Assert.AreEqual(10 - 4, one.State.GetPlot(Centre).Hp, 1e-9);

            var max = NewSim();
            Grant(max, "hoe_damage", 8);
            Assert.AreEqual(11, max.State.Stats.StrikeDamage, 1e-9);
            Assert.IsTrue(max.Strike(Centre));
            Assert.AreEqual(PlotState.Growing, max.State.GetPlot(Centre).State, "11 damage through 10 HP");
            Assert.AreEqual(max.Config.BreakCoins, max.State.Coins, 1e-9);
        }

        [Test]
        public void Purchased_CarriesNodeIdAndLevel()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            var events = new List<PurchaseEvent>();
            sim.Purchased += events.Add;
            sim.TryBuy("growth");
            sim.TryBuy("growth");
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("growth", events[1].NodeId);
            Assert.AreEqual(2, events[1].Level);
            Assert.AreEqual(2, sim.State.AlmanacLevels["growth"]);
        }

        [Test]
        public void UpgradePlot_NeedsUnlockedTier_PicksLowestRowMajor_MaxesWhenAllAtCap()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsFalse(sim.IsAvailable("upgrade_plot"), "needs unlock_tomato first");
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.AreEqual(1, sim.State.Stats.MaxTierUnlocked);
            Assert.AreEqual(16, sim.GetMaxLevel("upgrade_plot"));

            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(1, sim.State.GetPlot(0, 0).Tier);
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(1, sim.State.GetPlot(1, 0).Tier, "row-major tiebreak");
            for (int i = 2; i < 16; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot"), "#" + i);
            foreach (var p in sim.State.Plots) Assert.AreEqual(1, p.Tier);
            Assert.IsTrue(sim.IsMaxed("upgrade_plot"), "all plots at the highest unlocked tier");
            Assert.IsFalse(sim.TryBuy("upgrade_plot"));

            Assert.IsTrue(sim.TryBuy("unlock_corn"));
            Assert.IsFalse(sim.IsMaxed("upgrade_plot"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(2, sim.State.GetPlot(0, 0).Tier);
        }

        [Test]
        public void ExpandField_3To6_NewPlotsHardAtLayer0Tier0()
        {
            var sim = NewSim();
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e9);
            for (int size = 4; size <= 6; size++)
            {
                Assert.IsTrue(sim.TryBuy("expand_field"));
                Assert.AreEqual(size, sim.State.GridSize);
                Assert.AreEqual(size * size, sim.State.Plots.Count);
            }
            Assert.IsTrue(sim.IsMaxed("expand_field"));
            Assert.IsFalse(sim.TryBuy("expand_field"));
            for (int i = 0; i < 36; i++)
            {
                var p = sim.State.Plots[i];
                Assert.AreEqual(new GridPos(i % 6, i / 6), p.Pos);
                Assert.AreEqual(PlotState.Hard, p.State);
                Assert.AreEqual(0, p.Layer);
                Assert.AreEqual(sim.Config.BaseHp, p.MaxHp, 1e-9);
                Assert.AreEqual(p.MaxHp, p.Hp, 1e-9);
                Assert.AreEqual(0, p.Tier);
            }
        }

        // ---------------------------------------------------------------- apprentices

        private static FarmSim SimWithApprentices(int count, Action<FarmSim> more = null)
        {
            var sim = NewSim(c => c.CrowFirstYear = 99);
            Grant(sim, "apprentice_count", count);
            more?.Invoke(sim);
            return sim;
        }

        [Test]
        public void Picker_TakesWorkTime_PaysValueTimesYield()
        {
            var sim = SimWithApprentices(1);
            Assert.AreEqual(1, sim.State.Apprentices.Count);
            var a = sim.State.Apprentices[0];
            Assert.AreEqual(ApprenticeRole.Picker, a.Role);
            Assert.That(a.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(a.Y, Is.EqualTo(-1.2f).Within(1e-4f));

            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            sim.DebugForceRipeAll();
            // Nearest ripe plot is (1,0): 1.2 plots at 1.5 plots/s = 0.8 s, then 1.0 s of work.
            float t = TimeUntil(sim, () => harvests.Count > 0);
            Assert.That(t, Is.EqualTo(1.8f).Within(0.05f));
            Assert.AreEqual(HarvestSource.Apprentice, harvests[0].Source);
            Assert.AreEqual(0, harvests[0].ApprenticeIndex);
            Assert.AreEqual(new GridPos(1, 0), harvests[0].Pos);
            Assert.AreEqual(0.5, harvests[0].Coins, "value 1 × yield 0.5 at level 0");
            Assert.AreEqual(0.5, sim.State.Coins);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(1, 0).State, "the ground came back");
        }

        [Test]
        public void Apprentice_WorkTimeAndYieldNodes()
        {
            var sim = SimWithApprentices(1, s =>
            {
                Grant(s, "apprentice_work_time", 3); // 1.0 - 3 × 0.2 = 0.4
                Grant(s, "apprentice_yield", 4);     // 1.3
            });
            Assert.That(sim.State.Stats.ApprenticeWorkTime, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.AreEqual(1.3, sim.State.Stats.ApprenticeYield);
            sim.DebugForceRipeAll();
            var harvests = new List<HarvestEvent>();
            sim.Harvested += harvests.Add;
            float t = TimeUntil(sim, () => harvests.Count > 0);
            Assert.That(t, Is.EqualTo(0.8f + 0.4f).Within(0.05f));
            Assert.AreEqual(1.3, harvests[0].Coins);
        }

        [Test]
        public void Apprentice_SpeedNode_ChangesWalkSpeed()
        {
            float DistanceIn(int speedLevel, float seconds)
            {
                var sim = SimWithApprentices(1, s => Grant(s, "apprentice_speed", speedLevel));
                sim.DebugForceRipeAll();
                var a = sim.State.Apprentices[0];
                float x0 = a.X, y0 = a.Y;
                Run(sim, seconds);
                return (float)Math.Sqrt((a.X - x0) * (a.X - x0) + (a.Y - y0) * (a.Y - y0));
            }
            Assert.That(DistanceIn(0, 0.4f), Is.EqualTo(0.6f).Within(0.03f));  // 1.5 plots/s
            Assert.That(DistanceIn(4, 0.2f), Is.EqualTo(0.7f).Within(0.03f));  // 1.5 + 4 × 0.5 = 3.5 plots/s
        }

        [Test]
        public void TwoApprentices_NeverTargetTheSamePlot()
        {
            var sim = SimWithApprentices(2);
            Assert.AreEqual(2, sim.State.Apprentices.Count);
            var a0 = sim.State.Apprentices[0];
            var a1 = sim.State.Apprentices[1];
            Assert.AreNotEqual(a0.IdleX, a1.IdleX, "spread along the edge");

            sim.DebugForceRipeAll();
            int bothTargeting = 0;
            for (int i = 0; i < 600; i++)
            {
                sim.Tick(Dt);
                if (a0.HasTarget && a1.HasTarget)
                {
                    bothTargeting++;
                    Assert.AreNotEqual(a0.Target, a1.Target, "tick " + i);
                }
            }
            Assert.That(bothTargeting, Is.GreaterThan(100), "both were busy for a while");
            Assert.That(sim.State.Coins, Is.GreaterThan(0));
        }

        [Test]
        public void Apprentice_Retargets_WhenTheHandReapsItsTarget_AndIdlesAtEdge()
        {
            var sim = SimWithApprentices(1);
            sim.DebugForceRipeAll();
            Run(sim, 0.3f);
            var a = sim.State.Apprentices[0];
            Assert.IsTrue(a.HasTarget);
            Assert.IsTrue(a.IsWalking);
            var target = a.Target;
            Assert.IsTrue(sim.ReapOne(target)); // the hand gets there first
            Assert.IsFalse(sim.State.GetPlot(target).IsRipe);
            sim.Tick(Dt);
            Assert.IsTrue(a.HasTarget);
            Assert.AreNotEqual(target, a.Target);

            // The hand takes the rest in one swipe; with nothing ripe the apprentice walks home.
            Assert.That(sim.ReapAll(), Is.GreaterThan(0));
            Run(sim, 5f);
            Assert.IsFalse(a.HasTarget);
            Assert.That(a.X, Is.EqualTo(a.IdleX).Within(1e-3f));
            Assert.That(a.Y, Is.EqualTo(a.IdleY).Within(1e-3f));
            Assert.IsFalse(a.IsWalking);
        }

        [Test]
        public void SixApprentices_AllWork()
        {
            var sim = SimWithApprentices(6);
            Assert.AreEqual(6, sim.State.Apprentices.Count);
            sim.DebugForceRipeAll();
            var byIndex = new HashSet<int>();
            sim.Harvested += e => byIndex.Add(e.ApprenticeIndex);
            Run(sim, 4f);
            Assert.AreEqual(6, byIndex.Count, "every apprentice reaped at least once");
        }

        // ---------------------------------------------------------------- crows

        [Test]
        public void Crow_EatsAfter4Seconds_GroundComesBackALayerDeeper()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.DebugSpawnCrow());
            var plot = sim.State.GetPlot(sim.State.Crows[0].Pos);
            Assert.IsTrue(plot.IsRipe);
            var ate = new List<CrowEvent>();
            sim.CrowAte += ate.Add;
            Run(sim, 3.9f);
            Assert.IsTrue(plot.HasCrow);
            Run(sim, 0.2f);
            Assert.IsFalse(plot.HasCrow);
            Assert.AreEqual(PlotState.Hard, plot.State);
            Assert.AreEqual(1, plot.Layer);
            Assert.AreEqual(1, ate.Count);
            Assert.AreEqual(1, sim.State.CropsLostThisYear);
        }

        [Test]
        public void Crow_TapScare_PaysTwiceCropValue_AndReapsTheCrop()
        {
            var sim = NewSim();
            Grant(sim, "unlock_tomato");
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e6);
            for (int i = 0; i < 9; i++) Assert.IsTrue(sim.TryBuy("upgrade_plot")); // all tomato (value 4)
            sim.DebugAddCoins(-sim.State.Coins);
            sim.StartNextYear();
            Assert.IsTrue(sim.DebugSpawnCrow());
            var pos = sim.State.Crows[0].Pos;
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;
            Run(sim, 2f);
            Assert.IsTrue(sim.TapAt(pos));
            Assert.AreEqual(1, scared.Count);
            Assert.AreEqual(8, scared[0].Coins, 1e-9, "2 × the tomato");
            Assert.AreEqual(8 + 4, sim.State.Coins, 1e-9, "the bounty and the crop under it");
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(pos).State);
            Assert.IsFalse(sim.TapAt(pos), "hard ground is the strike's");
            Assert.IsFalse(sim.TapAt(new GridPos(-1, 0)));
        }

        [Test]
        public void Crow_HelperScare_PaysNothing()
        {
            var sim = SimWithApprentices(1);
            var at = new GridPos(1, 0);
            Assert.IsTrue(sim.DebugForceRipe(at));
            Assert.IsTrue(sim.DebugSpawnCrow());
            Assert.AreEqual(at, sim.State.Crows[0].Pos);
            var scared = new List<CrowEvent>();
            sim.CrowScared += scared.Add;
            Run(sim, 2f); // the picker reaps it at 1.8 s, before the crow eats at 4 s
            Assert.AreEqual(1, scared.Count);
            Assert.AreEqual(0, scared[0].Coins);
            Assert.AreEqual(0.5, sim.State.Coins, 1e-9, "just the apprentice's harvest");
            Assert.AreEqual(0, sim.State.Crows.Count);
            Assert.AreEqual(PlotState.Hard, sim.State.GetPlot(at).State);
        }

        private static int CountSpawns(int scarecrowLevel, int seed)
        {
            // Every ripe plot is a target at once, and a landed crow is tapped and its plot ripened again, so every
            // spawn check has a candidate and the chance alone decides.
            var sim = NewSim(c => c.CrowMinRipe = 0f, seed);
            Grant(sim, "scarecrow", scarecrowLevel);
            int landed = 0;
            sim.CrowLanded += e =>
            {
                landed++;
                sim.TapAt(e.Pos);
                sim.DebugForceRipeAll();
            };
            for (int year = 0; year < 5; year++)
            {
                sim.DebugSkipToWinter();
                sim.StartNextYear(); // years 2..6
                sim.DebugForceRipeAll();
                Run(sim, 88f, 0.05f); // 88 spawn checks per year
            }
            return landed; // 440 checks per seed
        }

        [Test]
        public void Scarecrows_GuardAnArea_WithoutChangingTheChance()
        {
            int l0 = 0, l1 = 0, l2 = 0;
            for (int seed = 1; seed <= 5; seed++)
            {
                l0 += CountSpawns(0, seed);
                l1 += CountSpawns(1, seed);
                l2 += CountSpawns(2, seed);
            }
            // GDD §5.1 (v2.0): 2200 checks at 8% ≈ 176 (sd 13) with no scarecrow. One scarecrow leaves one plot of
            // the 3x3 field open, which still draws every crow; two guard the whole field.
            Assert.That(l0, Is.InRange(125, 230));
            Assert.That(l1, Is.InRange(125, 230), "the chance is unchanged: the crows all go to the open plot");
            Assert.AreEqual(0, l2, "a fully guarded field draws no crow");
        }

        [Test]
        public void Crows_OnlyFromYear2_MaxTwo_NeverOnAFreshCrop()
        {
            var y1 = NewSim();
            int landed = 0;
            y1.CrowLanded += _ => landed++;
            y1.DebugForceRipeAll();
            Run(y1, 60f);
            Assert.AreEqual(0, landed, "year 1");

            var y2 = NewSim(c => { c.CrowSpawnChance = 1f; c.CrowEatTime = 100f; });
            y2.DebugSkipToWinter();
            y2.StartNextYear();
            y2.DebugForceRipeAll();
            Run(y2, 12f);
            Assert.AreEqual(2, y2.State.Crows.Count);

            var fresh = NewSim(c => c.CrowSpawnChance = 1f);
            fresh.DebugSkipToWinter();
            fresh.StartNextYear();
            int landedFresh = 0;
            fresh.CrowLanded += _ => landedFresh++;
            for (int i = 0; i < 100; i++)
            {
                fresh.ReapAll();
                fresh.DebugForceRipeAll(); // no crop stands ripe longer than 0.1 s: none is a crow target
                fresh.Tick(0.1f);
            }
            Assert.AreEqual(0, landedFresh);
        }

        // ---------------------------------------------------------------- misc

        [Test]
        public void Determinism_SameSeedSameInputs_SameResult()
        {
            var a = NewSim(null, 42);
            var b = NewSim(null, 42);
            foreach (var s in new[] { a, b })
            {
                Grant(s, "growth", 3);
                Grant(s, "apprentice_count", 2);
                Assert.IsTrue(s.SetApprenticeRole(1, ApprenticeRole.Digger));
                s.DebugSkipToWinter();
                s.StartNextYear();
                s.DebugBreakAll();
                Run(s, 40f);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins);
            Assert.AreEqual(a.State.Crows.Count, b.State.Crows.Count);
            for (int i = 0; i < a.State.Plots.Count; i++)
            {
                Assert.AreEqual(a.State.Plots[i].State, b.State.Plots[i].State);
                Assert.AreEqual(a.State.Plots[i].Progress, b.State.Plots[i].Progress);
                Assert.AreEqual(a.State.Plots[i].Hp, b.State.Plots[i].Hp);
            }
        }

        [Test]
        public void CropTable_MatchesGdd()
        {
            var c = new FarmConfig();
            Assert.AreEqual(6, c.Crops.Length);
            Assert.AreEqual(5, c.MaxTier);
            var expected = new[]
            {
                ("crop.carrot", 2.5f, 2.0, Season.Spring), // GDD §2v3.6 timings, M.2 seasons (v1.7)
                ("crop.tomato", 5f, 4.0, Season.Summer),
                ("crop.corn", 8f, 12.0, Season.Summer),
                ("crop.pumpkin", 13f, 35.0, Season.Autumn),
                ("crop.grapes", 19f, 100.0, Season.Autumn),
                ("crop.golden_wheat", 27f, 300.0, Season.Spring),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Item1, c.Crops[i].Key);
                Assert.AreEqual(expected[i].Item2, c.Crops[i].Grow);
                Assert.AreEqual(expected[i].Item3, c.Crops[i].Value);
                Assert.AreEqual(expected[i].Item4, c.Crops[i].Likes, c.Crops[i].Key);
            }
        }
    }
}
