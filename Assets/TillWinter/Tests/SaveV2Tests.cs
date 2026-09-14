using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>Save schema v2 (Session 3): migration from a hand-written v1 JSON fixture, v2 round-trip of every new field.</summary>
    public class SaveV2Tests
    {
        private const float Dt = 0.01f;

        private static void Run(FarmSim sim, float seconds, RingInput? ring, float dt = Dt)
        {
            int ticks = (int)Math.Round(seconds / dt);
            for (int i = 0; i < ticks; i++) sim.Tick(dt, ring);
        }

        // A genuine v1 file: mid-year, generation 2, tractor 1 owned (v1 had no tractor state), no cloud/golden fields.
        private const string V1Fixture = @"{
  ""SchemaVersion"": 1, ""SavedAtUnixSeconds"": 1789000000,
  ""Phase"": 0, ""Year"": 3, ""Season"": 1, ""YearTime"": 41.5, ""FrostWarning"": false, ""Coins"": 123.5,
  ""CrowSpawnTimer"": 1.25, ""RngState"": 305419896,
  ""Generation"": 2, ""LifetimeCoinsThisGeneration"": 900, ""LifetimeCoinsTotal"": 6900, ""YearsThisGeneration"": 2,
  ""SeedsBanked"": 4, ""SeedsEarnedTotal"": 10, ""CrowsScared"": 7, ""Harvests"": 310,
  ""GridSize"": 3,
  ""Plots"": [
    {""X"":0,""Y"":0,""Tier"":1,""State"":2,""Progress"":0.0,""HasCrow"":true,""CrowTimer"":1.5},
    {""X"":1,""Y"":0,""Tier"":1,""State"":1,""Progress"":0.6,""HasCrow"":false,""CrowTimer"":0},
    {""X"":2,""Y"":0,""Tier"":0,""State"":0,""Progress"":0.2,""HasCrow"":false,""CrowTimer"":0},
    {""X"":0,""Y"":1,""Tier"":0,""State"":1,""Progress"":0.9,""HasCrow"":false,""CrowTimer"":0},
    {""X"":1,""Y"":1,""Tier"":0,""State"":2,""Progress"":0.0,""HasCrow"":false,""CrowTimer"":0},
    {""X"":2,""Y"":1,""Tier"":0,""State"":0,""Progress"":0.0,""HasCrow"":false,""CrowTimer"":0},
    {""X"":0,""Y"":2,""Tier"":0,""State"":0,""Progress"":0.0,""HasCrow"":false,""CrowTimer"":0},
    {""X"":1,""Y"":2,""Tier"":0,""State"":1,""Progress"":0.1,""HasCrow"":false,""CrowTimer"":0},
    {""X"":2,""Y"":2,""Tier"":0,""State"":0,""Progress"":0.0,""HasCrow"":false,""CrowTimer"":0}
  ],
  ""Apprentices"": [ {""X"":1.0,""Y"":-1.2} ],
  ""AlmanacLevels"": [ {""Id"":""irrigation"",""Level"":2}, {""Id"":""sun"",""Level"":1}, {""Id"":""tractor"",""Level"":1}, {""Id"":""unlock_tomato"",""Level"":1}, {""Id"":""upgrade_plot"",""Level"":2}, {""Id"":""apprentice_count"",""Level"":1} ],
  ""HeritageLevels"": [ {""Id"":""h_start_radius"",""Level"":1} ]
}";

        [Test]
        public void V1Fixture_MigratesToV2_LoadsAndPlaysDeterministically()
        {
            var data = MiniJson.To<SaveData>(V1Fixture);
            Assert.AreEqual(1, data.SchemaVersion);
            Assert.AreEqual(9, data.Plots.Length);
            var migrated = SaveMigrations.Migrate(MiniJson.To<SaveData>(V1Fixture), new FarmConfig());
            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.IsFalse(migrated.CloudActive);
            Assert.AreEqual(float.MaxValue, migrated.CloudSpawnTime);
            Assert.AreEqual(30f, migrated.TractorTimeToNextSweep, "tractor level 1 starts a full interval away");
            Assert.AreEqual(0, migrated.Combo);
            Assert.AreEqual(0f, migrated.GreenhouseSecondsLeft, "mid-year: no winter accrual");
            foreach (var p in migrated.Plots) Assert.IsFalse(p.Golden);

            var sim = FarmSim.FromSave(data, new FarmConfig());
            Assert.IsNotNull(sim);
            var s = sim.State;
            Assert.AreEqual(Phase.Year, s.Phase);
            Assert.AreEqual(3, s.Year);
            Assert.AreEqual(2, s.Generation.Generation);
            Assert.AreEqual(123.5, s.Coins);
            Assert.AreEqual(1, s.GetPlot(0, 0).Tier);
            Assert.IsTrue(s.GetPlot(0, 0).HasCrow);
            Assert.AreEqual(1.5f, s.Crows[0].Timer);
            Assert.AreEqual(1, s.Apprentices.Count);
            Assert.IsTrue(s.Tractor.Owned);
            Assert.AreEqual(30f, s.Tractor.TimeToNextSweep);
            Assert.That(s.RingRadius, Is.EqualTo(0.95f).Within(1e-5f), "heritage radius from the fixture");

            // Deterministic: two loads of the same fixture play out identically.
            var a = FarmSim.FromSave(MiniJson.To<SaveData>(V1Fixture), new FarmConfig());
            var b = FarmSim.FromSave(MiniJson.To<SaveData>(V1Fixture), new FarmConfig());
            for (int i = 0; i < 6000; i++)
            {
                var ring = i % 500 < 300 ? new RingInput(1, 1) : (RingInput?)null;
                a.Tick(Dt, ring);
                b.Tick(Dt, ring);
            }
            Assert.AreEqual(a.State.Coins, b.State.Coins);
            Assert.That(a.State.Coins, Is.GreaterThan(123.5));
            Assert.AreEqual(a.State.Generation.Harvests, b.State.Generation.Harvests);
        }

        [Test]
        public void UnknownVersion_StillNull()
        {
            Assert.IsNull(SaveMigrations.Migrate(new SaveData { SchemaVersion = 3 }));
            Assert.IsNull(SaveMigrations.Migrate(new SaveData { SchemaVersion = 0 }));
            var data = MiniJson.To<SaveData>(V1Fixture);
            data.SchemaVersion = 99;
            Assert.IsNull(FarmSim.FromSave(data, new FarmConfig()));
        }

        [Test]
        public void V2RoundTrip_CoversEveryNewField()
        {
            var sim = new FarmSim(new FarmConfig(), 5);
            sim.DebugSetLevel("tractor", 2);
            sim.DebugSetLevel("ring_combo", 1);
            sim.DebugSetLevel("h_unlock_rain_cloud", 1);
            sim.DebugSetLevel("h_golden_crop", 5);
            sim.DebugSkipToWinter();
            sim.DebugSetLevel("greenhouse", 1);
            Run(sim, 7f, null); // greenhouse accrues 7 s
            sim.StartNextYear();
            sim.DebugForceRipeAll();
            sim.DebugNextHarvestGolden();
            sim.DebugSetRingRadiusOverride(0.5f);
            Run(sim, 0.55f, new RingInput(1, 1)); // combo 1, golden replant at (1,1)
            Assert.IsTrue(sim.State.GetPlot(1, 1).IsGolden);
            sim.DebugSpawnCloud();
            Run(sim, 0.3f, null);                 // cloud mid-drift
            Assert.IsTrue(sim.DebugForceTractorSweep());
            Run(sim, 0.2f, null);                 // tractor mid-sweep
            var s = sim.State;
            Assert.IsTrue(s.Cloud.Active);
            Assert.IsTrue(s.Tractor.Sweeping);
            Assert.That(s.Combo, Is.GreaterThanOrEqualTo(1));

            var data = sim.ToSave();
            Assert.AreEqual(2, data.SchemaVersion);
            var loaded = FarmSim.FromSave(data, new FarmConfig());
            Assert.IsNotNull(loaded);
            var l = loaded.State;
            Assert.AreEqual(s.Cloud.Active, l.Cloud.Active);
            Assert.AreEqual(s.Cloud.X, l.Cloud.X);
            Assert.AreEqual(s.Cloud.TimeLeft, l.Cloud.TimeLeft);
            Assert.AreEqual(s.Cloud.SpawnedThisYear, l.Cloud.SpawnedThisYear);
            Assert.AreEqual(s.Cloud.SpawnTime, l.Cloud.SpawnTime);
            Assert.AreEqual(s.Tractor.Owned, l.Tractor.Owned);
            Assert.AreEqual(s.Tractor.Row, l.Tractor.Row);
            Assert.AreEqual(s.Tractor.X, l.Tractor.X);
            Assert.AreEqual(s.Tractor.Sweeping, l.Tractor.Sweeping);
            Assert.AreEqual(s.Tractor.TimeToNextSweep, l.Tractor.TimeToNextSweep);
            Assert.AreEqual(s.Combo, l.Combo);
            Assert.AreEqual(s.Greenhouse.SecondsLeftThisWinter, l.Greenhouse.SecondsLeftThisWinter);
            Assert.AreEqual(s.Greenhouse.CoinsThisWinter, l.Greenhouse.CoinsThisWinter);
            Assert.AreEqual(s.Greenhouse.CoinsPerSecond, l.Greenhouse.CoinsPerSecond);
            for (int i = 0; i < s.Plots.Count; i++) Assert.AreEqual(s.Plots[i].IsGolden, l.Plots[i].IsGolden, "golden " + i);

            // And the two keep agreeing after more play (cloud drifts off, sweep finishes, golden harvested).
            for (int i = 0; i < 1500; i++)
            {
                var ring = i < 600 ? new RingInput(1, 1) : (RingInput?)null;
                sim.Tick(Dt, ring);
                loaded.Tick(Dt, ring);
            }
            Assert.AreEqual(s.Coins, l.Coins);
            Assert.AreEqual(s.Generation.Harvests, l.Generation.Harvests);
            Assert.AreEqual(s.Cloud.Active, l.Cloud.Active);
            Assert.AreEqual(s.Tractor.Sweeping, l.Tractor.Sweeping);
        }

        [Test]
        public void SaveData_HasEveryPersistentFieldInJson()
        {
            // Guard: a v2 save serialised to JSON by the mini writer contains every public field name.
            var data = new FarmSim(new FarmConfig(), 1).ToSave();
            string json = MiniJson.From(data);
            foreach (var f in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                StringAssert.Contains("\"" + f.Name + "\"", json);
            var back = MiniJson.To<SaveData>(json);
            Assert.AreEqual(data.Plots.Length, back.Plots.Length);
            Assert.AreEqual(data.RngState, back.RngState);
        }
    }

    /// <summary>
    /// Minimal JSON reader/writer for tests (Core has no JSON dependency; Unity uses JsonUtility at runtime).
    /// Supports objects, arrays, numbers, strings, bools, null; maps onto public fields by name.
    /// </summary>
    public static class MiniJson
    {
        public static T To<T>(string json) where T : new() => (T)Map(Parse(json), typeof(T));

        public static string From(object obj)
        {
            var sb = new StringBuilder();
            Write(obj, sb);
            return sb.ToString();
        }

        // ---- writer
        private static void Write(object v, StringBuilder sb)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case int or long or uint or short or byte: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
                case Array arr:
                    sb.Append('[');
                    for (int i = 0; i < arr.Length; i++) { if (i > 0) sb.Append(','); Write(arr.GetValue(i), sb); }
                    sb.Append(']');
                    break;
                default:
                    sb.Append('{');
                    bool first = true;
                    foreach (var f in v.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('"').Append(f.Name).Append("\":");
                        Write(f.GetValue(v), sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        // ---- mapper
        private static object Map(object node, Type t)
        {
            if (node == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return node.ToString();
            if (t == typeof(bool)) return Convert.ToBoolean(node, CultureInfo.InvariantCulture);
            if (t == typeof(int)) return Convert.ToInt32(node, CultureInfo.InvariantCulture);
            if (t == typeof(long)) return Convert.ToInt64(node, CultureInfo.InvariantCulture);
            if (t == typeof(uint)) return Convert.ToUInt32(node, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return Convert.ToSingle(node, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return Convert.ToDouble(node, CultureInfo.InvariantCulture);
            if (t.IsArray)
            {
                var list = (List<object>)node;
                var arr = Array.CreateInstance(t.GetElementType(), list.Count);
                for (int i = 0; i < list.Count; i++) arr.SetValue(Map(list[i], t.GetElementType()), i);
                return arr;
            }
            var obj = Activator.CreateInstance(t);
            var dict = (Dictionary<string, object>)node;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (dict.TryGetValue(f.Name, out var v)) f.SetValue(obj, Map(v, f.FieldType));
            return obj;
        }

        // ---- parser
        private static object Parse(string s)
        {
            int i = 0;
            var v = ParseValue(s, ref i);
            return v;
        }

        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object ParseValue(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string key = ParseString(s, ref i);
                    Ws(s, ref i);
                    if (s[i] != ':') throw new FormatException("expected : at " + i);
                    i++;
                    d[key] = ParseValue(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("expected , or } at " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("expected , or ] at " + i);
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (s.Length - i >= 4 && s.Substring(i, 4) == "true") { i += 4; return true; }
            if (s.Length - i >= 5 && s.Substring(i, 5) == "false") { i += 5; return false; }
            if (s.Length - i >= 4 && s.Substring(i, 4) == "null") { i += 4; return null; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
