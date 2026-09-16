using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Save schema v6: the remembered skill tree views are dropped, because they were pans and zooms into the lane
    /// layout that the radial one replaced. A v5 fixture migrates and plays.
    /// </summary>
    public class SaveV6Tests
    {
        // A genuine v5 file: mid-year, both trees with a remembered view from the old layout, this year's counters set.
        private const string V5Fixture = @"{
  ""SchemaVersion"": 5, ""SavedAtUnixSeconds"": 1789500000,
  ""Phase"": 0, ""Year"": 4, ""Season"": 0, ""YearTime"": 12.5, ""FrostWarning"": false, ""Coins"": 310,
  ""CrowSpawnTimer"": 0.5, ""RngState"": 987654321,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 1900, ""LifetimeCoinsTotal"": 6100, ""YearsThisGeneration"": 3,
  ""SeedsBanked"": 2, ""SeedsEarnedTotal"": 10, ""CrowsScared"": 5, ""Harvests"": 1200,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":0,""State"":1,""Progress"":0.3,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":0,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.7,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":2,""Tier"":0,""State"":2,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false}
  ],
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""ring_radius"",""Level"":2}, {""Id"":""irrigation"",""Level"":1} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":1} ],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": false, ""CloudSpawnTime"": 3.4028235e38,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 0, ""TractorPassed"": 0,
  ""Combo"": 0, ""ComboTimer"": 0, ""GreenhouseSecondsLeft"": 0, ""GreenhouseCoinsThisWinter"": 0,
  ""OnboardingBits"": 31,
  ""AlmanacViewHas"": true, ""AlmanacViewX"": -260, ""AlmanacViewY"": 140, ""AlmanacViewZoom"": 0.62,
  ""HeritageViewHas"": true, ""HeritageViewX"": 35, ""HeritageViewY"": -20, ""HeritageViewZoom"": 0.7,
  ""EndingSeen"": false, ""GoldenYearActive"": false,
  ""HarvestsRing"": 900, ""HarvestsApprentice"": 250, ""HarvestsTractor"": 30, ""HarvestsLateFrost"": 20,
  ""GoldenHarvests"": 2, ""BestCombo"": 8, ""TimePlayedSeconds"": 2400, ""YearsTotal"": 7,
  ""CoinsThisYear"": 140, ""HarvestsThisYear"": 60
}";

        [Test]
        public void V5Fixture_MigratesToV6_WithTreeViewsForgotten()
        {
            var raw = MiniJson.To<SaveData>(V5Fixture);
            Assert.AreEqual(5, raw.SchemaVersion);
            Assert.IsTrue(raw.AlmanacViewHas, "the fixture remembers a view");

            var d = SaveMigrations.Migrate(MiniJson.To<SaveData>(V5Fixture), new FarmConfig());
            Assert.AreEqual(6, d.SchemaVersion);
            Assert.IsFalse(d.AlmanacViewHas, "a pan into the old layout would open the tree off-centre");
            Assert.IsFalse(d.HeritageViewHas);
            Assert.AreEqual(0f, d.AlmanacViewX);
            Assert.AreEqual(0f, d.AlmanacViewY);
            Assert.AreEqual(1f, d.AlmanacViewZoom);
            Assert.AreEqual(1f, d.HeritageViewZoom);
            // Everything else survives untouched.
            Assert.AreEqual(140, d.CoinsThisYear, 1e-9);
            Assert.AreEqual(60, d.HarvestsThisYear);
            Assert.AreEqual(31, d.OnboardingBits);
            Assert.AreEqual(8, d.BestCombo);
        }

        [Test]
        public void V5Fixture_LoadsWithNoRememberedView_AndPlaysDeterministically()
        {
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V5Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V5Fixture), new FarmConfig());
            Assert.IsNotNull(a);
            Assert.IsFalse(a.State.AlmanacView.HasView);
            Assert.IsFalse(a.State.HeritageView.HasView);
            Assert.AreEqual(2, a.State.Generation.Generation);
            for (int i = 0; i < 600; i++)
            {
                a.Tick(0.1f, new RingInput(1f, 1f));
                b.Tick(0.1f, new RingInput(1f, 1f));
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins, 1e-9);
            Assert.AreEqual(a.State.Year, b.State.Year);
        }

        [Test]
        public void ARememberedView_StillRoundTripsInV6()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            sim.RememberTreeView(TreeKind.Almanac, 12f, -8f, 0.55f);
            var back = FarmSim.FromSave(sim.ToSave(), new FarmConfig());
            Assert.IsTrue(back.State.AlmanacView.HasView, "only views saved before v6 are dropped");
            Assert.AreEqual(12f, back.State.AlmanacView.PanX, 1e-5f);
            Assert.AreEqual(-8f, back.State.AlmanacView.PanY, 1e-5f);
            Assert.AreEqual(0.55f, back.State.AlmanacView.Zoom, 1e-5f);
        }
    }
}
