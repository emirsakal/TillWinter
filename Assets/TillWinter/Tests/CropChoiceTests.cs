using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.2, crops and field (GDD §2.3/§2.4 v1.7): <c>upgrade_plot</c> raises a bed's quality, the seed bag plants any
    /// crop up to it, and a crop in its liked season sells for more.
    /// </summary>
    public class CropChoiceTests
    {
        /// <summary>A farm whose first plot's bed can grow tomatoes, back in spring.</summary>
        private static FarmSim TomatoBed(FarmConfig cfg = null)
        {
            var sim = new FarmSim(cfg ?? new FarmConfig(), 1);
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(5000);
            Assert.IsTrue(sim.TryBuy("expand_field"));
            Assert.IsTrue(sim.TryBuy("unlock_tomato"));
            Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            sim.StartNextYear();
            return sim;
        }

        [Test]
        public void UpgradePlot_RaisesTheBed_AndAnUnpickedPlotFollowsIt()
        {
            var sim = TomatoBed();
            var plot = sim.State.GetPlot(0, 0);
            Assert.AreEqual(1, plot.BedTier);
            Assert.AreEqual(-1, plot.Choice);
            Assert.AreEqual(1, plot.Tier, "no pick: the bed's best crop");
            Assert.AreEqual(0, sim.State.GetPlot(1, 0).BedTier);
        }

        [Test]
        public void TheSeedBag_PlantsAnyCropUpToTheBed()
        {
            var sim = TomatoBed();
            var at = new GridPos(0, 0);
            Assert.IsTrue(sim.SetPlotCrop(at, 0), "carrots on a tomato bed");
            Assert.AreEqual(0, sim.State.GetPlot(at).Tier);
            Assert.IsFalse(sim.SetPlotCrop(at, 2), "corn is beyond this bed");
            Assert.IsFalse(sim.SetPlotCrop(new GridPos(1, 0), 1), "a plain bed grows carrots only");
            Assert.IsFalse(sim.SetPlotCrop(at, -2));
            Assert.IsTrue(sim.SetPlotCrop(at, -1), "back to following the bed");
            Assert.AreEqual(1, sim.State.GetPlot(at).Tier);
        }

        [Test]
        public void ADifferentCrop_Replants_ButTheSameCropKeepsItsProgress()
        {
            var sim = TomatoBed();
            var at = new GridPos(0, 0);
            var plot = sim.State.GetPlot(at);
            Assert.IsTrue(sim.DebugBreak(at));
            for (int i = 0; i < 10; i++) sim.Tick(0.05f);
            Assert.That(plot.Progress, Is.GreaterThan(0f));
            var state = plot.State;
            float progress = plot.Progress;

            Assert.IsTrue(sim.SetPlotCrop(at, 1), "picking the crop already growing");
            Assert.AreEqual(state, plot.State);
            Assert.AreEqual(progress, plot.Progress);

            Assert.IsTrue(sim.SetPlotCrop(at, 0));
            Assert.AreEqual(PlotState.Growing, plot.State, "a new crop starts from its seed");
            Assert.AreEqual(0f, plot.Progress);
        }

        [Test]
        public void ThePick_SurvivesALaterBedUpgrade()
        {
            var sim = TomatoBed();
            Assert.IsTrue(sim.SetPlotCrop(new GridPos(0, 0), 0));
            sim.DebugSkipToWinter();
            sim.DebugAddCoins(1e7);
            Assert.IsTrue(sim.TryBuy("unlock_corn"));
            while (sim.State.GetPlot(0, 0).BedTier < 2) Assert.IsTrue(sim.TryBuy("upgrade_plot"));
            Assert.AreEqual(0, sim.State.GetPlot(0, 0).Tier, "still carrots: the player chose them");
        }

        [Test]
        public void TheSeedBag_IsClosedOutsideTheYear()
        {
            var sim = TomatoBed();
            sim.DebugSkipToWinter();
            Assert.IsFalse(sim.SetPlotCrop(new GridPos(0, 0), 0));
        }

        [Test]
        public void AnInSeasonCrop_SellsForMore()
        {
            double Coins(Season likes)
            {
                var cfg = new FarmConfig();
                cfg.Crops[0].Likes = likes;
                var sim = new FarmSim(cfg, 5);
                Assert.AreEqual(Season.Spring, sim.State.Season);
                var at = new GridPos(0, 0);
                Assert.IsTrue(sim.DebugForceRipe(at));
                double before = sim.State.Coins;
                Assert.IsTrue(sim.ReapOne(at));
                return sim.State.Coins - before;
            }

            double inSeason = Coins(Season.Spring), outOfSeason = Coins(Season.Summer);
            Assert.That(outOfSeason, Is.GreaterThan(0));
            Assert.AreEqual(outOfSeason * new FarmConfig().InSeasonValue, inSeason, 1e-9);
        }

        [Test]
        public void EveryCrop_LikesASeasonOfTheYear()
        {
            foreach (var crop in new FarmConfig().Crops)
                Assert.That(crop.Likes, Is.Not.EqualTo(Season.Winter), crop.Key);
        }
    }
}
