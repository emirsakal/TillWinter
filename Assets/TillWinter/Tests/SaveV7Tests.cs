using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Save schema v7 (M.1): plots remember how long they have stood Ripe and the player's ring shape. A v6 file has
    /// neither; it must load with fresh crops and the round ring, and play on deterministically.
    /// </summary>
    public class SaveV7Tests
    {
        // A genuine v6 file: mid-year, a Ripe plot standing, no remembered tree views (v6 dropped them).
        private const string V6Fixture = @"{
  ""SchemaVersion"": 6, ""SavedAtUnixSeconds"": 1789600000,
  ""Phase"": 0, ""Year"": 5, ""Season"": 1, ""YearTime"": 40.0, ""FrostWarning"": false, ""Coins"": 640,
  ""CrowSpawnTimer"": 1.25, ""RngState"": 24680,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 2400, ""LifetimeCoinsTotal"": 7400, ""YearsThisGeneration"": 4,
  ""SeedsBanked"": 3, ""SeedsEarnedTotal"": 10, ""CrowsScared"": 7, ""Harvests"": 1500,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":0,""Tier"":0,""State"":1,""Progress"":0.4,""HasCrow"":false,""CrowTimer"":0,""Golden"":true},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0.1,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.9,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":1,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":true,""CrowTimer"":1.5,""Golden"":false},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false}
  ],
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""ring_radius"",""Level"":3}, {""Id"":""irrigation"",""Level"":2}, {""Id"":""unlock_tomato"",""Level"":1} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":1} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": true, ""CloudSpawnTime"": 20,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 2, ""ComboTimer"": 0.4, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 31,
  ""AlmanacViewHas"": false, ""AlmanacViewX"": 0, ""AlmanacViewY"": 0, ""AlmanacViewZoom"": 1,
  ""HeritageViewHas"": false, ""HeritageViewX"": 0, ""HeritageViewY"": 0, ""HeritageViewZoom"": 1,
  ""EndingSeen"": false, ""GoldenYearActive"": false,
  ""HarvestsRing"": 1100, ""HarvestsApprentice"": 320, ""HarvestsTractor"": 50, ""HarvestsLateFrost"": 30,
  ""GoldenHarvests"": 4, ""BestCombo"": 11, ""TimePlayedSeconds"": 3000, ""YearsTotal"": 9,
  ""CoinsThisYear"": 260, ""HarvestsThisYear"": 90
}";

        [Test]
        public void V6Fixture_MigratesToV7_WithFreshCropsAndARoundRing()
        {
            var raw = MiniJson.To<SaveData>(V6Fixture);
            Assert.AreEqual(6, raw.SchemaVersion);

            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V6Fixture), new FarmConfig());
            Assert.AreEqual(SaveData.CurrentSchemaVersion, d.SchemaVersion);
            Assert.AreEqual(0, d.RingShape, "a v6 farm never chose a shape");
            foreach (var p in d.Plots) Assert.AreEqual(0f, p.RipeAge, "no crop was standing when v6 was written");
            // Everything else survives untouched.
            Assert.AreEqual(640, d.Coins, 1e-9);
            Assert.AreEqual(2, d.Combo);
            Assert.AreEqual(11, d.BestCombo);
            Assert.AreEqual(260, d.CoinsThisYear, 1e-9);
        }

        [Test]
        public void V6Fixture_LoadsAndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V6Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V6Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.AreEqual(RingShape.Round, a.State.RingShape);
            Assert.AreEqual(5, a.State.Year);

            for (int i = 0; i < 600; i++)
            {
                var ring = new RingInput(1f, 1f);
                a.Tick(0.05f, ring);
                b.Tick(0.05f, ring);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Year, b.State.Year);
            Assert.AreEqual(a.State.Generation.Harvests, b.State.Generation.Harvests);
        }
    }
}
