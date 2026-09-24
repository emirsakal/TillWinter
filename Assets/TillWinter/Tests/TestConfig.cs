using TillWinter.Core;

namespace TillWinter.Tests
{
    /// <summary>
    /// Rule tests pin the pre-balance numbers (carrot 1 coin, retire at 5 000 coins, seed divisor 50, no depth bonus,
    /// no specials in the ground) so that a tuning pass never changes what a rule test expects. Balance targets live
    /// in <see cref="BalanceTests"/>.
    /// </summary>
    public static class TestConfig
    {
        public static FarmConfig Classic()
        {
            var cfg = new FarmConfig();
            cfg.Crops[0].Value = 1;
            cfg.HeritageThreshold = 5000;
            cfg.SeedDivisor = 50;
            // The ground (v3) without its surprises: no chests, no hardpan, no depth bonus, no season softness; a rule
            // test that needs one turns it on.
            cfg.ChestChance = 0;
            cfg.HardpanChance = 0;
            cfg.DepthValue = 0;
            // The prototype's ground numbers (GDD §2v3.3 v3.0): the rules were written against them.
            cfg.BaseHp = 10;
            cfg.HpGrowth = 1.35;
            cfg.BreakCoins = 2;
            cfg.BreakGrowth = 1.4;
            cfg.TiredDamage = 0.4;
            cfg.MaxLayer = 40;
            cfg.SpringSoftness = 1f;
            cfg.SummerHardness = 1f;
            cfg.SummerGrowth = 1f;
            cfg.AutumnCombo = 1f;
            // Seasonal preferences (M.2) off: rule timings and values stay what they were written against.
            cfg.InSeasonValue = 1;
            // Field variety (M.2) off too: no neighbour, rotation or ground bonuses, and expansions add plain ground.
            cfg.NeighbourVarietyBonus = 0;
            cfg.RotationBonus = 1;
            cfg.FertileValue = 1;
            cfg.FertileChance = 0;
            // Grade, goals and weather (M.3) off as well: no bonus, no RNG drawn for them.
            cfg.FrostRushValue = 1;
            cfg.GradeBonusByStars = new double[] { 0, 0, 0, 0 };
            cfg.GoalFirstYear = int.MaxValue;
            cfg.WeatherFirstYear = int.MaxValue;
            // Events and threats (M.5) off: no pests, luck or trader, and no RNG drawn for them.
            cfg.PestFirstYear = int.MaxValue;
            cfg.LuckyFirstYear = int.MaxValue;
            cfg.TraderFirstYear = int.MaxValue;
            // Heirs and heirlooms (M.7) off: a rebirth draws no heir and achievements add no bonus.
            cfg.HeirsEnabled = false;
            cfg.HeirloomsEnabled = false;
            return cfg;
        }
    }
}
