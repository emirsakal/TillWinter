using System.IO;
using TillWinter.Unity.Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Opens the core-loop v3 prototype scene (GDD §2v3.13), creating it on first use: one DigProtoBootstrap object,
    /// nothing else, never added to the build list. Play it in the editor or the device simulator.
    /// </summary>
    public static class DigProtoSetup
    {
        private const string ScenePath = "Assets/TillWinter/Scenes/Proto.unity";

        [MenuItem("Till Winter/Core v3 prototype (open scene)")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("DigProtoBootstrap").AddComponent<DigProtoBootstrap>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Proto] created " + ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Till Winter/Core v3 prototype (play)")]
        public static void Play()
        {
            Open();
            EditorApplication.isPlaying = true;
        }
    }
}
