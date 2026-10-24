using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Test-only probe: at every field size (3x3 up to the cap) and after every screen change that has broken taps
    /// before (mid-year expansion, winter → Next year, apprentices, seed bag, magnifier, pause, retire, seasons,
    /// weather), it taps every plot through the real input path at its centre and near its four visible corners and
    /// checks that (a) no UI sits under the finger, (b) the screen point maps back to that plot, (c) the strike lands
    /// on that plot. It also checks the plot views: one per cell, each standing where the grid says.
    /// Report only, it changes no game code. Run: Unity -executeMethod TillWinter.EditorTools.FieldTapProbe.Run
    /// -probeOut &lt;dir&gt; [-probePreset "1080x1920 (Portrait)"].
    /// </summary>
    [InitializeOnLoad]
    public static class FieldTapProbe
    {
        private const string ActiveKey = "TillWinter.FieldTapProbe.Active";
        private const string StepKey = "TillWinter.FieldTapProbe.Step";
        private const string OutKey = "TillWinter.FieldTapProbe.Out";
        private const string PresetKey = "TillWinter.FieldTapProbe.Preset";
        private const string ExitKey = "TillWinter.FieldTapProbe.Exit";
        private const string DeviceKey = "TillWinter.FieldTapProbe.Device";
        private const string BackupSuffix = ".tapprobe-bak";

        private static readonly StringBuilder Report = new StringBuilder();
        private static IEnumerator _script;
        private static GameController _game;
        private static double _t0;
        private static int _errors, _checks, _scenarioErrors;
        private static bool _finished;

        static FieldTapProbe()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
            if (SessionState.GetInt(ExitKey, -1) >= 0) EditorApplication.update += WaitThenExit;
        }

        public static void Run()
        {
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults", "tapprobe");
            string preset = "1080x2340 (Portrait)";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-probeOut") output = args[i + 1];
                if (args[i] == "-probePreset") preset = args[i + 1];
            }
            Directory.CreateDirectory(output);
            foreach (var f in Directory.GetFiles(output)) if (f.EndsWith(".png") || f.EndsWith("report.txt")) File.Delete(f);
            SessionState.SetString(OutKey, output);
            SessionState.SetString(PresetKey, preset);
            var savePath = Path.Combine(Application.persistentDataPath, SaveController.FileName);
            if (File.Exists(savePath)) File.Copy(savePath, savePath + BackupSuffix, true);
            if (File.Exists(savePath + ".bak")) File.Copy(savePath + ".bak", savePath + ".bak" + BackupSuffix, true);
            SaveController.DeleteFiles(savePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ExitKey, -1);
            SessionState.SetInt(StepKey, 0);
            SessionState.SetInt(DeviceKey, -1);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_finished) return;
            try
            {
                int step = SessionState.GetInt(StepKey, 0);
                if (step == 0)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    EditorSceneManager.OpenScene("Assets/TillWinter/Scenes/Farm.unity");
                    var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                    if (gameViewType != null) EditorWindow.GetWindow(gameViewType);
                    // Play mode runs in the Device Simulator, which ignores Game view sizes: pick its closest device.
                    string size = SessionState.GetString(PresetKey, "1080x2340 (Portrait)");
                    if (GameViewPresets.TryGetSize(size, out int w, out int h))
                    {
                        SessionState.SetInt(DeviceKey, SimulatorDevices.CurrentIndex());
                        string err = SimulatorDevices.SelectClosest(w, h, out string device);
                        Log("Screen " + size + ": simulator device " + device + (err.Length > 0 ? " (" + err + ")" : ""));
                    }
                    GameViewPresets.Select(size);
                    SessionState.SetInt(StepKey, 1);
                    EditorApplication.EnterPlaymode();
                    return;
                }
                if (!EditorApplication.isPlaying) return;
                // As the UI tour does: without it the player loop stops whenever the editor window loses focus.
                if (!Application.runInBackground) Application.runInBackground = true;
                if (_script == null)
                {
                    _game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                    if (_game == null || _game.Sim == null) return;
                    _t0 = EditorApplication.timeSinceStartup;
                    Application.logMessageReceived += OnLog;
                    _script = Script();
                }
                if (EditorApplication.timeSinceStartup - _t0 > 420) { Fail("Timed out"); return; }
                if (!_script.MoveNext()) Finish();
            }
            catch (Exception e) { Fail("Exception in probe: " + e); }
        }

        // ------------------------------------------------------------------ the script

        private static IEnumerator Script()
        {
            var sim = _game.Sim;
            var s = _game.State;
            Log("Screen " + Screen.width + "x" + Screen.height + ", preset " + SessionState.GetString(PresetKey, ""));
            foreach (var w in Wait(1.5)) yield return w;

            // 1. A clean start: retire into a new generation without the Heritage start field, so it opens on 3x3.
            foreach (var w in RetireToNewGeneration(smallField: true)) yield return w;
            foreach (var w in Scenario("new generation, 3x3, year 1")) yield return w;

            // 2. Grow it the way a player does: a winter purchase, then Next year through the button (the path where a
            // 3x3 once stood ghosted behind the 4x4).
            for (int round = 0; round < 4 && s.GridSize < sim.Config.MaxGridSize; round++)
            {
                int before = s.GridSize;
                sim.DebugAddCoins(5e6);
                sim.DebugSkipToWinter();
                foreach (var w in Wait(1.5)) yield return w;
                bool bought = sim.TryBuy("expand_field");
                Log("winter: expand_field bought=" + bought + " (grid " + before + ", target " + s.Stats.TargetGridSize + ")");
                if (!Click("NextYear")) Error("no NextYear button in winter");
                foreach (var w in Wait(2.5)) yield return w;
                Check(s.Phase == Phase.Year, "phase is Year after Next year (" + s.Phase + ")");
                foreach (var w in Scenario("after winter + Next year, " + before + "x" + before + " -> " + s.GridSize + "x" + s.GridSize)) yield return w;
            }

            // 3. Expansion inside a year (the developer panel's path) from a fresh 3x3: 3 -> 4 -> 5 -> 6 without a winter.
            foreach (var w in RetireToNewGeneration(smallField: true)) yield return w;
            while (s.GridSize < sim.Config.MaxGridSize)
            {
                int before = s.GridSize;
                sim.DebugSetLevel("expand_field", s.GetLevel("expand_field") + 1);
                foreach (var w in Wait(1.5)) yield return w;
                if (s.GridSize == before) { Error("mid-year expand_field did not grow the field (" + before + ")"); break; }
                foreach (var w in Scenario("mid-year expansion to " + s.GridSize + "x" + s.GridSize)) yield return w;
            }

            // 4. The things that sit over the field or take the finger, each opened and closed again.
            sim.DebugAddCoins(5e6);
            sim.DebugSkipToWinter();
            foreach (var w in Wait(1.5)) yield return w;
            foreach (var id in new[] { "apprentice_count", "apprentice_count", "apprentice_count", "barn", "scarecrow", "tractor", "unlock_tomato", "farm_dog" })
                Log("winter buy " + id + ": " + sim.TryBuy(id));
            Click("NextYear");
            foreach (var w in Wait(3.0)) yield return w;
            Log("apprentices " + s.Apprentices.Count + ", tractor " + s.Tractor.Owned + ", scarecrows " + s.Stats.ScarecrowCount + ", barn " + s.Stats.BarnCapacity);
            foreach (var w in Scenario("with apprentices, dog, tractor, barn", keepTime: true)) yield return w;

            Click("SeedBag"); foreach (var w in Wait(0.5)) yield return w;
            Click("SeedBag"); foreach (var w in Wait(0.5)) yield return w;
            foreach (var w in Scenario("after the seed bag opened and closed")) yield return w;

            Click("Inspect"); foreach (var w in Wait(0.5)) yield return w;
            Click("Inspect"); foreach (var w in Wait(0.5)) yield return w;
            foreach (var w in Scenario("after the magnifier toggled on and off")) yield return w;

            Click("PauseButton"); foreach (var w in Wait(0.6)) yield return w;
            Click("pause.resume"); foreach (var w in Wait(0.6)) yield return w;
            foreach (var w in Scenario("after pause and resume")) yield return w;

            sim.DebugBringTrader(); foreach (var w in Wait(0.8)) yield return w;
            foreach (var w in Scenario("with the travelling trader card up")) yield return w;

            foreach (var season in new[] { Season.Summer, Season.Autumn })
            {
                sim.DebugSetSeason(season); foreach (var w in Wait(1.2)) yield return w;
                foreach (var w in Scenario("season " + season)) yield return w;
            }
            foreach (Weather weather in Enum.GetValues(typeof(Weather)))
            {
                if (weather == Weather.Clear) continue;
                sim.DebugSetSeason(Season.Summer); // a fresh stretch of year, so the pass ends before the frost
                sim.DebugStartWeather(weather); foreach (var w in Wait(1.2)) yield return w;
                foreach (var w in Scenario("weather " + weather)) yield return w;
            }

            // 5. A second retire with the big field: the new generation starts over on the small one.
            foreach (var w in RetireToNewGeneration()) yield return w;
            foreach (var w in Scenario("second new generation")) yield return w;
        }

        private static IEnumerable RetireToNewGeneration(bool smallField = false)
        {
            var sim = _game.Sim;
            var s = _game.State;
            sim.DebugAddCoins(5e6);
            sim.DebugSkipToWinter();
            foreach (var w in Wait(1.5)) yield return w;
            sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold * 4);
            if (!Click("Retire")) Error("no Retire link in winter");
            foreach (var w in Wait(1.0)) yield return w;
            if (!Click("Yes")) Error("no Yes on the retire confirm");
            foreach (var w in Wait(2.0)) yield return w;
            if (!Click("Continue")) Error("no Continue on the album page");
            foreach (var w in Wait(1.5)) yield return w;
            if (smallField) foreach (var id in new[] { "h_start_field", "h_bigger_field" }) sim.DebugSetLevel(id, 0);
            if (!Click("StartGeneration")) Error("no Start generation button");
            foreach (var w in Wait(2.5)) yield return w;
            Check(s.Phase == Phase.Year, "a new generation starts in a Year (" + s.Phase + ", gen " + s.Generation.Generation + ", grid " + s.GridSize + ")");
        }

        /// <summary>
        /// One full pass over the field. Time stands still for it (the year, crows and helpers wait), unless
        /// <paramref name="keepTime"/> keeps the helpers walking under the taps.
        /// </summary>
        private static IEnumerable Scenario(string name, bool keepTime = true)
        {
            var sim = _game.Sim;
            var s = _game.State;
            _scenarioErrors = 0;
            Log("== " + name + ": phase " + s.Phase + ", gen " + s.Generation.Generation + ", year " + s.Year + ", season " + s.Season
                + ", grid " + s.GridSize + "x" + s.GridSize + ", inputBlocked " + _game.InputBlocked + ", paused " + _game.Paused
                + ", plotOverride " + (_game.PlotTapOverride != null) + ", fieldOverride " + (_game.FieldTapOverride != null));
            if (s.Phase != Phase.Year) { Error(name + ": not in a Year, skipped"); yield break; }
            CheckViews(name);
            Shot(Slug(name));

            float oldScale = _game.TimeScale;
            if (!keepTime) _game.TimeScale = 0f;
            var points = new List<(GridPos cell, Vector2 screen, string where)>();
            foreach (var p in s.Plots)
            {
                var corners = VisibleCorners(p.Pos);
                points.Add((p.Pos, ScreenOfTop(p.Pos, 0f, 0f), "centre"));
                foreach (var c in corners) points.Add((p.Pos, c.screen, c.where));
            }
            int blocked = 0, mismatched = 0, missed = 0, taps = 0, notTaken = 0;
            foreach (var (cell, screen, where) in points)
            {
                if (s.Phase != Phase.Year) { Log("  the year ended mid-pass (" + s.Phase + ")"); break; }
                // (a) UI over the point
                string ui = UiAt(screen);
                if (ui != null) { blocked++; if (blocked <= 10) Error(name + ": UI over plot " + cell + " " + where + " at " + Fmt(screen) + " -> " + ui); continue; }
                // (b) the point maps back to this plot
                if (!_game.TryScreenToPlot(screen, out var back)) { mismatched++; Error(name + ": " + cell + " " + where + " maps to no ground"); continue; }
                var mapped = new GridPos(Mathf.RoundToInt(back.x), Mathf.RoundToInt(back.y));
                if (mapped != cell) { mismatched++; if (mismatched <= 10) Error(name + ": plot " + cell + " " + where + " at " + Fmt(screen) + " maps to " + mapped + " (" + back.x.ToString("0.00") + ", " + back.y.ToString("0.00") + ")"); continue; }
                // (c) a real tap strikes this plot
                var plot = s.GetPlot(cell);
                if (!plot.IsHard) sim.DebugSetLayer(cell, Math.Max(0, plot.Layer));
                if (!plot.IsHard) { Log("  " + cell + " could not be made hard (" + plot.State + "), skipped"); continue; }
                sim.DebugSetStamina(s.Stats.StaminaMax);
                sim.DebugClearCooldown();
                int strikes = s.Generation.Strikes;
                double hp = plot.Hp;
                bool crow = plot.HasCrow, pest = s.Pest.Kind != PestKind.None && s.Pest.Pos == cell;
                _game.DebugTapScreen = screen;
                double give = EditorApplication.timeSinceStartup + 1.0;
                while (_game.DebugTapScreen.HasValue && EditorApplication.timeSinceStartup < give) yield return null;
                int frame = Time.frameCount;
                while (Time.frameCount == frame && EditorApplication.timeSinceStartup < give) yield return null;
                if (_game.DebugTapScreen.HasValue)
                {
                    _game.DebugTapScreen = null;
                    notTaken++;
                    if (notTaken <= 3) Error(name + ": the game did not take the tap on " + cell + " within a second (inputBlocked " + _game.InputBlocked + ", paused " + _game.Paused
                        + ", phase " + s.Phase + ", timeScale " + Time.timeScale + ", controller enabled " + _game.isActiveAndEnabled + ", sheets " + OpenSheets() + ")");
                    continue;
                }
                taps++;
                bool landed = s.Generation.Strikes > strikes && (plot.Hp < hp - 1e-6 || !plot.IsHard);
                if (!landed && !crow && !pest)
                {
                    missed++;
                    if (missed <= 10)
                    {
                        Error(name + ": tap on " + cell + " " + where + " at " + Fmt(screen) + " did not strike it (strikes " + strikes + "->" + s.Generation.Strikes
                            + ", hp " + hp.ToString("0.0") + "->" + plot.Hp.ToString("0.0") + ", state " + plot.State + ", inputBlocked " + _game.InputBlocked
                            + ", apprenticeHit " + (_game.ApprenticeHitTest != null ? _game.ApprenticeHitTest(screen) : -9) + ")");
                    }
                }
            }
            _game.TimeScale = oldScale;
            Log("   " + points.Count + " points, " + taps + " taps: " + blocked + " under UI, " + mismatched + " mapped to another plot, " + missed + " taps missed"
                + ", " + notTaken + " taps never taken" + (_scenarioErrors == 0 ? "  -> OK" : "  -> " + _scenarioErrors + " problems"));
            _checks++;
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>The screen point of a plot's top face, offset by (dx, dz) in cells.</summary>
        private static Vector2 ScreenOfTop(GridPos p, float dx, float dz)
        {
            float top = TopOf(p);
            var w = _game.PlotToWorld(p.X + dx, p.Y + dz, top);
            var sp = _game.Cam.WorldToScreenPoint(w);
            return new Vector2(sp.x, sp.y);
        }

        /// <summary>Where the player sees the plot's corners: 40% of the visible tile out from its centre, on its top face.</summary>
        private static List<(Vector2 screen, string where)> VisibleCorners(GridPos p)
        {
            var list = new List<(Vector2, string)>();
            float half = HalfWidthOf(p) * 0.8f;
            list.Add((ScreenOfTop(p, -half, -half), "near-left corner"));
            list.Add((ScreenOfTop(p, half, -half), "near-right corner"));
            list.Add((ScreenOfTop(p, -half, half), "far-left corner"));
            list.Add((ScreenOfTop(p, half, half), "far-right corner"));
            return list;
        }

        /// <summary>The bed's top face: the plot's own soil block (the renderer on the view's root), not the crop on it.</summary>
        private static float TopOf(GridPos p)
        {
            var r = FindView(p)?.GetComponent<Renderer>();
            return r != null ? r.bounds.max.y : 0.1f;
        }

        private static float HalfWidthOf(GridPos p)
        {
            var r = FindView(p)?.GetComponent<Renderer>();
            return r != null ? Mathf.Min(r.bounds.extents.x, r.bounds.extents.z) : 0.45f;
        }

        private static PlotView FindView(GridPos p)
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<PlotView>(FindObjectsSortMode.None))
                if (v.name == "Plot " + p && v.gameObject.activeInHierarchy) return v;
            return null;
        }

        /// <summary>One active view per cell, none extra, each standing on its own cell; no view at a position another owns.</summary>
        private static void CheckViews(string name)
        {
            var s = _game.State;
            var views = UnityEngine.Object.FindObjectsByType<PlotView>(FindObjectsSortMode.None);
            int active = 0;
            var seen = new HashSet<string>();
            foreach (var v in views)
            {
                if (!v.gameObject.activeInHierarchy) continue;
                active++;
                if (!seen.Add(v.name)) Error(name + ": two active views named " + v.name);
            }
            if (active != s.GridSize * s.GridSize) Error(name + ": " + active + " active plot views for a " + s.GridSize + "x" + s.GridSize + " field");
            foreach (var p in s.Plots)
            {
                var v = FindView(p.Pos);
                if (v == null) { Error(name + ": no view for plot " + p.Pos); continue; }
                var want = _game.PlotToWorld(p.Pos);
                var at = v.transform.position;
                var flat = new Vector2(at.x - want.x, at.z - want.z);
                if (flat.magnitude > 0.12f) Error(name + ": view of " + p.Pos + " stands at (" + at.x.ToString("0.00") + ", " + at.z.ToString("0.00") + "), grid says (" + want.x.ToString("0.00") + ", " + want.z.ToString("0.00") + ")");
            }
            // Anything plot-shaped outside the grid (a ghost field behind the real one).
            foreach (var v in views)
            {
                if (!v.gameObject.activeInHierarchy) continue;
                var local = _game.WorldToPlot(v.transform.position);
                if (local.x < -0.6f || local.y < -0.6f || local.x > s.GridSize - 0.4f || local.y > s.GridSize - 0.4f)
                    Error(name + ": view " + v.name + " stands outside the field at grid (" + local.x.ToString("0.00") + ", " + local.y.ToString("0.00") + ")");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string UiAt(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return null;
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            if (hits.Count == 0) return null;
            return NodePath(hits[0].gameObject.transform);
        }

        private static string OpenSheets()
        {
            var sb = new StringBuilder();
            foreach (var n in new[] { "PauseSheet", "SettingsSheet", "StatsSheet", "HelpSheet", "AlbumSheet", "EndYearConfirm", "AwayCard", "WinterScreen", "GenerationCard", "Caption" })
            {
                var go = GameObject.Find(n);
                if (go != null && go.activeInHierarchy) sb.Append(n).Append(' ');
            }
            return sb.Length == 0 ? "none" : sb.ToString().Trim();
        }

        private static string NodePath(Transform t) => t.parent == null ? t.name : NodePath(t.parent) + "/" + t.name;
        private static string Fmt(Vector2 v) => "(" + v.x.ToString("0") + ", " + v.y.ToString("0") + ")";
        private static string Slug(string s) { var sb = new StringBuilder(); foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'); return sb.ToString(); }

        private static IEnumerable Wait(double seconds)
        {
            double until = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < until) yield return null;
        }

        private static bool Click(string name)
        {
            var go = GameObject.Find(name);
            var btn = go != null ? go.GetComponent<Button>() : null;
            if (btn == null) { Log("  no active button '" + name + "'"); return false; }
            btn.onClick.Invoke();
            return true;
        }

        private static void Shot(string name) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(OutKey, "."), name + ".png"));

        private static void Check(bool ok, string what) { if (ok) Log("PASS " + what); else Error(what); }
        private static void Error(string what) { _errors++; _scenarioErrors++; Log("FAIL " + what); }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Error("CONSOLE " + type + ": " + condition + " | " + stackTrace.Split('\n')[0]);
        }

        private static void Log(string msg)
        {
            Report.AppendLine("[" + (EditorApplication.timeSinceStartup - _t0).ToString("0.0") + "s] " + msg);
            Debug.Log("[TapProbe] " + msg);
        }

        private static void Fail(string why) { Error(why); Finish(); }

        private static void Finish()
        {
            if (_finished) return;
            _finished = true;
            Log("Done. scenarios=" + _checks + " problems=" + _errors + " result=" + (_errors == 0 ? "PASS" : "FAIL"));
            var dir = SessionState.GetString(OutKey, ".");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "report.txt"), Report.ToString());
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.isPlaying = false;
            SessionState.SetInt(ExitKey, _errors == 0 ? 0 : 1);
            EditorApplication.update += WaitThenExit;
        }

        private static void WaitThenExit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitThenExit;
            var savePath = Path.Combine(Application.persistentDataPath, SaveController.FileName);
            if (File.Exists(savePath + BackupSuffix))
            {
                SaveController.DeleteFiles(savePath);
                File.Move(savePath + BackupSuffix, savePath);
                if (File.Exists(savePath + ".bak" + BackupSuffix)) File.Move(savePath + ".bak" + BackupSuffix, savePath + ".bak");
            }
            int device = SessionState.GetInt(DeviceKey, -1);
            if (device >= 0) SimulatorDevices.SetIndex(device);
            int code = SessionState.GetInt(ExitKey, 1);
            SessionState.SetInt(ExitKey, -1);
            EditorApplication.Exit(code);
        }
    }
}
