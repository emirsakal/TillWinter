using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Picks a device in the editor's Device Simulator window through its internal API (reflection; no public API
    /// exists). The UI tour uses it to shoot other screen shapes: the Simulator ignores Game view size presets.
    /// Every call fails soft and returns a message instead of throwing when the internals change.
    /// </summary>
    internal static class SimulatorDevices
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static object Main(out string error)
        {
            error = "";
            var type = FindType("UnityEditor.DeviceSimulation.SimulatorWindow");
            if (type == null) { error = "no SimulatorWindow type"; return null; }
            var windows = Resources.FindObjectsOfTypeAll(type);
            if (windows == null || windows.Length == 0) { error = "no Simulator window open"; return null; }
            var main = type.GetField("m_Main", All)?.GetValue(windows[0]);
            if (main == null) error = "Simulator window has no m_Main";
            return main;
        }

        /// <summary>The Simulator lives in its own editor module assembly, not in UnityEditor.dll.</summary>
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        public static int CurrentIndex()
        {
            var main = Main(out _);
            var prop = main?.GetType().GetProperty("deviceIndex", All);
            return prop != null ? (int)prop.GetValue(main, null) : -1;
        }

        public static string SetIndex(int index)
        {
            var main = Main(out string error);
            if (main == null) return error;
            var prop = main.GetType().GetProperty("deviceIndex", All);
            if (prop == null || !prop.CanWrite) return "deviceIndex not writable";
            prop.SetValue(main, index, null);
            return "";
        }

        /// <summary>Name and screen of the current device, for the tour report.</summary>
        public static string CurrentName()
        {
            var main = Main(out string error);
            if (main == null) return error;
            var device = main.GetType().GetProperty("currentDevice", All)?.GetValue(main, null);
            return Describe(device, out string name, out int w, out int h) ? name + " " + w + "x" + h : "unknown";
        }

        /// <summary>Selects the device whose first screen is closest in shape (then size) to <paramref name="width"/> x <paramref name="height"/>.</summary>
        public static string SelectClosest(int width, int height, out string chosen)
        {
            chosen = "";
            var main = Main(out string error);
            if (main == null) return error;
            var devices = main.GetType().GetProperty("devices", All)?.GetValue(main, null) as IList;
            if (devices == null || devices.Count == 0) return "no devices listed";
            float want = (float)width / height;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < devices.Count; i++)
            {
                if (!Describe(devices[i], out string name, out int w, out int h)) continue;
                if (w > h) { int t = w; w = h; h = t; } // compare in portrait
                float score = Mathf.Abs((float)w / h - want) * 10f + Mathf.Abs(w - width) / 10000f;
                if (score < bestScore) { bestScore = score; best = i; chosen = name + " " + w + "x" + h; }
            }
            if (best < 0) return "no device could be read";
            return SetIndex(best);
        }

        private static bool Describe(object asset, out string name, out int w, out int h)
        {
            name = "";
            w = h = 0;
            var info = asset?.GetType().GetField("deviceInfo", All)?.GetValue(asset);
            if (info == null) return false;
            name = info.GetType().GetField("friendlyName", All)?.GetValue(info) as string ?? "";
            var screens = info.GetType().GetField("screens", All)?.GetValue(info) as IList;
            if (screens == null || screens.Count == 0) return false;
            var screen = screens[0];
            var st = screen.GetType();
            w = Convert.ToInt32(st.GetField("width", All)?.GetValue(screen) ?? 0);
            h = Convert.ToInt32(st.GetField("height", All)?.GetValue(screen) ?? 0);
            return w > 0 && h > 0;
        }
    }
}
