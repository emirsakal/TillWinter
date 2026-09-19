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
            // Seasons, grade, goals and weather (M.3) off as well: no season rules, no bonus, no RNG drawn for them.
            cfg.SpringWaterBoost = 1f;
            cfg.SummerDryOutSeconds = float.MaxValue;
            cfg.AutumnComboWindow = 1f;
            cfg.FrostRushValue = 1;
            cfg.GradeBonusByStars = new double[] { 0, 0, 0, 0 };
            cfg.GoalFirstYear = int.MaxValue;
            cfg.WeatherFirstYear = int.MaxValue;
            // Events and threats (M.5) off: no pests, luck or trader, and no RNG drawn for them.
            cfg.PestFirstYear = int.MaxValue;
            cfg.LuckyFirstYear = int.MaxValue;
            cfg.TraderFirstYear = int.MaxValue;
            return cfg;
        }
    }
}
