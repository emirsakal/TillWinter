using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Game view sizes are stored per editor install, not per project, so this adds the
    /// portrait presets the demo is designed for on load. Uses reflection on the internal
    /// UnityEditor.GameViewSizes API; everything is guarded so an internal API change only
    /// logs a warning instead of breaking the editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewPresets
    {
        /// <summary>Why the last <see cref="Select"/> failed, for tools that report it.</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>The pixel size of a named preset.</summary>
        public static bool TryGetSize(string name, out int width, out int height)
        {
            foreach (var (n, w, h) in Sizes)
                if (n == name) { width = w; height = h; return true; }
            width = height = 0;
            return false;
        }

        private const string FirstRunKey = "TillWinter.GameViewPresets.SelectedDefault";
        private const string PreferredName = "1080x2340 (Portrait)";

        private static readonly (string name, int w, int h)[] Sizes =
        {
            ("1080x2340 (Portrait)", 1080, 2340),
            ("1080x1920 (Portrait)", 1080, 1920),
            ("1080x2400 (Portrait)", 1080, 2400),
            ("1536x2048 (Tablet)", 1536, 2048),
        };

        static GameViewPresets()
        {
            EditorApplication.delayCall += Install;
        }

        /// <summary>Selects a named Game view size (installing the presets first). Returns false if the internal API is unavailable.</summary>
        public static bool Select(string name)
        {
            Install();
            try
            {
                var editorAsm = typeof(Editor).Assembly;
                var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
                var groupType = editorAsm.GetType("UnityEditor.GameViewSizeGroupType");
                var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { CurrentGroup(sizesType, instance, groupType) });
                var gt = group.GetType();
                int idx = IndexOf(group, gt.GetMethod("GetTotalCount"), gt.GetMethod("GetGameViewSize"), sizeType.GetProperty("baseText"), name);
                LastError = idx < 0 ? "no preset named " + name + " in group " + CurrentGroup(sizesType, instance, groupType) : "";
                if (idx < 0) return false;
                bool ok = TrySelect(editorAsm, idx);
                if (!ok) LastError = "TrySelect failed at index " + idx;
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TillWinter] Could not select Game view size: " + e.Message);
                return false;
            }
        }

        private static void Install()
        {
            try
            {
                var editorAsm = typeof(Editor).Assembly;
                var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
                var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
                var sizeKindType = editorAsm.GetType("UnityEditor.GameViewSizeType");
                var groupType = editorAsm.GetType("UnityEditor.GameViewSizeGroupType");
                if (sizesType == null || sizeType == null || sizeKindType == null || groupType == null)
                {
                    Debug.LogWarning("[TillWinter] GameViewSizes internal API not found; add portrait Game view sizes manually.");
                    return;
                }

                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
                if (instance == null || getGroup == null)
                {
                    Debug.LogWarning("[TillWinter] GameViewSizes.instance/GetGroup missing; add portrait Game view sizes manually.");
                    return;
                }

                // The Game view lists the sizes of the active build target's group (Android/iOS here), not Standalone.
                var groupValue = CurrentGroup(sizesType, instance, groupType);
                var group = getGroup.Invoke(instance, new[] { groupValue });
                var gt = group.GetType();
                var getTotalCount = gt.GetMethod("GetTotalCount");
                var getGameViewSize = gt.GetMethod("GetGameViewSize");
                var addCustomSize = gt.GetMethod("AddCustomSize");
                var ctor = sizeType.GetConstructor(new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
                var baseTextProp = sizeType.GetProperty("baseText");
                if (getTotalCount == null || getGameViewSize == null || addCustomSize == null || ctor == null || baseTextProp == null)
                {
                    Debug.LogWarning("[TillWinter] GameViewSizeGroup API changed; add portrait Game view sizes manually.");
                    return;
                }

                var fixedRes = Enum.Parse(sizeKindType, "FixedResolution");
                int preferredIndex = -1;
                foreach (var (name, w, h) in Sizes)
                {
                    int idx = IndexOf(group, getTotalCount, getGameViewSize, baseTextProp, name);
                    if (idx < 0)
                    {
                        addCustomSize.Invoke(group, new[] { ctor.Invoke(new object[] { fixedRes, w, h, name }) });
                        idx = IndexOf(group, getTotalCount, getGameViewSize, baseTextProp, name);
                    }
                    if (name == PreferredName) preferredIndex = idx;
                }

                if (preferredIndex >= 0 && !EditorPrefs.GetBool(FirstRunKey + ":" + Application.dataPath, false))
                {
                    if (TrySelect(editorAsm, preferredIndex))
                        EditorPrefs.SetBool(FirstRunKey + ":" + Application.dataPath, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TillWinter] Could not install Game view presets (internal API changed?): " + e.Message);
            }
        }

        private static object CurrentGroup(Type sizesType, object instance, Type groupType)
        {
            var prop = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var value = prop?.GetValue(prop.GetGetMethod(true).IsStatic ? null : instance, null);
            return value ?? Enum.Parse(groupType, "Standalone");
        }

        private static int IndexOf(object group, MethodInfo getTotalCount, MethodInfo getGameViewSize, PropertyInfo baseText, string name)
        {
            int count = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                var s = getGameViewSize.Invoke(group, new object[] { i });
                if (s != null && string.Equals((string)baseText.GetValue(s), name, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool TrySelect(Assembly editorAsm, int index)
        {
            var gameViewType = editorAsm.GetType("UnityEditor.GameView");
            if (gameViewType == null) return false;
            var windows = Resources.FindObjectsOfTypeAll(gameViewType);
            if (windows == null || windows.Length == 0) return false; // no Game view open yet; retry next load
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (select == null) return false;
            foreach (var w in windows)
                select.Invoke(w, new object[] { index, null });
            return true;
        }
    }
}
