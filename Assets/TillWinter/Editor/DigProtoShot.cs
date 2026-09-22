using System;
using System.IO;
using TillWinter.Unity.Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// proto-shot.bat: opens the core-loop v3 prototype scene, enters play mode, drives it through its debug hooks
    /// (strike the centre tile until it breaks, let it grow, reap, end the year) and writes a few screenshots to a
    /// folder — the UiTour idea for one throwaway screen. Everything happens from the editor update, once the editor
    /// is idle (play mode cannot be entered from the -executeMethod call itself); state lives in SessionState because
    /// play mode reloads the domain. A watchdog quits with 1 if nothing moves for two minutes.
    /// </summary>
    [InitializeOnLoad]
    public static class DigProtoShot
    {
        private const string ActiveKey = "TillWinter.ProtoShot.Active", OutKey = "TillWinter.ProtoShot.Out", StepKey = "TillWinter.ProtoShot.Step",
            TimeKey = "TillWinter.ProtoShot.Time", TotalKey = "TillWinter.ProtoShot.Total";
        private const string ScenePath = "Assets/TillWinter/Scenes/Proto.unity";

        static DigProtoShot()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults", "proto");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-protoOut") output = args[i + 1];
            Directory.CreateDirectory(output);
            SessionState.SetString(OutKey, output);
            SessionState.SetInt(StepKey, -1);
            SessionState.SetFloat(TimeKey, 0f);
            SessionState.SetFloat(TotalKey, 0f);
            SessionState.SetBool(ActiveKey, true);
            Log("Run: waiting for the editor to settle");
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            float total = SessionState.GetFloat(TotalKey, 0f) + Mathf.Min(0.1f, Time.unscaledDeltaTime);
            SessionState.SetFloat(TotalKey, total);
            if (total > 120f) { Log("FAIL watchdog: nothing finished in 120 s"); Finish(1); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            int step = SessionState.GetInt(StepKey, -1);
            if (step < 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("DigProtoBootstrap").AddComponent<DigProtoBootstrap>();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    Log("created " + ScenePath);
                }
                else EditorSceneManager.OpenScene(ScenePath);
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null) EditorWindow.GetWindow(gameViewType);
                GameViewPresets.Select("1080x2340 (Portrait)");
                SessionState.SetInt(StepKey, 0);
                SessionState.SetFloat(TimeKey, 0f);
                Log("entering play mode");
                EditorApplication.EnterPlaymode();
                return;
            }
            if (step >= 100)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Finish(0);
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var proto = UnityEngine.Object.FindFirstObjectByType<DigProtoBootstrap>();
            if (proto == null) return;
            float t = SessionState.GetFloat(TimeKey, 0f) + Time.unscaledDeltaTime;
            SessionState.SetFloat(TimeKey, t);
            string dir = SessionState.GetString(OutKey, "");
            switch (step)
            {
                case 0: if (t > 1.5f) { Shoot(dir, "00-start"); Next(); } break;
                case 1: if (t > 0.3f) { proto.DebugStrike(4); Next(); } break;
                case 2: if (t > 0.4f) { proto.DebugStrike(4); Shoot(dir, "01-struck"); Next(); } break;
                case 3: if (t > 0.5f) { proto.DebugStrike(4); proto.DebugStrike(4); proto.DebugStrike(4); Next(); } break;
                case 4: if (t > 0.6f) { Shoot(dir, "02-broken-growing"); Next(); } break;
                case 5: if (t > 4f) { Shoot(dir, "03-ripe"); Next(); } break;
                case 6: if (t > 0.3f) { proto.DebugReapAll(); Next(); } break;
                case 7: if (t > 0.4f) { Shoot(dir, "04-reaped"); Next(); } break;
                case 8: if (t > 0.3f) { proto.DebugEndYear(); Next(); } break;
                case 9: if (t > 2.5f) { Shoot(dir, "05-winter"); Next(); } break;
                default:
                    Log("leaving play mode");
                    SessionState.SetInt(StepKey, 100);
                    EditorApplication.ExitPlaymode();
                    break;
            }
        }

        private static void Next()
        {
            SessionState.SetInt(StepKey, SessionState.GetInt(StepKey, 0) + 1);
            SessionState.SetFloat(TimeKey, 0f);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Log("Done, exit " + code);
            EditorApplication.Exit(code);
        }

        private static void Shoot(string dir, string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Log("shot " + name);
        }

        private static void Log(string line)
        {
            string dir = SessionState.GetString(OutKey, "");
            if (dir.Length > 0) { try { File.AppendAllText(Path.Combine(dir, "report.txt"), line + "\n"); } catch (IOException) { } }
            Debug.Log("[Proto] " + line);
        }
    }
}
