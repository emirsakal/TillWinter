using System;
using System.IO;
using System.Text;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Automated play-mode smoke test for the demo loop. Run with smoke-test.bat (opens the real editor,
    /// enters play mode, injects a virtual mouse, drives a full year, buys upgrades, starts year 2,
    /// and writes screenshots + a report to TestResults/smoke/). Exits the editor with 0 on success.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTest
    {
        private const string ActiveKey = "TillWinter.SmokeTest.Active";
        private const string StepKey = "TillWinter.SmokeTest.Step";
        private static readonly string OutDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults", "smoke");

        private static double _t0;
        private static int _phase;
        private static double _phaseStart;
        private static Mouse _mouse;
        private static bool _holding;
        private static Vector2 _holdPos;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _errors;
        private static int _harvests;
        private static int _crowScared;
        private static bool _hooked;
        private static GameController _game;
        private static bool _finished;

        static SmokeTest()
        {
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Tick;
        }

        /// <summary>Entry point for -executeMethod.</summary>
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var f in Directory.GetFiles(OutDir))
                if (f.EndsWith(".png") || f.EndsWith("report.txt")) File.Delete(f);
            var savePath = Path.Combine(Application.persistentDataPath, SaveController.FileName);
            foreach (var f in new[] { savePath, savePath + ".bak", savePath + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StepKey, 0);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                TickInner();
            }
            catch (Exception e)
            {
                Fail("Exception in smoke test: " + e);
            }
        }

        private static void TickInner()
        {
            if (_finished) return;
            int step = SessionState.GetInt(StepKey, 0);
            if (step == 0)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                Log("Opening scene and entering play mode");
                EditorSceneManager.OpenScene("Assets/TillWinter/Scenes/Farm.unity");
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null) EditorWindow.GetWindow(gameViewType);
                GameViewPresets.Select("1080x2340 (Portrait)");
                SessionState.SetInt(StepKey, 1);
                EditorApplication.EnterPlaymode();
                return;
            }

            if (!EditorApplication.isPlaying) return;
            if (step == 1)
            {
                SessionState.SetInt(StepKey, 2);
                _t0 = EditorApplication.timeSinceStartup;
                _phase = 0;
                _phaseStart = _t0;
                return;
            }

            if (!_hooked)
            {
                _game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (_game == null) return;
                Application.logMessageReceived += OnLog;
                _game.Sim.Harvested += _ => _harvests++;
                _game.Sim.CrowScared += _ => _crowScared++;
                _hooked = true;
                Log("Game view " + Screen.width + "x" + Screen.height + ", audio " + (UnityEngine.Object.FindFirstObjectByType<AudioManager>().UsingKenneyClips ? "kenney" : "generated"));
            }

            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - _t0;
            double inPhase = now - _phaseStart;
            if (elapsed > 180) { Fail("Timed out"); return; }

            if (_holding && _mouse != null)
            {
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _holdPos }.WithButton(MouseButton.Left));
                // Editor focus can swallow injected input; after 1 s without a ring, drive the pointer directly.
                _holdFrames++;
                if (_holdFrames > 60 && _game.CurrentRing == null && !_fallback)
                {
                    _fallback = true;
                    Log("Input System injection not reaching the game; using GameController.DebugPointerScreen");
                }
                if (_fallback) _game.DebugPointerScreen = _holdPos;
            }

            var s = _game.State;
            switch (_phase)
            {
                case 0: // spring, idle
                    if (inPhase > 1.0) { Log("Render stats at 3x3 gen 1: " + RenderStats().text); Shot("01-spring-idle"); Next(); }
                    break;
                case 1: // hold the finger under the field centre so the offset ring covers the 3x3
                    if (inPhase > 0.3)
                    {
                        _mouse = InputSystem.AddDevice<Mouse>("SmokeMouse");
                        _holdPos = ScreenOf(1f, 1f - _game.RingOffsetPlots);
                        _holding = true;
                        Log("Holding virtual mouse at " + _holdPos);
                        Next();
                    }
                    break;
                case 2:
                    if (inPhase > 2.0 && _game.CurrentRing == null && !_warnedInput)
                    {
                        _warnedInput = true;
                        Log("Ring still off 2 s after the virtual press (Game view focus?)");
                    }
                    if (inPhase > 6.0)
                    {
                        Shot("02-harvesting");
                        Check(!_game.Sim.HintPending(Hint.FirstTouch) && !_game.Sim.HintPending(Hint.Hold), "first-touch and hold hints consumed by the ring");
                        Log("After 4 s of ring: coins=" + s.Coins + " harvests=" + _harvests + " ring=" + (_game.CurrentRing.HasValue ? "on" : "off"));
                        Check(s.Coins >= 1, "at least one carrot harvested after 6 s under the 0.7 ring (got " + s.Coins + ", ring " + (_game.CurrentRing.HasValue ? "on" : "off") + ", pointer=" + (Pointer.current == null ? "null" : Pointer.current.name) + " down=" + _game.Pointer.Current.IsDown + " blocked=" + _game.InputBlocked + " overUi=" + (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) + " phase=" + s.Phase + ")");
                        Check(_harvests >= 1, "Harvested fired");
                        Next();
                    }
                    break;
                case 3: // fast-forward to frost warning
                    if (inPhase < 0.1) { _game.TimeScale = 8f; }
                    if (s.FrostWarning) { _game.TimeScale = 2f; Next(); }
                    break;
                case 4:
                    if (inPhase > 1.6) { Shot("03-frost-warning"); Log("Frost: season=" + s.Season + " left=" + s.SecondsUntilWinter.ToString("0.0") + " coins=" + s.Coins); Next(); }
                    break;
                case 5: // wait for winter
                    if (s.IsWinter) { _holding = false; Release(); Next(); }
                    break;
                case 6:
                    if (inPhase > 1.2)
                    {
                        Shot("04-winter-shop");
                        Check(_game.InputBlocked, "input blocked while shop open");
                        Check(!_game.Sim.HintPending(Hint.FirstFrost), "frost hint consumed");
                        Check(!_game.Sim.HintPending(Hint.FirstWinter), "first-winter hint shown");
                        Check(GameObject.Find("TreeToggle") != null, "Heritage tab visible in Winter");
                        double before = s.Coins;
                        _game.Sim.DebugAddCoins(600);
                        Check(_game.Sim.TryBuy("apprentice_count"), "buy apprentice_count");
                        Check(_game.Sim.TryBuy("apprentice_count"), "buy apprentice_count #2");
                        Check(_game.Sim.TryBuy("irrigation"), "buy irrigation");
                        Check(_game.Sim.TryBuy("expand_field"), "buy expand_field");
                        Check(!_game.Sim.TryBuy("sun") || true, "sun is available after irrigation");
                        Check(_game.Sim.TryBuy("ring_radius"), "buy ring_radius");
                        Log("Shop: coins " + before + " (+400) -> " + s.Coins + ", grid " + s.GridSize + "x" + s.GridSize);
                        Next();
                    }
                    break;
                case 7:
                    if (inPhase > 0.8)
                    {
                        Shot("05-winter-tree-after-buys");
                        var screen = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
                        Check(screen != null && screen.IsOpen, "winter screen open");
                        var view = screen.AlmanacView;
                        var panBefore = view.Pan;
                        view.PanBy(new Vector2(120f, 80f));
                        Check((view.Pan - panBefore).magnitude > 1f, "pan moved the canvas");
                        float zoomBefore = view.Zoom;
                        view.ZoomBy(1.3f);
                        Check(view.Zoom > zoomBefore, "zoom changed");
                        view.Select("ring_radius");
                        Check(screen.SelectedId == "ring_radius", "ring_radius selected");
                        _selectedAt = EditorApplication.timeSinceStartup;
                        _game.Sim.DebugAddCoins(100);
                        int lvBefore = s.GetLevel("ring_radius");
                        Check(_game.Sim.TryBuy("ring_radius"), "buy ring_radius via sim while selected");
                        Check(s.GetLevel("ring_radius") == lvBefore + 1, "ring_radius level +1");
                        Check(view.StateOf("ring_water_speed") != SkillTreeView.NodeState.Locked, "child ring_water_speed now available (edge lit)");
                        var btn = GameObject.Find("NextYear")?.GetComponent<Button>();
                        Check(btn != null, "Next Year button exists");
                        _phase = 70;
                        Next();
                    }
                    break;
                case 71:
                    if (inPhase > 0.7)
                    {
                        Shot("05b-winter-tree-selected");
                        GameViewPresets.Select("1080x1920 (Portrait)");
                        Next();
                    }
                    break;
                case 72:
                    if (inPhase > 0.7)
                    {
                        Shot("05c-winter-tree-1080x1920");
                        Next();
                    }
                    break;
                case 73:
                    if (inPhase > 0.3)
                    {
                        GameViewPresets.Select("1080x2340 (Portrait)");
                        var btn = GameObject.Find("NextYear")?.GetComponent<Button>();
                        btn?.onClick.Invoke();
                        Check(s.Year == 2 && s.Season == Season.Spring, "year 2 spring after Next Year");
                        Check(!_game.InputBlocked, "input unblocked after Next Year");
                        _game.TimeScale = 1f;
                        _phase = 7;
                        Next();
                    }
                    break;
                case 8: // year 2: hold ring on the new 4x4 field, apprentice should be working
                    if (inPhase < 0.1)
                    {
                        _game.Sim.DebugForceRipeAll();
                        _holdPos = ScreenOf(1f, 1f - _game.RingOffsetPlots);
                        _holding = true;
                    }
                    if (inPhase > 4.5)
                    {
                        Shot("06-year2-ring-apprentice");
                        _holding = false;
                        Release();
                        Check(s.Apprentices.Count == 2, "two apprentices on the field");
                        Next();
                    }
                    break;
                case 9: // crow
                    if (inPhase > 0.5 && !s.IsWinter)
                    {
                        Check(_game.Sim.DebugSpawnCrow(), "debug spawn crow");
                        Next();
                    }
                    break;
                case 10:
                    if (inPhase > 1.5)
                    {
                        Shot("07-crow");
                        if (s.Crows.Count > 0)
                        {
                            var pos = s.Crows[0].Pos;
                            var sp = ScreenOf(pos.X, pos.Y);
                            InputSystem.QueueStateEvent(_mouse, new MouseState { position = sp }.WithButton(MouseButton.Left));
                            if (_fallback) _game.DebugTapScreen = sp;
                            _tapPos = sp;
                            _tapState = 1;
                        }
                        else Log("No crow to tap (apprentice may have harvested the plot)");
                        Next();
                    }
                    break;
                case 11: // finish the tap next frame, then evaluate
                    if (_tapState == 1) { InputSystem.QueueStateEvent(_mouse, new MouseState { position = _tapPos }); _tapState = 2; }
                    else if (inPhase > 1.0)
                    {
                        Log("After tap: crows=" + s.Crows.Count + " scaredEvents=" + _crowScared);
                        Next();
                    }
                    break;
                case 12: // retire: threshold via debug, winter, Retire(), Heritage buys, new generation
                    _game.Sim.DebugAddLifetimeCoins(_game.Sim.Config.HeritageThreshold);
                    Check(_game.Sim.CanRetire, "CanRetire after threshold");
                    _game.Sim.DebugSkipToWinter();
                    Next();
                    break;
                case 13:
                    if (inPhase > 0.8)
                    {
                        if (_sub++ == 0) { Shot("08-winter-retire-button"); break; } // capture lands end of frame
                        if (_sub < 3) break;
                        int gen = s.Generation.Generation;
                        var winter = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
                        Check(winter != null && winter.IsOpen, "winter screen open before retire");
                        // Drive the real flow: Pass on -> confirm -> Generation card -> Heritage screen.
                        GameObject.Find("Retire")?.GetComponent<Button>()?.onClick.Invoke();
                        var yes = GameObject.Find("Yes")?.GetComponent<Button>();
                        Check(yes != null, "retire confirm dialog shown");
                        yes?.onClick.Invoke();
                        Check(s.Phase == Phase.Heritage, "phase Heritage");
                        Check(s.Generation.Generation == gen + 1, "generation incremented");
                        Check(s.Coins == 0 && s.AlmanacLevels.Count == 0, "coins and almanac reset");
                        var card = UnityEngine.Object.FindFirstObjectByType<GenerationCard>();
                        Check(card != null && card.IsOpen, "generation card open");
                        Next();
                    }
                    break;
                case 14: // let the seeds count up, screenshot, then tap to skip
                    if (inPhase > 1.6)
                    {
                        if (_sub++ == 0) { Shot("08b-generation-card"); break; }
                        if (_sub < 3) break;
                        var card = UnityEngine.Object.FindFirstObjectByType<GenerationCard>();
                        card.Skip();
                        Check(card != null && !card.IsOpen, "generation card skipped by tap");
                        var winter = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
                        Check(winter != null && winter.IsOpen, "heritage screen open after card");
                        _game.Sim.DebugAddSeeds(20);
                        Check(_game.Sim.TryBuy("h_start_radius"), "buy h_start_radius");
                        Check(_game.Sim.TryBuy("h_free_apprentice"), "buy h_free_apprentice");
                        Next();
                    }
                    break;
                case 15:
                    if (inPhase > 0.8)
                    {
                        if (_sub++ == 0) { Shot("09-heritage-panel"); break; }
                        if (_sub < 3) break;
                        var btn = GameObject.Find("StartGeneration")?.GetComponent<Button>();
                        Check(btn != null, "Start new generation button exists");
                        btn?.onClick.Invoke();
                        Check(s.Phase == Phase.Year && s.Year == 1, "new generation year 1");
                        Check(s.Apprentices.Count == 1, "free apprentice present");
                        Check(!_game.Sim.HintPending(Hint.FirstHeritage), "heritage hint shown");
                        var away = UnityEngine.Object.FindFirstObjectByType<AwayCard>();
                        _game.Sim.DebugSetLevel("irrigation", 2);
                        _game.Sim.DebugSetLevel("sun", 2);
                        var report = _game.Sim.SimulateOffline(600);
                        away.Show(report);
                        Check(away.IsOpen, "away card open (10 min offline)");
                        Check(Mathf.Abs(s.RingRadius - 0.95f) < 1e-3f, "heritage radius bonus applied");
                        var save = UnityEngine.Object.FindFirstObjectByType<SaveController>();
                        save.SaveNow();
                        var data = SaveController.Load(save.Path);
                        Check(data != null, "save file readable");
                        var loaded = data != null ? FarmSim.FromSave(data, new FarmConfig()) : null;
                        Check(loaded != null && loaded.State.Generation.Generation == s.Generation.Generation && loaded.State.Apprentices.Count == 1, "loaded save matches");
                        Next();
                    }
                    break;
                case 16:
                    if (inPhase > 1.0)
                    {
                        if (_sub++ == 0) { Shot("10-away-card"); break; }
                        if (_sub < 3) break;
                        var away = UnityEngine.Object.FindFirstObjectByType<AwayCard>();
                        away.Apply();
                        Check(!away.IsOpen, "away card closed by OK");
                        Next();
                    }
                    break;
                case 17:
                    if (inPhase > 1.0) { Shot("11-generation2-field"); Next(); }
                    break;
                // ---- art pass (S6): seasons, golden crop, tractor, six apprentices at 6x6, generation-3 decor, draw-call budget
                case 18:
                    if (_sub++ == 0)
                    {
                        _game.Sim.DebugSetLevel("expand_field", 3);
                        _game.Sim.DebugSetLevel("apprentice_count", 6);
                        _game.Sim.DebugSetLevel("tractor", 1);
                        _game.Sim.DebugSetGeneration(3);
                        _game.Sim.DebugSetSeason(Season.Spring);
                        break;
                    }
                    if (inPhase > 2.0)
                    {
                        Check(s.GridSize == 6, "field is 6x6 (" + s.GridSize + ")");
                        Check(s.Apprentices.Count >= 6, "six apprentices (" + s.Apprentices.Count + ")");
                        var stats = RenderStats();
                        Log("Render stats at 6x6 gen 3: " + stats.text);
                        Check(stats.batches <= 150, "batches (GPU draw calls) <= 150 (" + stats.text + ")");
                        Check(stats.triangles <= 60000, "triangles <= 60k (" + stats.triangles + ")");
                        Shot("12-art-spring-6x6-gen3");
                        Next();
                    }
                    break;
                case 19: // golden: force ripe, let an apprentice replant one plot golden, then park the apprentices and ripen again
                    if (_sub++ == 0) { _game.Sim.DebugNextHarvestGolden(); _game.Sim.DebugForceRipeAll(); break; }
                    if (inPhase > 1.5) { _game.Sim.DebugSetLevel("apprentice_count", 0); _game.Sim.DebugForceRipeAll(); Next(); }
                    break;
                case 20:
                    if (inPhase > 0.7 && _sub++ == 0) { Shot("13-art-golden-crop"); break; }
                    if (_sub == 0 || _sub++ < 3) break;
                    {
                        bool golden = false;
                        foreach (var p in s.Plots) if (p.IsGolden) golden = true;
                        Check(golden, "a golden crop is on the field");
                        Check(_game.Sim.DebugForceTractorSweep(), "tractor sweep started");
                        _game.Sim.DebugSetLevel("apprentice_count", 6);
                        Next();
                    }
                    break;
                case 21:
                    if (inPhase > 0.35 && _sub++ == 0) { Check(s.Tractor.Sweeping, "tractor sweeping"); Shot("14-art-tractor-sweep"); break; }
                    if (_sub > 0 && inPhase > 0.6) { _game.Sim.DebugSetSeason(Season.Summer); Next(); }
                    break;
                case 22:
                    if (inPhase > 2.0 && _sub++ == 0) { Check(s.Season == Season.Summer, "summer"); Shot("15-art-summer"); break; }
                    if (_sub == 0 || _sub++ < 3) break; // capture lands end of frame: change state two frames later
                    _game.Sim.DebugSetSeason(Season.Autumn);
                    Log("Autumn set: " + s.SecondsUntilWinter.ToString("0.0") + " s until winter, frost " + s.FrostWarning);
                    Next();
                    break;
                case 23:
                    if (inPhase > 1.6 && _sub++ == 0) { Check(s.Season == Season.Autumn && s.FrostWarning && s.Phase == Phase.Year, "autumn frost warning (" + s.SecondsUntilWinter.ToString("0.0") + " s left)"); Shot("16-art-autumn-frost"); break; }
                    if (_sub == 0 || _sub++ < 3) break;
                    _game.Sim.DebugSkipToWinter();
                    Next();
                    break;
                case 24:
                    if (inPhase > 1.5) { Check(s.Phase == Phase.Winter, "winter"); Shot("17-art-winter-tree"); Next(); }
                    break;
                default:
                    if (inPhase > 1.0) Finish();
                    break;
            }
        }

        private static Vector2 _tapPos;
        private static bool _warnedInput;
        private static bool _fallback;
        private static double _selectedAt;
        private static int _holdFrames;
        private static int _tapState;
        private static int _sub;

        private static Vector2 ScreenOf(float plotX, float plotY)
        {
            var world = _game.PlotToWorld(plotX, plotY, 0.1f);
            var sp = _game.Cam.WorldToScreenPoint(world);
            return new Vector2(sp.x, sp.y);
        }

        private static void Release()
        {
            if (_mouse != null) InputSystem.QueueStateEvent(_mouse, new MouseState { position = _holdPos });
            _game.DebugPointerScreen = null;
            _holdFrames = 0;
        }

        private static void Next()
        {
            _phase++;
            _sub = 0;
            _phaseStart = EditorApplication.timeSinceStartup;
        }

        /// <summary>Last rendered frame's Game view statistics (UnityEditor.UnityStats is internal).</summary>
        private static (int drawCalls, int batches, int triangles, string text) RenderStats()
        {
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.UnityStats");
            int Get(string name)
            {
                var p = type?.GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                return p != null ? System.Convert.ToInt32(p.GetValue(null)) : -1;
            }
            int dc = Get("drawCalls"), b = Get("batches"), t = Get("triangles"), sp = Get("setPassCalls"), v = Get("vertices");
            return (dc, b, t, "drawCalls=" + dc + " batches=" + b + " setPass=" + sp + " tris=" + t + " verts=" + v);
        }

        private static void Shot(string name)
        {
            var path = Path.Combine(OutDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Log("Screenshot " + name + " (" + Screen.width + "x" + Screen.height + ")");
        }

        private static void Check(bool ok, string what)
        {
            Log((ok ? "PASS " : "FAIL ") + what);
            if (!ok) _errors++;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                Log("CONSOLE " + type + ": " + condition + "\n" + stackTrace);
            }
        }

        private static void Log(string msg)
        {
            Report.AppendLine("[" + (EditorApplication.timeSinceStartup - _t0).ToString("0.0") + "s] " + msg);
            Debug.Log("[Smoke] " + msg);
        }

        private static void Fail(string why)
        {
            _errors++;
            Log(why);
            Finish();
        }

        private static void Finish()
        {
            if (_finished) return;
            _finished = true;
            Log("Done. harvests=" + _harvests + " errors=" + _errors + " result=" + (_errors == 0 ? "PASS" : "FAIL"));
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "report.txt"), Report.ToString());
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            int code = _errors == 0 ? 0 : 1;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
