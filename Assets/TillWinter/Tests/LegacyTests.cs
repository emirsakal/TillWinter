using System.Collections.Generic;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// M.7, progression and rebirth (GDD §7.3–§7.6 v2.3): exclusive Heritage paths, heirs with traits, challenge
    /// generations and achievements that leave heirlooms.
    /// </summary>
    public class LegacyTests
    {
        private static FarmConfig Cfg()
        {
            var cfg = new FarmConfig();
            cfg.CrowSpawnChance = 0f;
            cfg.WeatherFirstYear = cfg.GoalFirstYear = int.MaxValue;
            cfg.PestFirstYear = cfg.LuckyFirstYear = cfg.TraderFirstYear = int.MaxValue;
            return cfg;
        }

        private static FarmSim Retired(FarmConfig cfg, int seed = 1)
        {
            var sim = new FarmSim(cfg, seed);
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            return sim;
        }

        [Test]
        public void ARebirth_OffersThreeDifferentHeirs_AndThePlayerPicksOne()
        {
            var sim = Retired(Cfg());
            var offer = sim.State.Generation.HeirOffer;
            var seen = new HashSet<HeirTrait>();
            foreach (var t in offer)
            {
                Assert.AreNotEqual(HeirTrait.None, t);
                Assert.IsTrue(seen.Add(t), "three different heirs");
            }
            Assert.AreEqual(offer[0], sim.State.Generation.Trait, "the first is chosen until the player picks");
            Assert.IsTrue(sim.ChooseHeir(2));
            Assert.AreEqual(offer[2], sim.State.Generation.Trait);
            Assert.IsFalse(sim.ChooseHeir(3));
            sim.StartNewGeneration();
            Assert.IsFalse(sim.ChooseHeir(0), "only while handing the farm on");
        }

        [Test]
        public void AnHeirsTrait_ShapesTheirGeneration()
        {
            var cfg = Cfg();
            cfg.HeirloomsEnabled = false; // compare the trait alone
            Stats Resolve() => StatResolver.Resolve(cfg, new Dictionary<string, int> { ["growth"] = 2 }, new Dictionary<string, int>());
            var none = Resolve();
            var s = Resolve();
            Legacy.Apply(s, cfg, 0, HeirTrait.GreenThumb, ChallengeKind.None);
            Assert.AreEqual(none.GrowthMult * (1f + (float)cfg.TraitGreenThumb), s.GrowthMult, 1e-5, "green thumb: crops grow faster");
            var q = Resolve();
            Legacy.Apply(q, cfg, 0, HeirTrait.QuickHands, ChallengeKind.None);
            Assert.AreEqual(none.StrikeCooldown / (1f + (float)cfg.TraitQuickHands), q.StrikeCooldown, 1e-5, "quick hands: a shorter strike cooldown");
        }

        [Test]
        public void AChallenge_TakesTheHelpers_OrShortensTheYears_AndPaysMoreSeeds()
        {
            var cfg = Cfg();
            var sim = Retired(cfg);
            Assert.IsTrue(sim.SetChallenge(ChallengeKind.NoHelpers));
            sim.DebugSetLevel("apprentice_count", 3);
            Assert.AreEqual(0, sim.State.Stats.ApprenticeCount, "no helpers this generation");

            Assert.IsTrue(sim.SetChallenge(ChallengeKind.ShortYears));
            float plain = new FarmSim(Cfg(), 1).State.Stats.YearLength;
            Assert.AreEqual(plain * cfg.ChallengeShortYear, sim.State.Stats.YearLength, 1e-3);

            sim.StartNewGeneration();
            Assert.IsFalse(sim.SetChallenge(ChallengeKind.None), "chosen before the generation starts");
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            int plainSeeds = sim.SeedsFor(sim.State.Generation.LifetimeCoinsThisGeneration);
            Assert.AreEqual((int)System.Math.Floor(plainSeeds * cfg.ChallengeSeedBonus), sim.SeedsIfRetiredNow);
        }

        [Test]
        public void TheFirstHarvest_LeavesAnHeirloom_OnceForGood()
        {
            var cfg = Cfg();
            var sim = new FarmSim(cfg, 1);
            int unlocked = 0;
            sim.AchievementUnlocked += id => { if (id == AchievementId.FirstHarvest) unlocked++; };
            double before = sim.State.Stats.CropValueMult;
            sim.DebugForceRipeAll();
            Assert.IsTrue(sim.ReapOne(new GridPos(0, 0)));
            sim.Tick(0.05f); // achievements are checked on the tick
            Assert.AreEqual(1, unlocked);
            Assert.IsTrue(Legacy.Has(sim.State.Generation.Achievements, AchievementId.FirstHarvest));
            Assert.AreEqual(before * (1 + Legacy.Heirloom(AchievementId.FirstHarvest).value), sim.State.Stats.CropValueMult, 1e-9);

            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(cfg.HeritageThreshold);
            Assert.IsTrue(sim.Retire());
            Assert.IsTrue(Legacy.Has(sim.State.Generation.Achievements, AchievementId.FirstHarvest), "heirlooms stay in the family");
            Assert.IsTrue(Legacy.Has(sim.State.Generation.Achievements, AchievementId.SecondGeneration));
        }

        [Test]
        public void ANineCropSwipe_LeavesTheComboHeirloom()
        {
            var sim = new FarmSim(Cfg(), 1);
            sim.DebugForceRipeAll();
            Assert.AreEqual(8, sim.Reap(new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(0, 1),
                new GridPos(1, 1), new GridPos(2, 1), new GridPos(0, 2), new GridPos(1, 2),
            }));
            sim.Tick(0.05f);
            Assert.IsFalse(Legacy.Has(sim.State.Generation.Achievements, AchievementId.Combo9), "eight is not nine");
            sim.DebugForceRipeAll();
            Assert.AreEqual(9, sim.ReapAll());
            sim.Tick(0.05f);
            Assert.IsTrue(Legacy.Has(sim.State.Generation.Achievements, AchievementId.Combo9));
        }

        [Test]
        public void ExclusivePaths_RuleEachOtherOut_AndTheEndingCountsAPathNotTaken()
        {
            var sim = Retired(Cfg());
            sim.DebugAddSeeds(200);
            foreach (var pre in new[] { "h_start_damage", "h_strike_speed", "h_break_coins", "h_free_apprentice", "h_apprentice_yield" })
                Assert.IsTrue(sim.TryBuy(pre), pre);
            Assert.IsTrue(sim.CanBuy("h_steward"));
            Assert.IsTrue(sim.TryBuy("h_hoe_master"));
            Assert.IsFalse(sim.CanBuy("h_steward"), "the other path is closed");

            foreach (var n in HeritageData.Nodes)
                if (n.Id != "h_steward" && n.Id != "h_rich_soil") sim.DebugSetLevel(n.Id, n.MaxLevel);
            Assert.IsTrue(sim.HeritageComplete, "a path not taken does not block the ending");
        }

        [Test]
        public void TheTables_ValidateTheExclusions()
        {
            Assert.IsEmpty(SkillTree.ValidateAll(AlmanacData.Nodes, HeritageData.Nodes));
            Assert.AreEqual("h_steward", HeritageData.Get("h_hoe_master").Excludes);
            Assert.AreEqual("h_rich_soil", HeritageData.Get("h_long_summer").Excludes);
        }

        [Test]
        public void WithHeirsOff_ARebirthDrawsNoHeir()
        {
            var sim = Retired(TestConfig.Classic());
            Assert.AreEqual(HeirTrait.None, sim.State.Generation.Trait);
        }
    }
}
