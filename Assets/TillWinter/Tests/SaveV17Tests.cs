using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Save schema v17 (play-test fixes): the tap-harvest cooldown is saved. A v16 farm loads with no tap harvest waiting.
    /// </summary>
    public class SaveV17Tests
    {
        // A genuine v16 file: the checklist done and a balanced away plan.
        private const string V16Fixture = @"{
  ""SchemaVersion"": 16, ""SavedAtUnixSeconds"": 1789700000,
  ""Phase"": 0, ""Year"": 6, ""Season"": 1, ""YearTime"": 45.0, ""FrostWarning"": false, ""Coins"": 820,
  ""CrowSpawnTimer"": 2.0, ""RngState"": 13579,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 2800, ""LifetimeCoinsTotal"": 7800, ""YearsThisGeneration"": 5,
  ""SeedsBanked"": 3, ""SeedsEarnedTotal"": 10, ""CrowsScared"": 9, ""Harvests"": 1700,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":14.5,""Choice"":0,""Kind"":1,""LastYearTier"":1},
    {""X"":1,""Y"":0,""Tier"":0,""State"":1,""Progress"":0.4,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0.1,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":0,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":1,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.9,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":2,""Y"":1,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":true,""CrowTimer"":1.5,""Golden"":false,""RipeAge"":3,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":1,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false,""RipeAge"":0,""Choice"":-1,""Kind"":0,""LastYearTier"":0}
  ],
  ""Apprentices"": [ {""X"":1,""Y"":-1.2,""Role"":1} ],
  ""AlmanacLevels"": [ {""Id"":""ring_radius"",""Level"":3}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":1}, {""Id"":""ring_shape"",""Level"":1}, {""Id"":""scarecrow"",""Level"":2}, {""Id"":""apprentice_count"",""Level"":1} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":1} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": true, ""CloudSpawnTime"": 20,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 3, ""ComboTimer"": 0.5, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 31,
  ""AlmanacViewHas"": false, ""AlmanacViewX"": 0, ""AlmanacViewY"": 0, ""AlmanacViewZoom"": 1,
  ""HeritageViewHas"": false, ""HeritageViewX"": 0, ""HeritageViewY"": 0, ""HeritageViewZoom"": 1,
  ""EndingSeen"": false, ""GoldenYearActive"": false,
  ""HarvestsRing"": 1250, ""HarvestsApprentice"": 350, ""HarvestsTractor"": 60, ""HarvestsLateFrost"": 40,
  ""GoldenHarvests"": 5, ""BestCombo"": 14, ""TimePlayedSeconds"": 3500, ""YearsTotal"": 10,
  ""CoinsThisYear"": 300, ""HarvestsThisYear"": 100,
  ""RingShape"": 1,
  ""YearFreshSum"": 88.5, ""CropsLostThisYear"": 2, ""LastGrade"": 2, ""LastGradeBonus"": 14, ""LastYearCoins"": 700,
  ""GoalType"": 1, ""GoalTier"": 0, ""GoalTarget"": 18, ""GoalProgress"": 7, ""GoalDone"": false, ""GoalReward"": 35,
  ""Weather"": 0, ""WeatherLeft"": 0, ""PlannedWeather"": 2, ""PlannedWeatherTime"": 50,
  ""ScarecrowX"": [0, 3], ""ScarecrowY"": [0, 3], ""DogCooldown"": 4.5,
  ""PestKind"": 1, ""PestX"": 2, ""PestY"": 1, ""PestTimer"": 1.5, ""PestShoo"": 0, ""HenCooldown"": 0,
  ""CloverX"": 0, ""CloverY"": 0, ""CloverLeft"": 0, ""StarLeft"": 0, ""RushLeft"": 0,
  ""TraderActive"": false, ""TraderTimeLeft"": 0, ""TraderPlannedTime"": 60, ""TraderSeedPrice"": 0, ""TraderRarePrice"": 0,
  ""TraderSeedSold"": false, ""TraderRareSold"": false, ""PestCheckTimer"": 7, ""LuckyCheckTimer"": 3,
  ""BarnStock"": 120, ""BarnCount"": 9, ""BarnStoreShare"": 0.25, ""BarnStoreAcc"": 0.5, ""BarnMarketPrice"": 1, ""BarnJars"": 0,
  ""AlmanacSpent"": 900, ""RespecUsed"": false,
  ""Trait"": 3, ""HeirOffer"": [3, 1, 6], ""Challenge"": 0, ""Achievements"": 97, ""GoalsMet"": 2, ""PestsStopped"": 4,
  ""BestGradeThisGeneration"": 2, ""HarvestsAtGenerationStart"": 1500, ""NgPlus"": 0,
  ""ChecklistBits"": 31, ""AwayPlan"": 2,
  ""Album"": [ {""Generation"":1,""Years"":7,""Coins"":3600,""Seeds"":10,""Harvests"":1500,""BestGrade"":3,""Trait"":0,""Challenge"":0,""NgPlus"":0} ]
}";

        [Test]
        public void V16Fixture_MigratesToV17_WithNoCooldown()
        {
            var raw = MiniJson.To<SaveData>(V16Fixture);
            Assert.AreEqual(16, raw.SchemaVersion);

            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V16Fixture), new FarmConfig());
            Assert.AreEqual(SaveData.CurrentSchemaVersion, d.SchemaVersion);
            Assert.AreEqual(0f, d.TapCooldown);
            Assert.AreEqual((int)AwayPlan.Balanced, d.AwayPlan, "the v16 away plan survives");
            Assert.AreEqual(1, d.Album.Length);
        }

        [Test]
        public void V16Fixture_LoadsAndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V16Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V16Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.AreEqual(AwayPlan.Balanced, a.State.AwayPlan);
            for (int i = 0; i < 1200; i++)
            {
                var ring = new RingInput(1f, 1f);
                a.Tick(0.05f, ring);
                b.Tick(0.05f, ring);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Generation.Harvests, b.State.Generation.Harvests);
        }
    }
}
