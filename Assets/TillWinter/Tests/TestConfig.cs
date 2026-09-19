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
            // Seasonal preferences (M.2) off: rule timings and values stay what they were written against.
            cfg.InSeasonValue = 1;
            // Field variety (M.2) off too: no neighbour, rotation or ground bonuses, and expansions add plain ground.
            cfg.NeighbourVarietyBonus = 0;
            cfg.RotationBonus = 1;
            cfg.FertileValue = 1;
            cfg.FertileChance = 0;
            cfg.StonyChance = 0;
            return cfg;
        }
    }
}
