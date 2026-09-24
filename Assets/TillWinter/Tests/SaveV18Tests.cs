using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Save schema v18 (core loop v3): the first schema of the hoe game. A hand-written v18 file loads and plays
    /// deterministically; the next bump migrates from this fixture. Ring-game saves (v1–v17) start fresh by decision.
    /// </summary>
    public class SaveV18Tests
    {
        // A genuine v18 file: year 3 of generation 2, cracks on the field, a crop growing, one ripe with a crow on it.
        public const string V18Fixture = @"{
  ""SchemaVersion"": 18, ""SavedAtUnixSeconds"": 1790000000,
  ""Phase"": 0, ""Year"": 3, ""Season"": 1, ""YearTime"": 40.0, ""FrostWarning"": false, ""Coins"": 310,
  ""CrowSpawnTimer"": 0.4, ""RngState"": 24680, ""Stamina"": 62.5,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 1400, ""LifetimeCoinsTotal"": 5400, ""YearsThisGeneration"": 2,
  ""SeedsBanked"": 2, ""SeedsEarnedTotal"": 9, ""CrowsScared"": 12, ""Harvests"": 640,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":0,""Kind"":1,""LastYearTier"":1,""Layer"":6,""Hardpan"":false,""Chest"":false,""Hp"":20.0,""MaxHp"":32.97,""HiddenBonus"":4.4,""CropLayer"":6},
    {""X"":1,""Y"":0,""Tier"":0,""State"":1,""Progress"":0.4,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":5,""Hardpan"":false,""Chest"":false,""Hp"":0,""MaxHp"":27.03,""HiddenBonus"":3.44,""CropLayer"":5},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":4,""Hardpan"":true,""Chest"":false,""Hp"":66.46,""MaxHp"":66.46,""HiddenBonus"":21.47,""CropLayer"":4},
    {""X"":0,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":3,""Hardpan"":false,""Chest"":true,""Hp"":18.16,""MaxHp"":18.16,""HiddenBonus"":15.73,""CropLayer"":3},
    {""X"":1,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.9,""HasCrow"":false,""CrowTimer"":0,""Golden"":true,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":5,""Hardpan"":false,""Chest"":false,""Hp"":0,""MaxHp"":27.03,""HiddenBonus"":3.44,""CropLayer"":5},
    {""X"":2,""Y"":1,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":true,""CrowTimer"":1.5,""Golden"":false,""RipeAge"":5,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":4,""Hardpan"":false,""Chest"":false,""Hp"":0,""MaxHp"":22.15,""HiddenBonus"":2.68,""CropLayer"":4},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":2,""Hardpan"":false,""Chest"":false,""Hp"":14.88,""MaxHp"":14.88,""HiddenBonus"":1.64,""CropLayer"":2},
    {""X"":1,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":2,""Hardpan"":false,""Chest"":false,""Hp"":6.1,""MaxHp"":14.88,""HiddenBonus"":1.64,""CropLayer"":2},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0,""Layer"":1,""Hardpan"":false,""Chest"":false,""Hp"":12.2,""MaxHp"":12.2,""HiddenBonus"":1.28,""CropLayer"":1}
  ],
  ""Apprentices"": [ {""X"":1,""Y"":-1.2,""Role"":2} ],
  ""AlmanacLevels"": [ {""Id"":""hoe_damage"",""Level"":3}, {""Id"":""stamina_depot"",""Level"":1}, {""Id"":""growth"",""Level"":2}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":1}, {""Id"":""scarecrow"",""Level"":1}, {""Id"":""apprentice_count"",""Level"":1} ],
  ""HeritageLevels"": [ {""Id"":""h_start_damage"",""Level"":1} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": false, ""CloudSpawnTime"": 3.4e38,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 2, ""ComboTimer"": 0.3, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 511,
  ""AlmanacViewHas"": false, ""AlmanacViewX"": 0, ""AlmanacViewY"": 0, ""AlmanacViewZoom"": 1,
  ""HeritageViewHas"": false, ""HeritageViewX"": 0, ""HeritageViewY"": 0, ""HeritageViewZoom"": 1,
  ""EndingSeen"": false, ""GoldenYearActive"": false,
  ""HarvestsHand"": 500, ""HarvestsApprentice"": 100, ""HarvestsTractor"": 0, ""HarvestsLateFrost"": 10,
  ""GoldenHarvests"": 3, ""BestCombo"": 6, ""Strikes"": 2400, ""Crits"": 900, ""Breaks"": 660, ""DeepestLayer"": 6,
  ""TimePlayedSeconds"": 2600, ""YearsTotal"": 9,
  ""CoinsThisYear"": 140, ""HarvestsThisYear"": 40,
  ""YearFreshSum"": 38.5, ""CropsLostThisYear"": 1, ""LastGrade"": 2, ""LastGradeBonus"": 6, ""LastYearCoins"": 300,
  ""GoalType"": 2, ""GoalTier"": 0, ""GoalTarget"": 5, ""GoalProgress"": 3, ""GoalDone"": false, ""GoalReward"": 15,
  ""Weather"": 0, ""WeatherLeft"": 0, ""PlannedWeather"": 1, ""PlannedWeatherTime"": 55,
  ""ScarecrowX"": [3], ""ScarecrowY"": [0], ""DogCooldown"": 0,
  ""PestKind"": 0, ""PestX"": 0, ""PestY"": 0, ""PestTimer"": 0, ""PestShoo"": 0, ""HenCooldown"": 0,
  ""CloverX"": 0, ""CloverY"": 0, ""CloverLeft"": 0, ""StarLeft"": 0, ""RushLeft"": 0,
  ""TraderActive"": false, ""TraderTimeLeft"": 0, ""TraderPlannedTime"": -1, ""TraderSeedPrice"": 0, ""TraderRarePrice"": 0,
  ""TraderSeedSold"": false, ""TraderRareSold"": false, ""PestCheckTimer"": 4, ""LuckyCheckTimer"": 9,
  ""BarnStock"": 0, ""BarnCount"": 0, ""BarnStoreShare"": 0, ""BarnStoreAcc"": 0, ""BarnMarketPrice"": 1, ""BarnJars"": 0,
  ""AlmanacSpent"": 420, ""RespecUsed"": false,
  ""Trait"": 2, ""HeirOffer"": [2, 4, 6], ""Challenge"": 0, ""Achievements"": 65, ""GoalsMet"": 1, ""PestsStopped"": 2,
  ""BestGradeThisGeneration"": 2, ""HarvestsAtGenerationStart"": 600, ""NgPlus"": 0,
  ""Album"": [ {""Generation"":1,""Years"":6,""Coins"":4000,""Seeds"":9,""Harvests"":600,""BestGrade"":3,""Trait"":0,""Challenge"":0,""NgPlus"":0} ],
  ""ChecklistBits"": 31, ""AwayPlan"": 1
}";

        [Test]
        public void V18Fixture_LoadsEveryFieldOfTheHoe()
        {
            var sim = FarmSim.FromSave(MiniJson.To<SaveData>(V18Fixture), new FarmConfig());
            Assert.IsNotNull(sim);
            var s = sim.State;
            Assert.AreEqual(62.5f, s.Stamina, 1e-4f);
            Assert.AreEqual(3 + 3 + 1, s.Stats.StrikeDamage, 1e-9, "base, three hoe levels, the heritage start");
            var deep = s.GetPlot(0, 0);
            Assert.AreEqual(6, deep.Layer);
            Assert.AreEqual(GroundType.Gravel, deep.Ground);
            Assert.AreEqual(33.0, deep.MaxHp, 0.1, "10 x 1.22^6");
            Assert.AreEqual(20.0, deep.Hp, 0.1); // the cracks keep their share when the layer's HP is recomputed
            Assert.AreEqual(PlotState.Hard, deep.State);
            Assert.IsTrue(s.GetPlot(2, 0).Hardpan);
            Assert.IsTrue(s.GetPlot(0, 1).Chest);
            Assert.AreEqual(PlotState.Growing, s.GetPlot(1, 1).State);
            Assert.IsTrue(s.GetPlot(1, 1).IsGolden);
            Assert.AreEqual(PlotState.Ripe, s.GetPlot(2, 1).State);
            Assert.IsTrue(s.GetPlot(2, 1).HasCrow);
            Assert.AreEqual(1, s.Crows.Count);
            Assert.AreEqual(ApprenticeRole.Digger, s.Apprentices[0].Role);
            Assert.AreEqual(2400, s.Generation.Strikes);
            Assert.AreEqual(6, s.Generation.DeepestLayer);
            Assert.AreEqual(AwayPlan.AllHarvest, s.AwayPlan);
        }

        [Test]
        public void V18Fixture_PlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V18Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V18Fixture), new FarmConfig());
            for (int i = 0; i < 1200; i++)
            {
                if (i % 30 == 0) { a.Strike(new GridPos(0, 0)); b.Strike(new GridPos(0, 0)); }
                if (i % 200 == 199) { a.ReapAll(); b.ReapAll(); }
                a.Tick(0.05f);
                b.Tick(0.05f);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Generation.Harvests, b.State.Generation.Harvests);
            Assert.That(a.State.Generation.Breaks, Is.GreaterThan(660), "the hoe worked");
        }

        [Test]
        public void ARingGameSave_StartsFresh()
        {
            var old = MiniJson.To<SaveData>(V18Fixture);
            old.SchemaVersion = 17;
            Assert.IsNull(SaveMigrations.Migrate(old, new FarmConfig()));
            Assert.IsNull(FarmSim.FromSave(old, new FarmConfig()));
        }
    }
}
