using System;
using System.Globalization;

namespace TillWinter.Core
{
    public static class NumberFormat
    {
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
                text = t.ToString("0.#", CultureInfo.InvariantCulture);
            }
            else
            {
                text = Math.Floor(value + 1e-9).ToString("0", CultureInfo.InvariantCulture);
            }
            return (negative ? "-" : "") + text + Suffixes[tier];
        }
    }
}
