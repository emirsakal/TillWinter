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
        /// <summary>The season this crop likes: it sells for more then (GDD §2.3 v1.7).</summary>
        public Season Likes;

        public CropDef(string key, float water, float grow, float harvest, double value, Season likes = Season.Spring)
        {
            Key = key;
            Water = water;
            Grow = grow;
            Harvest = harvest;
            Value = value;
            Likes = likes;
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
            new CropDef("crop.carrot", 1.0f, 1.5f, 0.5f, 2.2, Season.Spring), // S9 balance: year 1 ≈ 67 coins (M.2: 2.3 → 2.2 for the spring bonus)
            new CropDef("crop.tomato", 1.5f, 3.5f, 0.5f, 4, Season.Summer),
            new CropDef("crop.corn", 2.0f, 6.0f, 0.7f, 12, Season.Summer),
            new CropDef("crop.pumpkin", 3.0f, 10f, 1.0f, 35, Season.Autumn),
            new CropDef("crop.grapes", 4.0f, 15f, 1.0f, 100, Season.Autumn),
            new CropDef("crop.golden_wheat", 5.0f, 22f, 1.2f, 300, Season.Spring),
        };
        public int MaxTier => Crops.Length - 1;
        /// <summary>A crop in its liked season sells for this much more (GDD §2.3 v1.7). Timings never change with the season.</summary>
        public double InSeasonValue = 1.25;

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
        public double HeritageThreshold = 3000; // S9 balance (GDD §7 v1.4)
        /// <summary>Seeds = floor(sqrt(lifetimeCoinsThisGeneration / SeedDivisor)); 3 000 coins = 10 seeds.</summary>
        public double SeedDivisor = 30;

        // Events and remaining nodes (GDD §4, §5, §6, §7)
        public float CloudDriftSeconds = 8f;
        public float CloudWetBoost = 0.25f;
        public double GoldenValueMultiplier = 10;
        /// <summary>Indexed by tractor level (0 unused).</summary>
        public float[] TractorIntervalByLevel = { 0f, 30f, 20f, 12f };
        public float TractorSecondsPerPlot = 0.15f;
        public double GreenhouseRatePerLevel = 0.02;
        public float GreenhouseWinterCapSeconds = 60f;
        // --- M.1 (GDD §2.5 v1.6): ripe crops do not wait forever, the combo pays out, a moving ring works faster.
        /// <summary>A Ripe plot keeps full value for this long; after it the value falls off.</summary>
        public float RipeGraceSeconds = 12f;
        /// <summary>Seconds from the end of the grace to the lowest value.</summary>
        public float OverripeDecaySeconds = 24f;
        /// <summary>A crop never falls below this share of its value: waiting costs, it never wastes the crop.</summary>
        public double OverripeMinValue = 0.5;
        /// <summary>Combo lengths that pay a bonus.</summary>
        public int[] ComboMilestones = { 10, 25, 50 };
        /// <summary>Bonus at each milestone, in crop values of the harvest that reached it.</summary>
        public double[] ComboMilestoneBonus = { 3, 8, 20 };
        /// <summary>A ring that keeps moving works this much faster (GDD §2.1 v1.6).</summary>
        public float FlowBonus = 0.15f;
        /// <summary>Plots per second the ring must move to count as flowing.</summary>
        public float FlowSpeedThreshold = 1.2f;
        /// <summary>Tap-harvest cooldown per `tap_harvest` level (index 0 = not owned).</summary>
        public float[] TapHarvestCooldownByLevel = { 0f, 6f, 3f };
        /// <summary>Rake shape: half-length and half-width, in ring radii.</summary>
        public float RakeLength = 1.7f, RakeWidth = 0.5f;
        /// <summary>Cross shape: arm half-length and half-width, in ring radii.</summary>
        public float CrossLength = 1.5f, CrossWidth = 0.42f;
        public float ComboWindowSeconds = 1.0f;
        public int ComboMaxStacks = 10;
        public float LateFrostThreshold = 0.8f;
        public double LateFrostValueMultiplier = 0.5;

        // Offline (GDD §9)
        public double OfflineCapSeconds = 8 * 3600;
        public float OfflineStepSeconds = 1f;
        /// <summary>Absences shorter than this (a call, an app switch) resume exactly where the player left: no offline simulation.</summary>
        public double OfflineMinSeconds = 60;

        // Ending (GDD §8)
        /// <summary>Length of the Golden Year: the one year after every Heritage node is maxed.</summary>
        public float GoldenYearSeconds = 300f;
    }
}
