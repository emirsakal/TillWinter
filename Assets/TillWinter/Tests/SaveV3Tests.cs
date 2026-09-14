using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Save schema v3 (Session 5): onboarding flags and per-tree view memory; v2 fixture migrates.</summary>
    public class SaveV3Tests
    {
        // A genuine v2 file (Session 3 shape): Winter, tractor mid-idle, one golden plot, no v3 fields.
        private const string V2Fixture = @"{
  ""SchemaVersion"": 2, ""SavedAtUnixSeconds"": 1789100000,
  ""Phase"": 1, ""Year"": 4, ""Season"": 3, ""YearTime"": 90, ""FrostWarning"": false, ""Coins"": 640,
  ""CrowSpawnTimer"": 0, ""RngState"": 123456789,
  ""Generation"": 1, ""LifetimeCoinsThisGeneration"": 2100, ""LifetimeCoinsTotal"": 2100, ""YearsThisGeneration"": 3,
  ""SeedsBanked"": 0, ""SeedsEarnedTotal"": 0, ""CrowsScared"": 2, ""Harvests"": 800,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":true},
    {""X"":1,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":1,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":1,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0,""HasCrow"":false,""CrowTimer"":0,""Golden"":false}
  ],
  ""Apprentices"": [],
  ""AlmanacLevels"": [ {""Id"":""irrigation"",""Level"":1}, {""Id"":""tractor"",""Level"":1}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":1}, {""Id"":""greenhouse"",""Level"":1} ],
  ""HeritageLevels"": [],
  ""CloudActive"": false, ""CloudX"": 0, ""CloudTimeLeft"": 0, ""CloudSpawnedThisYear"": false, ""CloudSpawnTime"": 3.4028235e38,
  ""TractorRow"": 0, ""TractorX"": -0.5, ""TractorSweeping"": false, ""TractorTimeToNextSweep"": 30, ""TractorPassed"": 0,
  ""Combo"": 0, ""ComboTimer"": 0, ""GreenhouseSecondsLeft"": 42.5, ""GreenhouseCoinsThisWinter"": 3.15
}";

        [Test]
        public void V2Fixture_MigratesToV3_LoadsWithNoHintsAndNoViews()
        {
            var raw = MiniJson.To<SaveData>(V2Fixture);
            Assert.AreEqual(2, raw.SchemaVersion);
            var migrated = SaveMigrations.Migrate(MiniJson.To<SaveData>(V2Fixture), new FarmConfig());
            Assert.AreEqual(3, migrated.SchemaVersion);
            Assert.AreEqual(0, migrated.OnboardingBits);
            Assert.IsFalse(migrated.AlmanacViewHas);
            Assert.AreEqual(1f, migrated.AlmanacViewZoom);
            Assert.AreEqual(42.5f, migrated.GreenhouseSecondsLeft, "v2 fields untouched");

            var sim = FarmSim.FromSave(raw, new FarmConfig());
            Assert.IsNotNull(sim);
            Assert.AreEqual(Phase.Winter, sim.State.Phase);
            Assert.AreEqual(0, sim.State.Onboarding.Count);
            foreach (Hint h in System.Enum.GetValues(typeof(Hint))) Assert.IsTrue(sim.HintPending(h), h.ToString());
            Assert.IsFalse(sim.State.AlmanacView.HasView);
            Assert.IsTrue(sim.State.GetPlot(0, 0).IsGolden);
            Assert.IsTrue(sim.State.Tractor.Owned);
            Assert.That(sim.State.Greenhouse.SecondsLeftThisWinter, Is.EqualTo(42.5f));
        }

        [Test]
        public void V1Fixture_StillMigrates_ThroughBothSteps()
        {
            var v1 = new SaveData { SchemaVersion = 1, GridSize = 1, Plots = new[] { new PlotSave() }, AlmanacLevels = new LevelPair[0], HeritageLevels = new LevelPair[0] };
            var m = SaveMigrations.Migrate(v1, new FarmConfig());
            Assert.AreEqual(3, m.SchemaVersion);
            Assert.AreEqual(0, m.OnboardingBits);
            Assert.IsNull(SaveMigrations.Migrate(new SaveData { SchemaVersion = 4 }));
        }

        [Test]
        public void OnboardingFlags_RoundTrip_AndFireOnce()
        {
            var sim = new FarmSim(new FarmConfig(), 1);
            var shown = new List<Hint>();
            sim.HintShown += shown.Add;
            Assert.IsTrue(sim.MarkHint(Hint.FirstTouch));
            Assert.IsFalse(sim.MarkHint(Hint.FirstTouch), "fires once, ever");
            Assert.IsTrue(sim.MarkHint(Hint.FirstCrow));
            Assert.AreEqual(2, shown.Count);
            Assert.AreEqual(2, sim.State.Onboarding.Count);
            Assert.IsTrue(sim.State.Onboarding.Has(Hint.FirstCrow));
            Assert.IsFalse(sim.State.Onboarding.Has(Hint.Hold));

            // Survives retire and save.
            sim.DebugSkipToWinter();
            sim.DebugAddLifetimeCoins(5000);
            Assert.IsTrue(sim.Retire());
            Assert.IsTrue(sim.State.Onboarding.Has(Hint.FirstTouch), "never reset");
            sim.RememberTreeView(TreeKind.Almanac, 12f, -34f, 0.8f);
            var loaded = FarmSim.FromSave(sim.ToSave(), new FarmConfig());
            Assert.AreEqual(sim.State.Onboarding.Bits, loaded.State.Onboarding.Bits);
            Assert.IsTrue(loaded.State.AlmanacView.HasView);
            Assert.AreEqual(12f, loaded.State.AlmanacView.PanX);
            Assert.AreEqual(-34f, loaded.State.AlmanacView.PanY);
            Assert.AreEqual(0.8f, loaded.State.AlmanacView.Zoom);
            Assert.IsFalse(loaded.State.HeritageView.HasView);
            Assert.AreEqual(3, sim.ToSave().SchemaVersion);
        }

        [Test]
        public void OfflineReport_CountsHarvestsBySource()
        {
            var sim = new FarmSim(new FarmConfig(), 3);
            sim.DebugSetLevel("irrigation", 3);
            sim.DebugSetLevel("sun", 3);
            sim.DebugSetLevel("apprentice_count", 1);
            sim.DebugSetLevel("tractor", 3);
            var r = sim.SimulateOffline(600);
            Assert.That(r.Harvests, Is.GreaterThan(0));
            Assert.AreEqual(r.Harvests, r.HarvestsApprentice + r.HarvestsTractor, "offline harvests come from helpers only");
            Assert.That(r.HarvestsApprentice, Is.GreaterThan(0));
        }

        [Test]
        public void GenerationFlavourStrings_ExistFor2To10_AndFallback()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/TillWinter/Unity/Localization/en.json");
            var table = MiniJson.ParseObject(File.ReadAllText(path));
            for (int g = 2; g <= 10; g++)
            {
                Assert.IsTrue(table.ContainsKey("gen.flavour." + g), "gen.flavour." + g);
                Assert.IsNotEmpty((string)table["gen.flavour." + g]);
            }
            Assert.IsTrue(table.ContainsKey("gen.flavour.default"));
            foreach (var key in new[] { "hint.first_touch", "hint.hold", "hint.first_ripe", "hint.first_frost", "hint.first_winter", "hint.first_crow", "hint.first_can_retire", "hint.first_heritage", "gen.title", "gen.seeds", "away.duration", "away.apprentices", "away.tractor", "away.capped" })
                Assert.IsTrue(table.ContainsKey(key), key);
        }
    }
}
