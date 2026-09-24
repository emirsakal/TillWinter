using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.2, the rest of crops and field (GDD §2.3/§2.4 v1.8): mixed neighbours, crop rotation, the fertile ground a
    /// field expansion may add, and the rain cloud's burst on a growing field.
    /// </summary>
    public class FieldVarietyTests
    {
        /// <summary>Only the rule under test pays: seasons off, no special ground or depth bonus unless a test asks for it.</summary>
        private static FarmConfig Plain()
        {
            var cfg = new FarmConfig();
            cfg.InSeasonValue = 1;
            cfg.FertileChance = 0;
            cfg.ChestChance = cfg.HardpanChance = 0;
            cfg.DepthValue = 0;
            return cfg;
        }

        /// <summary>A 4x4 farm whose (0,0) bed can grow tomatoes, back in spring.</summary>
        private static FarmSim TomatoBed(FarmConfig cfg)
        {
            var sim = new FarmSim(cfg, 1);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(5000);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            sim.StartNextYear();
            return sim;
        }

        [Test]
        public void DifferentNeighbours_EachAddToTheValue()
        {
            var cfg = Plain();
            cfg.RotationBonus = 1; // the bed was carrots last year: keep the rotation out of this sum
            var sim = TomatoBed(cfg);
            var tomato = sim.State.GetPlot(0, 0);
            Assert.AreEqual(1, tomato.Tier);
            Assert.AreEqual(2, sim.DifferentNeighbours(tomato), "two carrot neighbours; the field edge counts for nothing");
            Assert.AreEqual(1 + 2 * cfg.NeighbourVarietyBonus, sim.PlotValueMultiplier(tomato), 1e-9);
            Assert.AreEqual(1, sim.DifferentNeighbours(sim.State.GetPlot(1, 0)));
            Assert.AreEqual(0, sim.DifferentNeighbours(sim.State.GetPlot(2, 2)), "carrots among carrots");
            Assert.AreEqual(1.0, sim.PlotValueMultiplier(sim.State.GetPlot(2, 2)), 1e-9);
        }

        [Test]
        public void ADifferentCropThanLastYear_IsARotation()
        {
            var cfg = Plain();
            cfg.NeighbourVarietyBonus = 0;
            var sim = TomatoBed(cfg);
            var plot = sim.State.GetPlot(0, 0);
            Assert.AreEqual(0, plot.LastYearTier, "carrots last year");
            Assert.IsTrue(plot.IsRotated, "an upgraded bed grows tomatoes after carrots");

            sim.DebugSkipToWinter();
            Assert.AreEqual(1, plot.LastYearTier, "tomatoes this year");
            sim.StartNextYear();
            Assert.IsFalse(plot.IsRotated, "tomatoes again");
            Assert.AreEqual(1.0, sim.PlotValueMultiplier(plot), 1e-9);

            Assert.IsTrue(sim.SetPlotCrop(plot.Pos, 0));
            Assert.IsTrue(plot.IsRotated, "carrots after tomatoes");
            Assert.AreEqual(cfg.RotationBonus, sim.PlotValueMultiplier(plot), 1e-9);
        }

        [Test]
        public void FertileGround_SellsForMore()
        {
            var cfg = Plain();
            var sim = new FarmSim(cfg, 3);
            var plot = sim.State.GetPlot(1, 1);
            sim.DebugSetPlotKind(plot.Pos, PlotKind.Fertile);
            Assert.AreEqual(cfg.FertileValue, sim.PlotValueMultiplier(plot), 1e-9);
        }

        [Test]
        public void TheRainCloud_GivesEveryGrowingCropABurst_AndLeavesHardGroundAlone()
        {
            var sim = new FarmSim(Plain(), 3);
            var at = new GridPos(1, 1);
            Assert.IsTrue(sim.DebugBreak(at));
            int watered = 0;
            sim.PlotWatered += p => { watered++; Assert.AreEqual(at, p); };
            Assert.IsTrue(sim.DebugSpawnCloud());
            Assert.IsTrue(sim.TapCloud());
            Assert.AreEqual(1, watered);
            Assert.AreEqual(sim.Config.CloudGrowBoost, sim.State.GetPlot(at).Progress, 1e-6f);
            var hard = sim.State.GetPlot(0, 0);
            Assert.AreEqual(PlotState.Hard, hard.State, "rain does not dig");
            Assert.AreEqual(hard.MaxHp, hard.Hp, 1e-9);
            Assert.IsFalse(sim.State.Cloud.Active, "one tap and it is spent");
        }

        [Test]
        public void AFieldExpansion_MayAddFertileGround_ButTheOldFieldStaysPlain()
        {
            var cfg = Plain();
            cfg.FertileChance = 1; // every new plot fertile, so the rule shows
            var sim = new FarmSim(cfg, 3);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(5000);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            foreach (var p in sim.State.Plots)
            {
                bool added = p.Pos.X >= 3 || p.Pos.Y >= 3;
                Assert.AreEqual(added ? PlotKind.Fertile : PlotKind.Normal, p.Kind, p.Pos.ToString());
            }

            var plain = new FarmSim(Plain(), 3);
            plain.DebugSkipToWinter();
            plain.DebugAddCoins(5000);
            Assert.IsTrue(plain.TryBuy("expand_field"));
            foreach (var p in plain.State.Plots) Assert.AreEqual(PlotKind.Normal, p.Kind);
        }

        [Test]
        public void SpecialGround_LastsTheGeneration_AndRetiringGivesAPlainField()
        {
            var sim = new FarmSim(Plain(), 3);
            sim.DebugSetPlotKind(new GridPos(0, 0), PlotKind.Fertile);
            sim.DebugSkipToWinter();
            sim.StartNextYear();
            Assert.AreEqual(PlotKind.Fertile, sim.State.GetPlot(0, 0).Kind, "a new year keeps the ground");

            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            foreach (var p in sim.State.Plots)
            {
                Assert.AreEqual(PlotKind.Normal, p.Kind);
                Assert.AreEqual(-1, p.LastYearTier, "a new farm remembers no harvest");
            }
        }
    }
}
