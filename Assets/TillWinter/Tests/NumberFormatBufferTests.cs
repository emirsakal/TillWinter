using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>The allocation-free Short(value, buffer) used by per-frame UI must print exactly what Short(value) prints.</summary>
    public class NumberFormatBufferTests
    {
        private static readonly double[] Values =
        {
            0, 0.5, 7, 42, 950, 999, 999.99, 1000, 1049, 1050, 1234, 9999, 9999.99, 10000, 15400, 99999, 100000, 999999,
            1e6, 1.25e6, 3.4e6, 9.99e6, 5.6e9, 1.05e12, 7.77e15, 3.2e18, 4.4e21, -1, -950, -1234, -5.6e9,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity,
        };

        [Test]
        public void BufferVariant_MatchesStringVariant()
        {
            var buffer = new char[32];
            foreach (double v in Values)
            {
                int n = NumberFormat.Short(v, buffer);
                Assert.AreEqual(NumberFormat.Short(v), new string(buffer, 0, n), "value " + v);
            }
        }

        [Test]
        public void BufferVariant_MatchesAcrossARangeOfIncomes()
        {
            var buffer = new char[32];
            for (double v = 1; v < 1e13; v *= 1.37)
            {
                int n = NumberFormat.Short(v, buffer);
                Assert.AreEqual(NumberFormat.Short(v), new string(buffer, 0, n), "value " + v);
            }
        }
    }
}
