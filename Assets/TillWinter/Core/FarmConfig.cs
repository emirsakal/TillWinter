namespace TillWinter.Core
{
    /// <summary>One crop tier. Times are seconds under the ring at level 0 (GDD §2.3).</summary>
    [System.Serializable]
    public sealed class CropDef
    {
        /// <summary>Localization key, e.g. "crop.carrot". Core holds no user-facing strings.</summary>
        public string Key;
        public float Water;
        public float Grow;
        public float Harvest;
        public double Value;

        public CropDef(string key, float water, float grow, float harvest, double value)
        {
            Key = key;
            Water = water;
            Grow = grow;
            Harvest = harvest;
            Value = value;
        }
    }

    /// <summary>
    /// Every tunable number that is not an Almanac node value. Plain C# so Core stays engine-free;
    /// the Unity layer may wrap it in a ScriptableObject for tweaking. Per-level node values live in
    /// <see cref="AlmanacData"/>.
    /// </summary>
    [System.Serializable]
    public sealed class FarmConfig
    {
        // Field (GDD §2.4)
        public int StartGridSize = 3;
        public int MaxGridSize = 6;

        // Crops (GDD §2.3), indexed by tier
        public CropDef[] Crops =
        {
            new CropDef("crop.carrot", 1.0f, 1.5f, 0.5f, 1),
            new CropDef("crop.tomato", 1.5f, 3.5f, 0.5f, 4),
            new CropDef("crop.corn", 2.0f, 6.0f, 0.7f, 12),
            new CropDef("crop.pumpkin", 3.0f, 10f, 1.0f, 35),
            new CropDef("crop.grapes", 4.0f, 15f, 1.0f, 100),
            new CropDef("crop.golden_wheat", 5.0f, 22f, 1.2f, 300),
        };
        public int MaxTier => Crops.Length - 1;

        // Ring (GDD §2.1)
        public float BaseRingRadius = 0.7f;
        public float MaxRingRadius = 2.5f;

        // Year (GDD §3)
        public float BaseYearLength = 90f;
        public float MaxYearLength = 180f;
        public float BaseFrostWarningSeconds = 10f;

        // Crows (GDD §5.1)
        public int CrowFirstYear = 2;
        public float CrowSpawnInterval = 4f;
        /// <summary>Indexed by scarecrow level. Never 0 in the Almanac.</summary>
        public float[] CrowSpawnChanceByScarecrow = { 0.25f, 0.15f, 0.08f };
        public int MaxCrows = 2;
        public float CrowEatTime = 4f;
        /// <summary>A tap-scared crow drops this × crop value.</summary>
        public double CrowScareValueMultiplier = 2;

        // Apprentices (GDD §4)
        public float ApprenticeBaseSpeed = 1.5f;
        public float ApprenticeBaseHarvestTime = 1.0f;
        public float ApprenticeMinHarvestTime = 0.4f;
        /// <summary>Indexed by apprentice_yield level.</summary>
        public double[] ApprenticeYieldByLevel = { 0.5, 0.75, 1.0, 1.15, 1.3 };
        /// <summary>How far below the field's bottom row apprentices idle (plot units).</summary>
        public float ApprenticeIdleOffset = 1.2f;

        // Heritage (GDD §7)
        /// <summary>Lifetime coins in this generation needed before "Pass on the farm" unlocks.</summary>
        public double HeritageThreshold = 5000;
        /// <summary>Seeds = floor(sqrt(lifetimeCoinsThisGeneration / SeedDivisor)); 5 000 coins ≈ 10 seeds.</summary>
        public double SeedDivisor = 50;

        // Offline (GDD §9)
        public double OfflineCapSeconds = 8 * 3600;
        public float OfflineStepSeconds = 1f;
    }
}
