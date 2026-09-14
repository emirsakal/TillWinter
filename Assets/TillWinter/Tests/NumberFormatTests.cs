using NUnit.Framework;
using TillWinter.Core;

namespace TillWinter.Tests
{
    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(7.9, "7")]
        [TestCase(950, "950")]
        [TestCase(999, "999")]
        [TestCase(999.9, "999")]
        [TestCase(1000, "1K")]
        [TestCase(1200, "1.2K")]
        [TestCase(1250, "1.2K")]
        [TestCase(9999, "9.9K")]
        [TestCase(15400, "15K")]
        [TestCase(999999, "999K")]
        [TestCase(1000000, "1M")]
        [TestCase(3400000, "3.4M")]
        [TestCase(5600000000, "5.6B")]
        [TestCase(1.2e12, "1.2T")]
        [TestCase(-1500, "-1.5K")]
        public void Short_FormatsAsExpected(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }
    }
}
