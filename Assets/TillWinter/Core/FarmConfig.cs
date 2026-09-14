namespace TillWinter.Core
{
    /// <summary>Static description of one shop entry.</summary>
    [System.Serializable]
    public sealed class UpgradeDef
    {
        public UpgradeId Id;
        public string Name;
        public string Effect;
        public double BaseCost;
        /// <summary>-1 means "dynamic": <see cref="FarmSim.GetMaxLevel"/> decides (used by UpgradePlot).</summary>
        public int MaxLevel;

        public UpgradeDef(UpgradeId id, string name, string effect, double baseCost, int maxLevel)
        {
            Id = id;
            Name = name;
            Effect = effect;
            BaseCost = baseCost;
            MaxLevel = maxLevel;
        }
    }

    /// <summary>
    /// Every tunable number in the game. Plain C# so Core stays engine-free; the Unity layer may
    /// wrap it in a ScriptableObject for tweaking.
    /// </summary>
    [System.Serializable]
    public sealed class FarmConfig
    {
        // Field
        public int StartGridSize = 3;
        public int MaxGridSize = 5;

        // Crops, indexed by tier
        public string[] CropNames = { "Carrot", "Tomato", "Corn" };
        public float[] RipeTimes = { 3f, 6f, 10f };
        public double[] CropValues = { 1, 4, 12 };
        public int MaxTier => RipeTimes.Length - 1;

        // Growth
        public float SoilPerLevel = 0.25f;
        public float IrrigationPerLevel = 0.15f;
        public float BaseRingRadius = 1.5f;
        public float RingRadiusPerLevel = 0.5f;
        public float MaxRingRadius = 3f;

        // Year
        public float BaseYearLength = 90f;
        public float CalendarPerLevel = 15f;
        public float MaxYearLength = 150f;
        public float FrostWarningSeconds = 10f;

        // Crows
        public int CrowFirstYear = 2;
        public float CrowSpawnInterval = 4f;
        public float CrowSpawnChance = 0.25f;
        public int MaxCrows = 2;
        public float CrowEatTime = 4f;

        // Apprentice
        public float ApprenticeBaseSpeed = 1.5f;
        public float ApprenticeSpeedPerLevel = 0.5f;
        public float ApprenticeHarvestTime = 0.5f;

        // Shop
        public double CostGrowth = 1.6;
        public UpgradeDef[] Upgrades =
        {
            new UpgradeDef(UpgradeId.ExpandField, "Expand Field", "Adds a ring of plots (3x3 to 4x4 to 5x5)", 60, 2),
            new UpgradeDef(UpgradeId.UpgradePlot, "Upgrade Plot", "Raises the lowest-tier plot by one crop tier", 15, -1),
            new UpgradeDef(UpgradeId.Soil, "Soil", "+25% growth speed", 25, 5),
            new UpgradeDef(UpgradeId.Irrigation, "Irrigation", "Crops grow outside the ring (+15% of ring speed)", 40, 3),
            new UpgradeDef(UpgradeId.RingRadius, "Ring Radius", "+0.5 plots ring radius", 30, 3),
            new UpgradeDef(UpgradeId.Calendar, "Calendar", "+15 s year length", 35, 4),
            new UpgradeDef(UpgradeId.Apprentice, "Apprentice", "Helper harvests ripe plots (then +speed)", 80, 3),
            new UpgradeDef(UpgradeId.Scarecrow, "Scarecrow", "No crows", 50, 1),
        };

        public UpgradeDef GetUpgrade(UpgradeId id)
        {
            foreach (var u in Upgrades)
                if (u.Id == id) return u;
            return null;
        }
    }
}
