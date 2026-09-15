using NUnit.Framework;
using TillWinter.Core.Feel;

namespace TillWinter.Tests
{
    public class RateLimiterTests
    {
        [Test]
        public void AllowsUpToTheCap_ThenMergesInsteadOfDropping()
        {
            var l = new RateLimiter(3);
            int fired = 0;
            for (int i = 0; i < 10; i++) if (l.Request(0.0, out _)) fired++;
            Assert.AreEqual(3, fired);
            Assert.AreEqual(7, l.Pending, "over-budget requests are kept, not dropped");
        }

        [Test]
        public void MergedBacklog_FiresStronger_WhenBudgetReturns()
        {
            var l = new RateLimiter(2, 0.25f, 3f);
            l.Request(0.0, out _);
            l.Request(0.0, out _);
            for (int i = 0; i < 4; i++) Assert.IsFalse(l.Request(0.0, out _));
            Assert.IsFalse(l.Poll(0.1, out _), "no budget yet");
            Assert.IsTrue(l.Poll(0.6, out float intensity), "half a second refills one token");
            Assert.AreEqual(1f + 3 * 0.25f, intensity, 1e-4f, "four merged requests -> one trigger with the backlog folded in");
            Assert.AreEqual(0, l.Pending);
        }

        [Test]
        public void IntensityIsCapped()
        {
            var l = new RateLimiter(1, 0.5f, 2f);
            l.Request(0.0, out _);
            for (int i = 0; i < 50; i++) l.Request(0.0, out _);
            Assert.IsTrue(l.Request(2.0, out float intensity));
            Assert.AreEqual(2f, intensity, 1e-4f);
        }

        [Test]
        public void BudgetResetsPerSecond()
        {
            var l = new RateLimiter(12);
            for (int i = 0; i < 12; i++) Assert.IsTrue(l.Request(0.0, out _));
            Assert.IsFalse(l.Request(0.0, out _));
            int fired = 0;
            for (int i = 0; i < 12; i++) if (l.Request(1.0, out _)) fired++;
            Assert.AreEqual(12, fired, "a full second later the whole budget is back");
        }

        [Test]
        public void FirstTriggerIsNeverDelayed_AndBackwardsClockIsSafe()
        {
            var l = new RateLimiter(5);
            Assert.IsTrue(l.Request(100.0, out float first));
            Assert.AreEqual(1f, first);
            Assert.IsTrue(l.Request(50.0, out _), "clock going backwards does not throw or refill");
        }

        [Test]
        public void Reset_ClearsBacklogAndCounts()
        {
            var l = new RateLimiter(1);
            l.Request(0.0, out _);
            l.Request(0.0, out _);
            l.Reset();
            Assert.AreEqual(0, l.Pending);
            Assert.AreEqual(0, l.Fired);
            Assert.IsTrue(l.Request(0.0, out _));
        }
    }
}
