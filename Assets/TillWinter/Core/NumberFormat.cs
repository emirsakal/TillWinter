using System;
using System.Globalization;

namespace TillWinter.Core
{
    public enum NumberStyle { English, Turkish }

    public static class NumberFormat
    {
        /// <summary>Set by the presentation layer from the language: Turkish uses a decimal comma (1,2K) and a leading percent sign (%10).</summary>
        public static NumberStyle Style { get; set; } = NumberStyle.English;
        public static char DecimalSeparator => Style == NumberStyle.Turkish ? ',' : '.';

        /// <summary>A whole-number percentage in the current style: 10% or %10.</summary>
        public static string Percent(double fraction)
        {
            string n = Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture);
            return Style == NumberStyle.Turkish ? "%" + n : n + "%";
        }

        /// <summary>A decimal in the current style (invariant digits, style separator).</summary>
        public static string Decimal(double value, string format)
        {
            string s = value.ToString(format, CultureInfo.InvariantCulture);
            return Style == NumberStyle.Turkish ? s.Replace('.', ',') : s;
        }

        /// <summary>A whole number with the style's thousands separator: 1,234 in English, 1.234 in Turkish.</summary>
        public static string Whole(long value)
        {
            string s = Math.Abs(value).ToString("#,0", CultureInfo.InvariantCulture);
            if (Style == NumberStyle.Turkish) s = s.Replace(',', '.');
            return value < 0 ? "-" + s : s;
        }

        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

        /// <summary>950 -> "950", 1234 -> "1.2K", 15400 -> "15K", 3.4e6 -> "3.4M", 5.6e9 -> "5.6B".</summary>
        public static string Short(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            bool negative = value < 0;
            value = Math.Abs(value);
            if (value < 1000)
                return (negative ? "-" : "") + Math.Floor(value).ToString(CultureInfo.InvariantCulture);

            int tier = 0;
            while (value >= 1000 && tier < Suffixes.Length - 1)
            {
                value /= 1000;
                tier++;
            }

            // Truncate, never round up: 999.99K must not read as 1000K.
            string text;
            if (value < 10)
            {
                double t = Math.Floor(value * 10 + 1e-9) / 10;
                text = Decimal(t, "0.#");
            }
            else
            {
                text = Math.Floor(value + 1e-9).ToString("0", CultureInfo.InvariantCulture);
            }
            return (negative ? "-" : "") + text + Suffixes[tier];
        }

        /// <summary>
        /// Allocation-free twin of <see cref="Short(double)"/> for per-frame UI: writes the same text into
        /// <paramref name="buffer"/> (at least 24 chars) and returns its length. NumberFormatBufferTests keeps both identical.
        /// </summary>
        public static int Short(double value, char[] buffer)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) { buffer[0] = '0'; return 1; }
            int n = 0;
            if (value < 0) { buffer[n++] = '-'; value = -value; }
            if (value < 1000) return WriteInt(buffer, n, (long)Math.Floor(value));
            int tier = 0;
            while (value >= 1000 && tier < Suffixes.Length - 1)
            {
                value /= 1000;
                tier++;
            }
            if (value < 10)
            {
                long tenths = (long)Math.Floor(value * 10 + 1e-9);
                n = WriteInt(buffer, n, tenths / 10);
                if (tenths % 10 != 0) { buffer[n++] = DecimalSeparator; buffer[n++] = (char)('0' + tenths % 10); }
            }
            else n = WriteInt(buffer, n, (long)Math.Floor(value + 1e-9));
            string suffix = Suffixes[tier];
            for (int i = 0; i < suffix.Length; i++) buffer[n++] = suffix[i];
            return n;
        }

        private static int WriteInt(char[] buffer, int n, long v)
        {
            if (v == 0) { buffer[n++] = '0'; return n; }
            int start = n;
            while (v > 0) { buffer[n++] = (char)('0' + v % 10); v /= 10; }
            // Swap in place: Mono's non-generic Array.Reverse(Array, int, int) can box elements on a char[].
            for (int i = start, j = n - 1; i < j; i++, j--)
            {
                char tmp = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = tmp;
            }
            return n;
        }
    }
}
