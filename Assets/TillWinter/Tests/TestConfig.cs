using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Rule tests pin the pre-balance numbers (carrot 1 coin, retire at 5 000 coins, seed divisor 50) so that a
    /// tuning pass never changes what a rule test expects. Balance targets live in <see cref="BalanceTests"/>.
    /// </summary>
    public static class TestConfig
    {
        public static FarmConfig Classic()
        {
            var cfg = new FarmConfig();
            cfg.Crops[0].Value = 1;
            cfg.HeritageThreshold = 5000;
            cfg.SeedDivisor = 50;
            return cfg;
        }
    }
}
