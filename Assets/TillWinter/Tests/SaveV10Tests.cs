using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Save schema v10 (M.3): the year's grade counters, last year's coins and stars, this year's goal, and the weather.
    /// A v9 file had none of them; it must load with no goal, clear skies and no grade yet, and play on deterministically.
    /// </summary>
    public class SaveV10Tests
    {
        // A genuine v9 file: mid-year, a fertile tomato bed planted with carrots after a year of tomatoes (a rotation).
        private const string V9Fixture = @"{
  ""SchemaVersion"": 9, ""SavedAtUnixSeconds"": 1789700000,
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
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""ring_radius"",""Level"":3}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":1}, {""Id"":""ring_shape"",""Level"":1} ],
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
  ""RingShape"": 1
}";

        [Test]
        public void V9Fixture_MigratesToV10_WithNoGoalClearSkiesAndNoGrade()
        {
            var raw = MiniJson.To<SaveData>(V9Fixture);
            Assert.AreEqual(9, raw.SchemaVersion);

            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V9Fixture), new FarmConfig());
            Assert.AreEqual(SaveData.CurrentSchemaVersion, d.SchemaVersion);
            Assert.AreEqual((int)GoalType.None, d.GoalType);
            Assert.AreEqual((int)Weather.Clear, d.Weather);
            Assert.AreEqual((int)Weather.Clear, d.PlannedWeather);
            Assert.AreEqual(0, d.LastGrade);
            Assert.AreEqual(0, d.LastYearCoins, 1e-9);
            foreach (var p in d.Plots) Assert.AreEqual(0f, p.DryTimer);
            Assert.AreEqual(1, d.Plots[0].Kind, "the fertile bed survives");
            Assert.AreEqual(820, d.Coins, 1e-9);
        }

        [Test]
        public void V9Fixture_LoadsAndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V9Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V9Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.IsFalse(a.State.Goal.Active);
            Assert.AreEqual(Weather.Clear, a.State.Weather);
            var plot = a.State.GetPlot(0, 0);
            Assert.AreEqual(PlotKind.Fertile, plot.Kind);
            Assert.IsTrue(plot.IsRotated, "carrots after a year of tomatoes");

            for (int i = 0; i < 2400; i++)
            {
                var ring = new RingInput(1f, 1f);
                a.Tick(0.05f, ring);
                b.Tick(0.05f, ring);
            }
            Assert.AreEqual(Phase.Winter, a.State.Phase, "played through to the grade");
            Assert.AreEqual(a.State.LastGrade, b.State.LastGrade);
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Generation.Harvests, b.State.Generation.Harvests);
        }
    }
}
