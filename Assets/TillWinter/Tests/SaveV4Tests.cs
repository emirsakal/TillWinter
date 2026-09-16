using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Save schema v4 (Session 9): ending flags and stats-screen counters; a v3 fixture migrates.</summary>
    public class SaveV4Tests
    {
        // A genuine v3 file (Session 5 shape): mid-year, two hints seen, Almanac view remembered, no v4 fields.
        private const string V3Fixture = @"{
  ""SchemaVersion"": 3, ""SavedAtUnixSeconds"": 1789300000,
  ""Phase"": 0, ""Year"": 5, ""Season"": 1, ""YearTime"": 41.5, ""FrostWarning"": false, ""Coins"": 1210,
  ""CrowSpawnTimer"": 1.5, ""RngState"": 987654321,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 3100, ""LifetimeCoinsTotal"": 9400, ""YearsThisGeneration"": 4,
  ""SeedsBanked"": 3, ""SeedsEarnedTotal"": 10, ""CrowsScared"": 7, ""Harvests"": 2150,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":1,""Progress"":0.4,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":0,""Tier"":1,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":true},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.1,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":2,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false}
  ],
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""irrigation"",""Level"":1}, {""Id"":""ring_radius"",""Level"":2}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":2} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":1} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": false, ""CloudSpawnTime"": 3.4028235e38,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 2, ""ComboTimer"": 0.3, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 3,
  ""AlmanacViewHas"": true, ""AlmanacViewX"": -120, ""AlmanacViewY"": 40, ""AlmanacViewZoom"": 0.9,
  ""HeritageViewHas"": false, ""HeritageViewX"": 0, ""HeritageViewY"": 0, ""HeritageViewZoom"": 1
}";

        [Test]
        public void V3Fixture_MigratesToV4_WithEndingUnseenAndStatsAtZero()
        {
            var raw = MiniJson.To<SaveData>(V3Fixture);
            Assert.AreEqual(3, raw.SchemaVersion);
            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V3Fixture), new FarmConfig());
            Assert.AreEqual(SaveData.CurrentSchemaVersion, d.SchemaVersion); // the chain always ends at the current version
            Assert.IsFalse(d.EndingSeen);
            Assert.IsFalse(d.GoldenYearActive);
            Assert.AreEqual(0, d.HarvestsRing + d.HarvestsApprentice + d.HarvestsTractor + d.HarvestsLateFrost);
            Assert.AreEqual(0, d.GoldenHarvests);
            Assert.AreEqual(0, d.BestCombo);
            Assert.AreEqual(0, d.TimePlayedSeconds);
            Assert.AreEqual(4, d.YearsTotal, "earlier generations' years are unknown; this generation's carry over");
            // v3 fields survive untouched.
            Assert.AreEqual(3, d.OnboardingBits);
            Assert.AreEqual(2150, d.Harvests);
            // The chain runs on to the current version, and v6 drops views remembered for the old tree layout.
            Assert.IsFalse(d.AlmanacViewHas);
        }

        [Test]
        public void V3Fixture_LoadsAndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V3Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V3Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.AreEqual(2, a.State.Generation.Generation);
            Assert.IsFalse(a.State.EndingSeen);
            for (int i = 0; i < 600; i++)
            {
                a.Tick(0.1f, new RingInput(1f, 0f));
                b.Tick(0.1f, new RingInput(1f, 0f));
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Generation.HarvestsRing, b.State.Generation.HarvestsRing);
            Assert.Greater(a.State.Generation.HarvestsRing, 0, "stats start counting after the migration");
        }

        [Test]
        public void V4Fields_RoundTrip()
        {
            var sim = new FarmSim(new FarmConfig(), 9);
            sim.DebugForceRipeAll();
            for (int i = 0; i < 200; i++) sim.Tick(0.05f, new RingInput(1f, 1f));
            sim.AddPlayTime(123.25);
            var g = sim.State.Generation;
            var back = FarmSim.FromSave(sim.ToSave(), new FarmConfig());
            var h = back.State.Generation;
            Assert.AreEqual(g.HarvestsRing, h.HarvestsRing);
            Assert.AreEqual(g.HarvestsApprentice, h.HarvestsApprentice);
            Assert.AreEqual(g.HarvestsTractor, h.HarvestsTractor);
            Assert.AreEqual(g.HarvestsLateFrost, h.HarvestsLateFrost);
            Assert.AreEqual(g.GoldenHarvests, h.GoldenHarvests);
            Assert.AreEqual(g.BestCombo, h.BestCombo);
            Assert.AreEqual(g.TimePlayedSeconds, h.TimePlayedSeconds, 1e-9);
            Assert.AreEqual(g.YearsTotal, h.YearsTotal);
            Assert.AreEqual(sim.State.EndingSeen, back.State.EndingSeen);
        }

        [Test]
        public void GoldenYear_SurvivesASaveMidYear()
        {
            var sim = new FarmSim(new FarmConfig(), 5);
            sim.DebugMaxHeritage();
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
            sim.DebugSkipToWinter();
            Assert.IsTrue(sim.Retire());
            sim.StartNewGeneration();
            for (int i = 0; i < 100; i++) sim.Tick(0.5f, null);
            var back = FarmSim.FromSave(sim.ToSave(), new FarmConfig());
            Assert.IsTrue(back.State.GoldenYearActive);
            Assert.AreEqual(300f, back.State.Stats.YearLength, 1e-4f, "the Golden Year's length is re-resolved after load");
            Assert.AreEqual(sim.State.YearTime, back.State.YearTime, 1e-4f);
        }

        [Test]
        public void UnknownFutureVersion_ReturnsNull()
        {
            var d = MiniJson.To<SaveData>(V3Fixture);
            d.SchemaVersion = SaveData.CurrentSchemaVersion + 1;
            Assert.IsNull(SaveMigrations.Migrate(d, new FarmConfig()));
        }
    }
}
