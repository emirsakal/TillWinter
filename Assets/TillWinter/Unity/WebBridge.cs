#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The few things the web build (GitHub Pages) does differently, behind one door: the clipboard, the farm-code
    /// prompt, a short buzz, and flushing the save to the browser's storage. Every other platform takes the old path,
    /// so the phone builds and the editor are untouched. The browser side is Assets/Plugins/WebGL/TillWinterWeb.jslib.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TW_CopyText(string text);
        [DllImport("__Internal")] private static extern string TW_PromptText(string message);
        [DllImport("__Internal")] private static extern void TW_ShowText(string title, string text);
        [DllImport("__Internal")] private static extern void TW_Vibrate(int ms);
        [DllImport("__Internal")] private static extern void TW_SyncFiles();
        public static bool IsWeb => true;
#else
        public static bool IsWeb => false;
#endif

        /// <summary>Puts text on the clipboard. On the web, a browser that refuses gets the text shown to copy by hand.</summary>
        public static void Copy(string text, string showTitle)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (TW_CopyText(text) == 0) TW_ShowText(showTitle, text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        /// <summary>
        /// A farm code from the player. Phones and the editor read the clipboard; a browser asks for it, since reading
        /// the clipboard silently needs a permission most browsers refuse. Null when nothing was given.
        /// </summary>
        public static string ReadCode(string prompt)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string text = TW_PromptText(prompt);
            return string.IsNullOrEmpty(text) ? null : text;
#else
            return GUIUtility.systemCopyBuffer;
#endif
        }

        /// <summary>A short buzz in the browser where it has one (Android Chrome).</summary>
        public static void Vibrate(int ms)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TW_Vibrate(ms);
#endif
        }

        /// <summary>After a file write: the browser keeps files in memory until they are flushed to its storage.</summary>
        public static void SyncFiles()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TW_SyncFiles();
#endif
        }
    }
}
