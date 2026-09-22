namespace TillWinter.Core.Dig
{
    /// <summary>One crop of the prototype: a growth time and a value. Water and grow are one number here (GDD §2v3.6).</summary>
    public sealed class DigCrop
    {
        public string Key;
        public float Grow;
        public double Value;

        public DigCrop(string key, float grow, double value)
        {
            Key = key;
            Grow = grow;
            Value = value;
        }
    }

    /// <summary>
    /// Every number of the core-loop v3 prototype (GDD §2v3), with the starting values the section quotes. They exist
    /// to be moved by the bot tables, not by feel; nothing in <see cref="DigSim"/> carries a literal.
    /// </summary>
    public sealed class DigConfig
    {
        // Field and clock
        public int GridSize = 3;
        public float YearLength = 90f;

        // Ground (§2v3.3)
        public double BaseHp = 10;
        public double HpGrowth = 1.35;
        public double BreakCoins = 2;
        public double BreakGrowth = 1.4;
        public double ChestChance = 0.04;
        /// <summary>A chest pays this many times the layer's break bonus (the prototype folds the two-reward choice into coins).</summary>
        public double ChestMult = 7.5;
        public double GoldChance = 0.02;
        public double GoldHpMult = 3;
        public double GoldCoinsMult = 8;
        /// <summary>Damage multipliers by season: spring rain softens the ground, summer drought hardens it.</summary>
        public float SpringSoftness = 1.25f, SummerHardness = 0.83f;
        public float SummerGrowth = 0.8f;
        public float AutumnCombo = 1.5f;

        // Striking (§2v3.4)
        public double Damage = 3;
        public float PulseSeconds = 1f;
        public float CritWindow = 0.25f;
        public double CritMult = 2.5;
        public float StrikeCooldown = 0.35f;

        // Stamina (§2v3.5)
        public float StaminaMax = 100f;
        public float StrikeCost = 5f;
        public float WaterCostPerSecond = 8f;
        public float StaminaRegen = 1.5f;
        public float ReapStamina = 3f;

        // Seeds and growth (§2v3.6–7)
        public float WaterBoost = 3f;
        public double DepthValue = 0.5;
        public double ComboPerCrop = 0.1;
        public int MaxTier = 0;
        public DigCrop[] Crops =
        {
            new DigCrop("crop.carrot", 2.5f, 2.0),
            new DigCrop("crop.tomato", 5f, 4),
            new DigCrop("crop.corn", 8f, 12),
            new DigCrop("crop.pumpkin", 13f, 35),
            new DigCrop("crop.grapes", 19f, 100),
            new DigCrop("crop.golden_wheat", 27f, 300),
        };

        // Crows (§2v3.8)
        public float CrowMinRipe = 3f;
        /// <summary>Chance per second that a crow lands on a ripe crop that has stood at least <see cref="CrowMinRipe"/>.</summary>
        public float CrowChancePerSecond = 0.08f;
        public float CrowWait = 4f;
        public int MaxCrows = 2;

        // Winter upgrades the bots buy the same way, so the tables compare play, not shopping (§2v3.13)
        public double UpgradeDamage = 1, UpgradeDamageCost = 20, UpgradeDamageGrowth = 1.6;
        public float UpgradeCritWindow = 0.03f; public double UpgradeCritCost = 30, UpgradeCritGrowth = 1.7;
        public float UpgradeStamina = 15f; public double UpgradeStaminaCost = 25, UpgradeStaminaGrowth = 1.6;
        public float UpgradeRegen = 0.25f; public double UpgradeRegenCost = 30, UpgradeRegenGrowth = 1.7;
        public float UpgradeGrowth = 0.1f; public double UpgradeGrowthCost = 20, UpgradeGrowthGrowth = 1.6;
        /// <summary>The crop tier after this one costs this much, growing by the same factor per tier.</summary>
        public double UpgradeTierCost = 60, UpgradeTierGrowth = 2.2;
        public float MaxCritWindow = 0.4f; // past this, timing stops being a skill: a random tap crits half the time
    }
}
