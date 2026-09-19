using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.2, the rest of crops and field (GDD §2.3/§2.4 v1.8): mixed neighbours, crop rotation, and the fertile or
    /// stony ground a field expansion adds.
    /// </summary>
    public class FieldVarietyTests
    {
        /// <summary>Only the rule under test pays: seasons off, no special ground unless a test asks for it.</summary>
        private static FarmConfig Plain()
        {
            var cfg = new FarmConfig();
            cfg.InSeasonValue = 1;
            cfg.StonyChance = 0;
            cfg.FertileChance = 0;
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
        public void AStonyNeighbour_IsNotVariety()
        {
            var sim = TomatoBed(Plain());
            sim.DebugSetPlotKind(new GridPos(1, 0), PlotKind.Stony);
            Assert.AreEqual(1, sim.DifferentNeighbours(sim.State.GetPlot(0, 0)));
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
        public void AStonyPlot_GrowsNothing_UntilTheRingHasClearedIt()
        {
            var cfg = Plain();
            var sim = new FarmSim(cfg, 3);
            sim.DebugSetLevel("irrigation", 3);
            var at = new GridPos(1, 1);
            var plot = sim.State.GetPlot(at);
            sim.DebugSetPlotKind(at, PlotKind.Stony);
            int cleared = 0;
            sim.PlotCleared += p => { cleared++; Assert.AreEqual(at, p); };

            for (int i = 0; i < 100; i++) sim.Tick(0.05f, null);
            Assert.IsTrue(plot.IsStony, "irrigation does not move stones");
            Assert.AreEqual(PlotState.Dry, plot.State);
            Assert.IsFalse(sim.SetPlotCrop(at, 0), "nothing can be planted on stones");

            float t = 0f;
            while (t < cfg.StoneClearSeconds - 0.2f) { sim.Tick(0.05f, new RingInput(1f, 1f)); t += 0.05f; }
            Assert.IsTrue(plot.IsStony, "not cleared yet");
            Assert.AreEqual(PlotState.Dry, plot.State, "the ring over stones waters nothing");
            for (int i = 0; i < 8; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            Assert.IsFalse(plot.IsStony);
            Assert.AreEqual(PlotKind.Normal, plot.Kind);
            Assert.AreEqual(1, cleared);

            for (int i = 0; i < 40; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            Assert.AreNotEqual(PlotState.Dry, plot.State, "cleared ground grows like any other");
        }

        [Test]
        public void TheRainCloud_PassesOverStones()
        {
            var sim = new FarmSim(Plain(), 3);
            sim.DebugSetPlotKind(new GridPos(0, 0), PlotKind.Stony);
            Assert.IsTrue(sim.DebugSpawnCloud());
            Assert.IsTrue(sim.TapCloud());
            Assert.AreEqual(PlotState.Dry, sim.State.GetPlot(0, 0).State);
            Assert.AreEqual(PlotState.Wet, sim.State.GetPlot(1, 1).State);
        }

        [Test]
        public void AFieldExpansion_MayAddSpecialGround_ButTheOldFieldStaysPlain()
        {
            var cfg = Plain();
            cfg.StonyChance = 1; // every new plot stony, so the rule shows
            var sim = new FarmSim(cfg, 3);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(5000);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            foreach (var p in sim.State.Plots)
            {
                bool added = p.Pos.X >= 3 || p.Pos.Y >= 3;
                Assert.AreEqual(added ? PlotKind.Stony : PlotKind.Normal, p.Kind, p.Pos.ToString());
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
