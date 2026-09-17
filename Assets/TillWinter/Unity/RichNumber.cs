using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// A short number with its unit suffix set smaller and in colour ("3,6<size=64%>K</size>"), so the digits carry
    /// the weight and the suffix still reads at a glance. Works on char buffers: the tags are built once, and each
    /// write only copies characters, so a counter can use it every frame without allocating. The label needs
    /// rich text on.
    /// </summary>
    public sealed class RichNumber
    {
        private readonly char[] _open;
        private readonly char[] _close;
        public readonly char[] Buffer = new char[96];

        public RichNumber(float suffixScale, Color suffixColor)
        {
            int pct = Mathf.RoundToInt(suffixScale * 100f);
            _open = ("<size=" + pct + "%><color=#" + ColorUtility.ToHtmlStringRGB(suffixColor) + ">").ToCharArray();
            _close = "</color></size>".ToCharArray();
        }

        /// <summary>Copies <paramref name="length"/> chars of a formatted number into <see cref="Buffer"/>, wrapping its trailing letters. Returns the new length.</summary>
        public int Write(char[] source, int length)
        {
            int digitsEnd = length;
            while (digitsEnd > 0 && char.IsLetter(source[digitsEnd - 1])) digitsEnd--;
            int n = 0;
            for (int i = 0; i < digitsEnd; i++) Buffer[n++] = source[i];
            if (digitsEnd == length) return n;
            for (int i = 0; i < _open.Length; i++) Buffer[n++] = _open[i];
            for (int i = digitsEnd; i < length; i++) Buffer[n++] = source[i];
            for (int i = 0; i < _close.Length; i++) Buffer[n++] = _close[i];
            return n;
        }
    }
}
