using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TillWinter.Core;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Plays the game's mechanics end to end through the real input and UI paths and writes a report: strike, water,
    /// reap, winter purchases, next year, field expansion, retire, the album page, Heritage purchases, a new
    /// generation — and after every screen change, whether a tap on the field still lands (and what UI sits under the
    /// finger when it does not). Run with mechanics-probe.bat; the report and screenshots go to -probeOut.
    /// </summary>
    [InitializeOnLoad]
    public static class MechanicsProbe
    {
        private const string ActiveKey = "TillWinter.MechanicsProbe.Active";
        private const string StepKey = "TillWinter.MechanicsProbe.Step";
        private const string OutKey = "TillWinter.MechanicsProbe.Out";
        private const string BackupSuffix = ".probe-bak";
        private const string ExitKey = "TillWinter.MechanicsProbe.Exit";

        private static double _t0, _phaseStart;
        private static int _phase, _sub, _frames;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _errors, _harvests, _breaks;
        private static bool _hooked, _finished;
        private static GameController _game;
        private static GridPos _target;
        private static int _strikesBefore;
        private static double _statBefore;

        static MechanicsProbe()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
            // Leaving play mode reloads the domain: the exit step re-registers itself here.
            if (SessionState.GetInt(ExitKey, -1) >= 0) EditorApplication.update += WaitThenExit;
        }

        public static void Run()
        {
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults", "probe");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-probeOut") output = args[i + 1];
            Directory.CreateDirectory(output);
            foreach (var f in Directory.GetFiles(output)) if (f.EndsWith(".png") || f.EndsWith("report.txt")) File.Delete(f);
            SessionState.SetString(OutKey, output);
            // The developer's save is set aside for the run and put back at the end (the probe starts a fresh farm).
            var savePath = Path.Combine(Application.persistentDataPath, SaveController.FileName);
            if (File.Exists(savePath)) File.Copy(savePath, savePath + BackupSuffix, true);
            SaveController.DeleteFiles(savePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ExitKey, -1);
            SessionState.SetInt(StepKey, 0);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try { TickInner(); }
            catch (Exception e) { Fail("Exception in probe: " + e); }
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
                _game.Sim.PlotBroken += _ => _breaks++;
                _hooked = true;
                Log("Game view " + Screen.width + "x" + Screen.height);
            }
            double now = EditorApplication.timeSinceStartup;
            if (now - _t0 > 150) { Fail("Timed out in phase " + _phase); return; }
            double inPhase = now - _phaseStart;
            var s = _game.State;
            var sim = _game.Sim;
            _frames++;

            switch (_phase)
            {
                case 0: // spring, idle
                    if (inPhase > 1.0) { Shot("00-start"); Describe("start"); Next(); }
                    break;
                case 1: // strike the centre plot until it breaks
                    if (_sub == 0 && (_sub = 1) == 1) { _target = new GridPos(1, 1); _strikesBefore = s.Generation.Strikes; Log("Tapping " + _target + " at " + ScreenOf(_target)); }
                    TapLoop(s, inPhase, 8.0, "year 1 strike", 2);
                    break;
                case 2: // hold on the growing plot: watering
                    if (_sub == 0 && (_sub = 1) == 1) _game.DebugPointerScreen = ScreenOf(_target);
                    if (inPhase > 1.0)
                    {
                        Check((s.WateringPos.HasValue && s.WateringPos.Value == _target) || s.GetPlot(_target).Watering, "holding the finger waters " + _target + " (watering=" + (s.WateringPos.HasValue ? s.WateringPos.ToString() : "none") + ", state " + s.GetPlot(_target).State + ")");
                        _game.DebugPointerScreen = null;
                        Next();
                    }
                    break;
                case 3: // ripe, then a tap reaps
                    if (_sub == 0 && (_sub = 1) == 1) { sim.DebugForceRipeAll(); _harvests = 0; }
                    if (inPhase > 0.3 && _sub == 1) { _game.DebugTapScreen = ScreenOf(_target); _sub = 2; }
                    if (inPhase > 0.8)
                    {
                        Check(_harvests >= 1, "a tap on the ripe plot reaped it (harvests " + _harvests + ")");
                        Check(s.GetPlot(_target).IsHard && s.GetPlot(_target).Layer == 1, "the reaped plot is hard again one layer deeper (state " + s.GetPlot(_target).State + ", layer " + s.GetPlot(_target).Layer + ")");
                        Next();
                    }
                    break;
                case 4: // winter: buy through the real purchase path, then Next Year through the button
                    if (_sub == 0 && (_sub = 1) == 1) { sim.DebugAddCoins(6000); sim.DebugSkipToWinter(); }
                    if (inPhase > 1.2 && _sub == 1)
                    {
                        _sub = 2;
                        var winter = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
                        Check(winter != null && winter.IsOpen && s.Phase == Phase.Winter, "winter screen open (phase " + s.Phase + ")");
                        Shot("04-winter");
                        double dmg = s.Stats.StrikeDamage;
                        Check(sim.TryBuy("hoe_damage") && s.Stats.StrikeDamage > dmg, "hoe_damage raises StrikeDamage " + dmg + " -> " + s.Stats.StrikeDamage);
                        double growth = s.Stats.GrowthMult;
                        Check(sim.TryBuy("growth") && s.Stats.GrowthMult > growth, "growth raises GrowthMult " + growth + " -> " + s.Stats.GrowthMult);
                        int grid = s.GridSize;
                        bool bought = sim.TryBuy("expand_field");
                        Log("expand_field bought=" + bought + ", grid " + grid + " -> " + s.GridSize + " (target " + s.Stats.TargetGridSize + ")");
                        Check(sim.TryBuy("apprentice_count"), "apprentice_count bought (count " + s.Stats.ApprenticeCount + ")");
                        Check(sim.TryBuy("year_length"), "year_length bought (year " + s.Stats.YearLength + " s)");
                        Check(sim.TryBuy("stamina_depot"), "stamina_depot bought (max " + s.Stats.StaminaMax + ")");
                        Click("NextYear");
                    }
                    if (inPhase > 3.0 && _sub == 2) { _sub = 3; Describe("year 2 after Next Year"); Shot("05-year2"); Next(); }
                    break;
                case 5: // year 2: does a tap still land?
                    if (_sub == 0 && (_sub = 1) == 1) { _target = FirstHard(s); _strikesBefore = s.Generation.Strikes; Log("Year " + s.Year + ", grid " + s.GridSize + ", tapping " + _target + " at " + ScreenOf(_target)); }
                    TapLoop(s, inPhase, 8.0, "year 2 strike", 6);
                    break;
                case 6: // second winter: more nodes, then retire through the real buttons
                    if (_sub == 0 && (_sub = 1) == 1) { sim.DebugAddCoins(20000); sim.DebugSkipToWinter(); }
                    if (inPhase > 1.2 && _sub == 1)
                    {
                        _sub = 2;
                        foreach (var id in new[] { "unlock_tomato", "upgrade_plot", "barn", "scarecrow", "soft_ground", "steady_hand", "lucky_hoe", "frost_warning" })
                            Check(sim.TryBuy(id), "bought " + id);
                        Log("stats after second winter: dmg " + s.Stats.StrikeDamage + " cd " + s.Stats.StrikeCooldown + " crit " + s.Stats.CritChance + " window " + s.Stats.CritWindow + " hpMult " + s.Stats.HpMult + " growth " + s.Stats.GrowthMult + " tier " + s.Stats.MaxTierUnlocked + " scarecrows " + s.Stats.ScarecrowCount + " barn " + s.Stats.BarnCapacity);
                        sim.DebugAddLifetimeCoins(sim.Config.HeritageThreshold);
                        Check(sim.CanRetire, "can retire");
                        Click("Retire");
                    }
                    if (inPhase > 1.8 && _sub == 2) { _sub = 3; Shot("06-retire-confirm"); Check(Click("Yes"), "confirm sheet has a Yes button"); }
                    if (inPhase > 3.4 && _sub == 3)
                    {
                        _sub = 4;
                        var card = UnityEngine.Object.FindFirstObjectByType<GenerationCard>();
                        Check(s.Phase == Phase.Heritage, "phase Heritage after retire (phase " + s.Phase + ")");
                        Check(card != null && card.IsOpen, "album page open");
                        Shot("07-album");
                        Check(Click("Heir1"), "album page: heir card 1 clickable");
                        Check(s.Generation.Trait == s.Generation.HeirOffer[1], "heir 1 chosen (" + s.Generation.Trait + ")");
                        Check(Click("Continue"), "album page: Continue");
                    }
                    if (inPhase > 5.0 && _sub == 4)
                    {
                        _sub = 5;
                        var winter = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
                        Check(winter != null && winter.IsOpen, "heritage screen open after the album page");
                        sim.DebugAddSeeds(40);
                        double dmg = s.Stats.StrikeDamage;
                        Check(sim.TryBuy("h_start_damage") && s.Stats.StrikeDamage > dmg, "h_start_damage raises StrikeDamage " + dmg + " -> " + s.Stats.StrikeDamage);
                        double growth = s.Stats.GrowthMult;
                        Check(sim.TryBuy("h_start_growth"), "h_start_growth bought: GrowthMult " + growth + " -> " + s.Stats.GrowthMult);
                        Check(sim.TryBuy("h_free_apprentice"), "h_free_apprentice bought: apprentices " + s.Stats.ApprenticeCount);
                        Check(sim.TryBuy("h_start_year_length"), "h_start_year_length bought: year " + s.Stats.YearLength);
                        Shot("08-heritage");
                        Click("StartGeneration");
                    }
                    if (inPhase > 7.0 && _sub == 5) { _sub = 6; Describe("generation 2, year 1"); Shot("09-gen2"); Next(); }
                    break;
                case 7: // generation 2: tap again
                    if (_sub == 0 && (_sub = 1) == 1) { _target = FirstHard(s); _strikesBefore = s.Generation.Strikes; Log("Gen " + s.Generation.Generation + " year " + s.Year + ", grid " + s.GridSize + ", tapping " + _target + " at " + ScreenOf(_target)); }
                    TapLoop(s, inPhase, 8.0, "generation 2 strike", 8);
                    break;
                case 8: // heritage + almanac on the same stat: floors and sums
                    if (_sub == 0 && (_sub = 1) == 1) { sim.DebugAddCoins(6000); sim.DebugSkipToWinter(); }
                    if (inPhase > 1.2)
                    {
                        double dmg = s.Stats.StrikeDamage;
                        Check(sim.TryBuy("hoe_damage") && Math.Abs(s.Stats.StrikeDamage - (dmg + 1)) < 1e-6, "hoe_damage on top of h_start_damage: " + dmg + " -> " + s.Stats.StrikeDamage + " (expected +1)");
                        double growth = s.Stats.GrowthMult;
                        bool ok = sim.TryBuy("growth");
                        Log("growth level 1 with h_start_growth floor already at 1: GrowthMult " + growth + " -> " + s.Stats.GrowthMult + (Math.Abs(growth - s.Stats.GrowthMult) < 1e-6 ? "  (NO EFFECT: the Heritage floor already covered level 1)" : ""));
                        double g2 = s.Stats.GrowthMult;
                        Check(sim.TryBuy("growth") && s.Stats.GrowthMult > g2, "growth level 2 raises GrowthMult " + g2 + " -> " + s.Stats.GrowthMult);
                        Log("unlock_tomato with tier already unlocked by Heritage? tier " + s.Stats.MaxTierUnlocked + ", available " + sim.IsAvailable("unlock_tomato") + ", maxed " + sim.IsMaxed("unlock_tomato"));
                        Click("NextYear");
                        Next();
                    }
                    break;
                case 9:
                    if (inPhase > 2.0) { Describe("generation 2, year 2"); _target = FirstHard(s); _strikesBefore = s.Generation.Strikes; Next(); }
                    break;
                case 10:
                    TapLoop(s, inPhase, 8.0, "generation 2 year 2 strike", 11);
                    break;
                case 11:
                    if (_sub == 0) { _sub = 1; Shot("10-end"); }
                    else if (inPhase > 0.5) Finish();
                    break;
            }
        }

        /// <summary>Taps the target every 20 frames until it breaks or the time is up; on failure says what sat under the finger.</summary>
        private static void TapLoop(FarmState s, double inPhase, double seconds, string what, int nextPhase)
        {
            var plot = s.GetPlot(_target);
            if (plot.IsHard && _frames % 20 == 0) _game.DebugTapScreen = ScreenOf(_target);
            bool broke = !plot.IsHard;
            if (broke || inPhase > seconds)
            {
                int strikes = s.Generation.Strikes - _strikesBefore;
                Check(strikes > 0, what + ": strikes landed (" + strikes + ", broke=" + broke + ", hp " + plot.Hp.ToString("0.0") + "/" + plot.MaxHp.ToString("0.0") + ")");
                if (strikes == 0)
                {
                    Describe(what + " FAILED");
                    Shot("fail-" + _phase);
                    var screen = ScreenOf(_target);
                    bool mapped = _game.TryScreenToPlot(screen, out var back);
                    Log("  tap point " + screen + " maps back to plot " + (mapped ? back.ToString() : "nothing") + "; target " + _target + " state " + plot.State + " crow " + plot.HasCrow
                        + "; pest " + s.Pest.Kind + " at " + s.Pest.Pos + "; crows " + s.Crows.Count + "; cloud " + s.Cloud.Active + "; canStrike " + _game.Sim.CanStrike);
                    foreach (var a in s.Apprentices) Log("  apprentice " + a.Index + " at (" + a.X.ToString("0.00") + "," + a.Y.ToString("0.00") + ") role " + a.Role + " working " + a.IsWorking + " target " + (a.HasTarget ? a.Target.ToString() : "-"));
                    int before = s.Generation.Strikes;
                    bool direct = _game.Sim.Strike(_target);
                    Log("  Core Strike(" + _target + ") directly: " + direct + " (strikes " + before + " -> " + s.Generation.Strikes + ")");
                }
                _phase = nextPhase;
                _sub = 0;
                _phaseStart = EditorApplication.timeSinceStartup;
            }
        }

        private static GridPos FirstHard(FarmState s)
        {
            foreach (var p in s.Plots) if (p.IsHard) return p.Pos;
            return new GridPos(0, 0);
        }

        /// <summary>Everything that could stop a tap: flags, overrides, open screens, and the UI under the target plot.</summary>
        private static void Describe(string when)
        {
            var s = _game.State;
            var winter = UnityEngine.Object.FindFirstObjectByType<WinterScreen>();
            var card = UnityEngine.Object.FindFirstObjectByType<GenerationCard>();
            Log(when + ": phase " + s.Phase + ", year " + s.Year + ", gen " + s.Generation.Generation + ", grid " + s.GridSize
                + ", inputBlocked " + _game.InputBlocked + ", paused " + _game.Paused
                + ", plotOverride " + (_game.PlotTapOverride != null) + ", fieldOverride " + (_game.FieldTapOverride != null)
                + ", winterOpen " + (winter != null && winter.IsOpen) + ", cardOpen " + (card != null && card.IsOpen)
                + ", cooldown " + s.StrikeCooldownLeft.ToString("0.00") + ", stamina " + s.Stamina.ToString("0") + ", timeScale " + Time.timeScale);
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) { Log("  no EventSystem"); return; }
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            int shown = 0;
            foreach (var p in s.Plots)
            {
                var data = new UnityEngine.EventSystems.PointerEventData(es) { position = ScreenOf(p.Pos) };
                hits.Clear();
                es.RaycastAll(data, hits);
                foreach (var h in hits)
                {
                    if (shown++ < 12) Log("  UI over plot " + p.Pos + ": " + NodePath(h.gameObject.transform));
                }
            }
            if (shown == 0) Log("  no UI over any plot");
        }

        private static string NodePath(Transform t) => t.parent == null ? t.name : NodePath(t.parent) + "/" + t.name;

        private static bool Click(string name)
        {
            var go = GameObject.Find(name);
            var btn = go != null ? go.GetComponent<Button>() : null;
            if (btn == null) { Log("no active button '" + name + "'"); return false; }
            btn.onClick.Invoke();
            return true;
        }

        private static Vector2 ScreenOf(GridPos p)
        {
            var sp = _game.Cam.WorldToScreenPoint(_game.PlotToWorld(p, 0.1f));
            return new Vector2(sp.x, sp.y);
        }

        private static void Next()
        {
            _phase++;
            _sub = 0;
            _phaseStart = EditorApplication.timeSinceStartup;
        }

        private static void Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(OutKey, "."), name + ".png"));
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
            Debug.Log("[Probe] " + msg);
        }

        private static void Fail(string why)
        {
            _errors++;
            Log(why);
            Finish();
        }

        /// <summary>After play mode has fully stopped (and the game has written its own save), the developer's save goes back.</summary>
        private static void WaitThenExit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitThenExit;
            var savePath = Path.Combine(Application.persistentDataPath, SaveController.FileName);
            if (File.Exists(savePath + BackupSuffix))
            {
                SaveController.DeleteFiles(savePath);
                File.Move(savePath + BackupSuffix, savePath);
            }
            int code = SessionState.GetInt(ExitKey, 1);
            SessionState.SetInt(ExitKey, -1);
            // Straight out, like the tour: a delayCall chain here was lost on the way and left the editor open.
            EditorApplication.Exit(code);
        }

        private static void Finish()
        {
            if (_finished) return;
            _finished = true;
            Log("Done. harvests=" + _harvests + " breaks=" + _breaks + " errors=" + _errors + " result=" + (_errors == 0 ? "PASS" : "FAIL"));
            var dir = SessionState.GetString(OutKey, ".");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "report.txt"), Report.ToString());
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            int code = _errors == 0 ? 0 : 1;
            EditorApplication.isPlaying = false;
            SessionState.SetInt(ExitKey, code);
            EditorApplication.update += WaitThenExit;
        }
    }
}
