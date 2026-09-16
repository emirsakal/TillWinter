using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Save schema v5: this year's coins and harvests (the Winter screen's year summary); a v4 fixture migrates.</summary>
    public class SaveV5Tests
    {
        // A genuine v4 file (Session 9 shape): mid-year, ending unseen, per-source harvests counted, no v5 fields.
        private const string V4Fixture = @"{
  ""SchemaVersion"": 4, ""SavedAtUnixSeconds"": 1789400000,
  ""Phase"": 0, ""Year"": 7, ""Season"": 1, ""YearTime"": 22.5, ""FrostWarning"": false, ""Coins"": 880,
  ""CrowSpawnTimer"": 0.5, ""RngState"": 123456789,
  ""Generation"": 3, ""LifetimeCoinsThisGeneration"": 4200, ""LifetimeCoinsTotal"": 15400, ""YearsThisGeneration"": 6,
  ""SeedsBanked"": 4, ""SeedsEarnedTotal"": 22, ""CrowsScared"": 11, ""Harvests"": 3100,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":1,""Progress"":0.2,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":0,""Tier"":1,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.6,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":2,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false}
  ],
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""irrigation"",""Level"":2}, {""Id"":""ring_radius"",""Level"":3}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":3} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":2} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": true, ""CloudSpawnTime"": 3.4028235e38,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 0, ""ComboTimer"": 0, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 15,
  ""AlmanacViewHas"": true, ""AlmanacViewX"": -40, ""AlmanacViewY"": 10, ""AlmanacViewZoom"": 0.85,
  ""HeritageViewHas"": false, ""HeritageViewX"": 0, ""HeritageViewY"": 0, ""HeritageViewZoom"": 1,
  ""EndingSeen"": false, ""GoldenYearActive"": false,
  ""HarvestsRing"": 2200, ""HarvestsApprentice"": 700, ""HarvestsTractor"": 150, ""HarvestsLateFrost"": 50,
  ""GoldenHarvests"": 9, ""BestCombo"": 12, ""TimePlayedSeconds"": 5400, ""YearsTotal"": 19
}";

        [Test]
        public void V4Fixture_MigratesToV5_WithThisYearAtZero()
        {
            var raw = MiniJson.To<SaveData>(V4Fixture);
            Assert.AreEqual(4, raw.SchemaVersion);
            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V4Fixture), new FarmConfig());
            Assert.AreEqual(5, d.SchemaVersion);
            Assert.AreEqual(0, d.CoinsThisYear, "v4 never counted the year separately");
            Assert.AreEqual(0, d.HarvestsThisYear);
            // v4 fields survive untouched.
            Assert.AreEqual(2200, d.HarvestsRing);
            Assert.AreEqual(12, d.BestCombo);
            Assert.AreEqual(19, d.YearsTotal);
            Assert.AreEqual(15, d.OnboardingBits);
        }

        [Test]
        public void V4Fixture_LoadsAndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V4Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V4Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.AreEqual(3, a.State.Generation.Generation);
            for (int i = 0; i < 600; i++)
            {
                a.Tick(0.1f, new RingInput(1f, 1f));
                b.Tick(0.1f, new RingInput(1f, 1f));
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.CoinsThisYear, b.State.CoinsThisYear, 1e-9);
            Assert.Greater(a.State.CoinsThisYear, 0, "the year starts counting after the migration");
        }

        [Test]
        public void ThisYearCounters_CountAndResetEveryYear()
        {
            var sim = new FarmSim(new FarmConfig(), 4);
            sim.DebugForceRipeAll();
            for (int i = 0; i < 200; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            Assert.Greater(sim.State.CoinsThisYear, 0);
            Assert.Greater(sim.State.HarvestsThisYear, 0);
            double firstYear = sim.State.CoinsThisYear;

            sim.DebugSkipToWinter();
            Assert.AreEqual(firstYear, sim.State.CoinsThisYear, 1e-9, "Winter keeps the year's numbers on screen");
            sim.StartNextYear();
            Assert.AreEqual(0, sim.State.CoinsThisYear, 1e-9, "a new year starts from zero");
            Assert.AreEqual(0, sim.State.HarvestsThisYear);
        }

        [Test]
        public void ThisYearCounters_RoundTrip()
        {
            var sim = new FarmSim(new FarmConfig(), 6);
            sim.DebugForceRipeAll();
            for (int i = 0; i < 120; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            var back = FarmSim.FromSave(sim.ToSave(), new FarmConfig());
            Assert.AreEqual(sim.State.CoinsThisYear, back.State.CoinsThisYear, 1e-9);
            Assert.AreEqual(sim.State.HarvestsThisYear, back.State.HarvestsThisYear);
        }
    }
}
