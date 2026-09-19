using System;
using System.IO;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Walks every screen and sheet in play mode and saves a screenshot of each, so layout can be checked without
    /// anyone driving the game. Every panel is opened through its own button's onClick, so the tour also proves the
    /// buttons are wired. Nothing is typed or clicked with a virtual pointer — that is what makes the smoke test
    /// unreliable while someone is using the machine.
    ///
    /// The tour ends a year early, grants lifetime coins and simulates an offline hour, so it backs up the save and
    /// settings first and restores them when it finishes, pass or fail. If a previous tour's backup is still on disk
    /// it refuses to start rather than overwrite the real save with a test one.
    ///
    /// Run with ui-tour.bat [output folder]. Writes NN-name.png and report.txt there.
    /// </summary>
    [InitializeOnLoad]
    public static class UiTour
    {
        private const string ActiveKey = "TillWinter.UiTour.Active";
        private const string StepKey = "TillWinter.UiTour.Step";
        private const string ActedAtKey = "TillWinter.UiTour.ActedAt";
        private const string ShotAtKey = "TillWinter.UiTour.ShotAt";
        private const string StartedKey = "TillWinter.UiTour.Started";
        private const string OutKey = "TillWinter.UiTour.Out";
        private const string SizeKey = "TillWinter.UiTour.Size";
        private const string CleanKey = "TillWinter.UiTour.Clean";
        /// <summary>The Simulator device the editor had before a sized tour, restored at the end.</summary>
        private const string SimulatorDeviceKey = "TillWinter.UiTour.SimulatorDevice";
        private const string ExitKey = "TillWinter.UiTour.Exit";
        private const string ErrorsKey = "TillWinter.UiTour.Errors";
        private const string BackupSuffix = ".uitour-bak";
        private const string AbsentSuffix = ".uitour-absent";
        private const float Settle = 0.3f;        // after a screenshot, before the next action
        private const double TimeoutSeconds = 300;
        private const float UntilTimeout = 20f;       // how long a step may wait for its condition

        private struct Step
        {
            public string Shot;
            public float Wait;
            public Action Act;
            public Func<bool> Until;
            public Step(string shot, float wait, Action act, Func<bool> until = null) { Shot = shot; Wait = wait; Act = act; Until = until; }
        }

        private static GameController Game => UnityEngine.Object.FindFirstObjectByType<GameController>();

        private static readonly Step[] Steps =
        {
            // Title scene. The studio mark plays once per process, and this is a fresh process.
            new Step("00-splash", 0.5f, null),
            new Step("01-menu", 3.2f, null),
            new Step("02-menu-settings", 0.7f, () => Click("menu.settings")),
            new Step(null, 0.4f, () => Click("settings.back", "SettingsSheet")),
            new Step("03-menu-credits", 0.7f, () => Click("menu.credits")),
            new Step("04-menu-after-credits", 0.5f, () => Click("settings.back", "CreditsSheet")), // must close to the menu
            new Step("05-menu-new-game", 0.6f, () => Click("menu.new_game", null, optional: true)),
            new Step(null, 0.3f, () => Click("Cancel", "NewGameConfirm", optional: true)),

            // Into the farm through the real loading screen.
            new Step("06-loading", 0.45f, () => SceneLoader.Load(SceneNames.Farm)),
            new Step(null, 1f, null, () => Game != null), // wait for the farm itself, not a fixed guess
            new Step(null, 1.6f, EnsureYear),
            new Step("07-hud", 1f, null),
            new Step(null, 0.2f, ProbeField),
            new Step("08-debug-panel", 0.4f, () => Click("DebugToggle")),
            new Step(null, 0.3f, () => Click("DebugToggle")),

            // Pause and its sheets, including the credits round trip through Settings.
            new Step("09-pause", 0.7f, () => Click("PauseButton")),
            new Step("10-settings", 0.7f, () => Click("pause.settings", "PauseSheet")),
            new Step("11-credits", 0.7f, () => Click("settings.credits", "SettingsSheet")),
            new Step("12-settings-again", 0.6f, () => Click("settings.back", "CreditsSheet")),
            new Step("13-pause-again", 0.6f, () => Click("settings.back", "SettingsSheet")),
            new Step("14-stats", 0.7f, () => Click("pause.stats", "PauseSheet")),
            new Step(null, 0.5f, () => Click("stats.continue", "StatsSheet")),
            new Step(null, 0.5f, () => Click("pause.resume", "PauseSheet")),

            // Winter, reached through the new End year button.
            new Step("14b-end-year-confirm", 0.7f, () => Click("EndYear")),
            new Step("15-winter", 1.8f, () => Click("EndYearYes", "EndYearConfirm")),
            new Step("16-node-sheet", 0.7f, () => Click("Ring", "Node ring_radius")),
            new Step("17-retire-ready", 0.8f, GrantRetire),
            new Step("18-retire-confirm", 0.6f, () => Click("Retire")),
            new Step(null, 0.4f, () => Click("No", "ConfirmDim")),
            new Step("19-heritage", 0.8f, () => Click("TreeToggle")),
            new Step(null, 0.5f, () => Click("TreeToggle")),

            // Back to a year, then the away card.
            new Step("20-next-year", 1.8f, () => Click("NextYear")),
            new Step(null, 0.6f, ProbeField), // the ring must still reach the field after Next Year
            new Step(null, 0.2f, GrantHelpers),
            new Step(null, 0.4f, () => Click("DebugToggle")),
            new Step(null, 0.3f, () => Click("Offline 1 h")),
            new Step("21-away-card", 0.9f, () => Click("DebugToggle")),
            new Step(null, 0.5f, () => Click("Ok", "AwayCard", optional: true)),
            new Step("22-hud-end", 0.6f, null),

            // A yearly goal on the HUD and weather over the field (GDD §3.3/§5.4 v1.9), then clear skies again.
            new Step("29-goal-storm", 2.4f, () => { var g = Game; if (g == null) return; g.Sim.DebugSetGoal(GoalType.HarvestCrop, 18, 1, 20); g.Sim.DebugStartWeather(Weather.Storm); }),
            new Step("30-fog", 2.6f, () => Game?.Sim.DebugStartWeather(Weather.Fog)),
            new Step("31-scarecrow-mode", 0.6f, () => Click("Scarecrow")),
            new Step(null, 0.2f, () => Click("Scarecrow")),

            // Events and threats (GDD §5.5–§5.7 v2.1): a locust swarm and a clover, a mole, the trader and a shooting star.
            new Step("32-locusts-clover", 1.2f, () => { var g = Game; if (g == null) return; g.Sim.DebugSpawnPest(PestKind.Locusts, new GridPos(1, 1)); g.Sim.DebugSpawnLucky(LuckyKind.Clover, new GridPos(0, 0)); }),
            new Step("33-mole-trader", 1.2f, () => { var g = Game; if (g == null) return; g.Sim.DebugSpawnPest(PestKind.Mole, new GridPos(2, 1)); g.Sim.DebugBringTrader(); }),
            new Step("34-star", 1.4f, () => Game?.Sim.DebugSpawnLucky(LuckyKind.ShootingStar, new GridPos(0, 0))),

            new Step(null, 1.5f, () => Game?.Sim.DebugStartWeather(Weather.Clear)),

            // The ring's shapes, cycled from the HUD button.
            new Step("25-ring-rake", 0.6f, () => Click("RingShape")),
            new Step("26-ring-cross", 0.6f, () => Click("RingShape")),

            // The seed bag, opened from the HUD, then a crop in hand.
            new Step("27-seed-bag", 0.6f, () => Click("SeedBag")),
            new Step("28-seed-picked", 0.6f, () => Click("Seed2")),
            new Step(null, 0.2f, () => Click("SeedBag")),

            // Weather and light the rest of the tour never meets: a rainbow after the rain cloud, then autumn at dusk.
            new Step("23-rainbow", 2.4f, () => { var g = Game; if (g != null && g.Sim.DebugSpawnCloud()) g.Sim.TapCloud(); }),
            new Step("24-autumn-dusk", 3.2f, () => Game?.Sim.DebugSetSeason(Season.Autumn)),

            // Winter with a stocked barn (GDD §3.5 v2.2): the market strip, the respec and the suggested node.
            new Step("35-winter-barn", 2.4f, () => Game?.Sim.DebugSkipToWinter()),

        };

        static UiTour()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>Entry point for -executeMethod. Output folder comes from -uiTourOut.</summary>
        public static void Run()
        {
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults", "ui-tour");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-uiTourOut") output = args[i + 1];
                if (args[i] == "-uiTourSize") SessionState.SetString(SizeKey, args[i + 1]);
            }
            // -uiTourClean: store screenshots, so the developer button never shows (it stays clickable for the tour).
            SessionState.SetBool(CleanKey, Array.IndexOf(args, "-uiTourClean") >= 0);
            Directory.CreateDirectory(output);
            foreach (var f in Directory.GetFiles(output))
                if (f.EndsWith(".png") || f.EndsWith("report.txt")) File.Delete(f);

            SessionState.SetString(OutKey, output);
            SessionState.SetInt(SimulatorDeviceKey, -1);
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetInt(ExitKey, -1);
            SessionState.SetInt(StepKey, -1);
            SessionState.SetBool(ActiveKey, true);
            try
            {
                BackUp();
            }
            catch (Exception e)
            {
                Log("FAIL could not back up the save: " + e.Message);
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        private static void Tick()
        {
            try
            {
                TickInner();
            }
            catch (Exception e)
            {
                Log("FAIL exception: " + e);
                Finish(1);
            }
        }

        private static void TickInner()
        {
            int exit = SessionState.GetInt(ExitKey, -1);
            if (exit >= 0)
            {
                // Finishing: leave play mode, then put the real save back, then quit.
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Restore();
                int device = SessionState.GetInt(SimulatorDeviceKey, -1);
                if (device >= 0)
                {
                    SimulatorDevices.SetIndex(device); // leave the Simulator on the device it had
                    Log("Simulator device restored to " + SimulatorDevices.CurrentName());
                }
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                Log("Done, exit " + exit);
                EditorApplication.Exit(exit);
                return;
            }

            int step = SessionState.GetInt(StepKey, -1);
            if (step < 0)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
                Log("Opening the title scene and entering play mode");
                EditorSceneManager.OpenScene("Assets/TillWinter/Scenes/Menu.unity");
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null) EditorWindow.GetWindow(gameViewType);
                // -uiTourSize picks another screen shape (short 16:9 phones, tablets). Play mode runs in the Device
                // Simulator here, which ignores Game view sizes, so the closest simulator device is chosen instead.
                string size = SessionState.GetString(SizeKey, "");
                if (size.Length == 0)
                {
                    GameViewPresets.Select("1080x2340 (Portrait)");
                    Log("Screen: simulator device " + SimulatorDevices.CurrentName());
                }
                else if (GameViewPresets.TryGetSize(size, out int w, out int h))
                {
                    if (SessionState.GetInt(SimulatorDeviceKey, -1) < 0) SessionState.SetInt(SimulatorDeviceKey, SimulatorDevices.CurrentIndex());
                    string err = SimulatorDevices.SelectClosest(w, h, out string device);
                    GameViewPresets.Select(size); // for a Game view, if that is what plays
                    Log(err.Length == 0 ? "Screen " + size + ": simulator device " + device : "Screen " + size + ": simulator not switched (" + err + "), Game view preset selected");
                }
                else Log("FAIL unknown size preset " + size);
                SessionState.SetInt(StepKey, 0);
                SessionState.SetFloat(ActedAtKey, -1f);
                SessionState.SetFloat(ShotAtKey, -1f);
                SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
                EditorApplication.EnterPlaymode();
                return;
            }

            if (!EditorApplication.isPlaying) return;
            // Without this the player loop stops whenever the editor window loses focus (the project's Run In
            // Background is off), so no frame renders, no capture is written and no scene loads. Play mode only;
            // the project setting is untouched.
            if (!Application.runInBackground) Application.runInBackground = true;
            float now = (float)EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(StartedKey, now) > TimeoutSeconds)
            {
                Log("FAIL timed out at step " + step);
                Finish(1);
                return;
            }
            if (step >= Steps.Length)
            {
                Log("Tour complete: " + Steps.Length + " steps");
                Finish(SessionState.GetInt(ErrorsKey, 0) > 0 ? 1 : 0);
                return;
            }

            var s = Steps[step];
            float actedAt = SessionState.GetFloat(ActedAtKey, -1f);
            if (actedAt < 0f)
            {
                s.Act?.Invoke();
                SessionState.SetFloat(ActedAtKey, now);
                return;
            }
            float shotAt = SessionState.GetFloat(ShotAtKey, -1f);
            if (shotAt < 0f)
            {
                if (s.Until != null && !s.Until())
                {
                    if (now - actedAt < UntilTimeout) return;
                    Error("TIMEOUT waiting at step " + step); // record it and carry on to the shot
                }
                else if (now - actedAt < s.Wait) return;
                if (s.Shot != null) Shot(s.Shot);
                SessionState.SetFloat(ShotAtKey, now);
                return;
            }
            // Let the capture land at the end of its frame before the next action changes the screen.
            if (now - shotAt < (s.Shot != null ? Settle : 0f)) return;
            SessionState.SetInt(StepKey, step + 1);
            SessionState.SetFloat(ActedAtKey, -1f);
            SessionState.SetFloat(ShotAtKey, -1f);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Invokes the first active button named <paramref name="name"/>, optionally under an ancestor named <paramref name="scope"/>.</summary>
        private static void Click(string name, string scope = null, bool optional = false)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (b.name != name || !b.isActiveAndEnabled) continue;
                if (scope != null && !HasAncestor(b.transform, scope)) continue;
                b.onClick.Invoke();
                return;
            }
            string what = "button '" + name + "'" + (scope != null ? " in '" + scope + "'" : "");
            if (optional) Log("SKIP no " + what);
            else Error("MISSING " + what);
        }

        private static bool HasAncestor(Transform t, string name)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        /// <summary>A save may open in Winter or Heritage; the tour starts from a running year.</summary>
        private static void EnsureYear()
        {
            var game = Game;
            if (game == null) { Error("MISSING GameController after loading the farm"); return; }
            if (game.State.Phase == Phase.Heritage) Click("StartGeneration");
            else if (game.State.Phase == Phase.Winter) Click("NextYear");
        }

        private static void GrantRetire()
        {
            var game = Game;
            if (game == null) return;
            game.Sim.DebugAddLifetimeCoins(game.Sim.Config.HeritageThreshold);
        }

        /// <summary>A new farm earns nothing offline, and the away card only opens when something was earned.</summary>
        private static void GrantHelpers()
        {
            var game = Game;
            if (game == null) return;
            game.Sim.DebugSetLevel("apprentice_count", 2);
            game.Sim.DebugSetLevel("irrigation", 3);
            game.Sim.DebugSetLevel("sun", 3);
            game.Sim.DebugSetLevel("ring_shape", 2); // the shape button only shows with its node
            game.Sim.DebugSetLevel("tap_harvest", 1);
            game.Sim.DebugSetLevel("unlock_tomato", 1); // the seed bag shows once a second crop is unlocked
            game.Sim.DebugSetLevel("unlock_corn", 1);
            game.Sim.DebugSetLevel("unlock_pumpkin", 1);
            // Special ground (GDD §2.4 v1.8), so the later shots show a stony and a fertile plot.
            game.Sim.DebugSetPlotKind(new GridPos(0, 2), PlotKind.Stony);
            game.Sim.DebugSetPlotKind(new GridPos(2, 0), PlotKind.Fertile);
            game.Sim.DebugSetLevel("scarecrow", 2); // two scarecrows on the field and the button to move them
            game.Sim.DebugSetLevel("tractor", 1);
            game.Sim.DebugSetLevel("farm_dog", 1);
            game.Sim.DebugSetLevel("beehive", 1);
            game.Sim.DebugSetLevel("barn", 1); // the store-share button, and a barn strip in the winter shots
            game.Sim.SetStoreShare(0.5f);
        }

        /// <summary>
        /// Logs any UI under a few points on the field. PointerInput drops presses that start over UI, so a stray
        /// raycast target there silently kills the ring; the report flags it as a failure.
        /// </summary>
        private static void ProbeField()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var game = Game;
            if (es == null || game == null) { Log("Probe: no event system or game"); return; }
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            int blocked = 0;
            for (int i = 0; i < 3; i++)
            {
                var screen = (Vector2)game.Cam.WorldToScreenPoint(game.PlotToWorld(i, i, 0.2f));
                var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };
                hits.Clear();
                es.RaycastAll(data, hits);
                foreach (var h in hits)
                {
                    blocked++;
                    Log("FAIL UI over the field at plot " + i + ": " + NodePath(h.gameObject.transform));
                    SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
                }
            }
            if (blocked == 0) Log("Probe: field clear of UI (phase " + game.State.Phase + ", input " + (game.InputBlocked ? "blocked" : "open") + ", paused " + game.Paused + ")");
        }

        private static string NodePath(Transform t) => t.parent == null ? t.name : NodePath(t.parent) + "/" + t.name;

        private static void Shot(string name)
        {
            var path = Path.Combine(SessionState.GetString(OutKey, "."), name + ".png");
            if (SessionState.GetBool(CleanKey, false))
            {
                var dev = GameObject.Find("DebugToggle");
                if (dev != null)
                {
                    var group = dev.GetComponent<CanvasGroup>();
                    if (group == null) group = dev.AddComponent<CanvasGroup>(); // not ??: Unity's missing component is a fake null
                    group.alpha = 0f;
                }
            }
            ScreenCapture.CaptureScreenshot(path);
            Log("Shot " + name + " (" + Screen.width + "x" + Screen.height + ")");
        }

        private static void Finish(int code)
        {
            if (SessionState.GetInt(ExitKey, -1) >= 0) return;
            SessionState.SetInt(ExitKey, code);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------ save safety

        private static string[] GuardedFiles => new[] { SaveController.FilePath, SaveController.FilePath + ".bak", SettingsStore.Path };

        private static void BackUp()
        {
            foreach (var f in GuardedFiles)
                if (File.Exists(f + BackupSuffix) || File.Exists(f + AbsentSuffix))
                    throw new IOException("a previous tour left " + f + BackupSuffix + " behind; restore it by hand first");
            foreach (var f in GuardedFiles)
            {
                if (File.Exists(f)) File.Copy(f, f + BackupSuffix);
                else File.WriteAllText(f + AbsentSuffix, "");
            }
            Log("Backed up the save and settings");
        }

        private static void Restore()
        {
            foreach (var f in GuardedFiles)
            {
                if (File.Exists(f + BackupSuffix))
                {
                    File.Copy(f + BackupSuffix, f, true);
                    File.Delete(f + BackupSuffix);
                }
                else if (File.Exists(f + AbsentSuffix))
                {
                    if (File.Exists(f)) File.Delete(f);
                    File.Delete(f + AbsentSuffix);
                }
            }
            foreach (var extra in new[] { SaveController.FilePath + ".tmp" })
                if (File.Exists(extra)) File.Delete(extra);
            Log("Restored the save and settings");
        }

        // ------------------------------------------------------------------ report

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // The editor's own Package Manager window reports when it cannot reach the registry; that is the
            // machine being offline, not the game failing.
            if (condition.StartsWith("[Package Manager")) { Log("SKIP editor: " + condition); return; }
            Error("CONSOLE " + type + ": " + condition);
        }

        private static void Error(string line)
        {
            SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
            Log(line);
        }

        private static void Log(string line)
        {
            var dir = SessionState.GetString(OutKey, "");
            if (string.IsNullOrEmpty(dir)) return;
            try { File.AppendAllText(Path.Combine(dir, "report.txt"), line + "\n"); }
            catch (IOException) { }
            Debug.Log("[UiTour] " + line);
        }
    }
}
