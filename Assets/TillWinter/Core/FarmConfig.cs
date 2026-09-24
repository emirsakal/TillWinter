namespace TillWinter.Core
{
    /// <summary>One crop tier. <see cref="Grow"/> is seconds from the seed dropping to ripe, unwatered, at level 0 (GDD §2v3.6).</summary>
    [System.Serializable]
    public sealed class CropDef
    {
        /// <summary>Localization key, e.g. "crop.carrot". Core holds no user-facing strings.</summary>
        public string Key;
        public float Grow;
        public double Value;
        /// <summary>The season this crop likes: it sells for more then (GDD §2.3 v1.7).</summary>
        public Season Likes;

        public CropDef(string key, float grow, double value, Season likes = Season.Spring)
        {
            Key = key;
            Grow = grow;
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

        // Crops (GDD §2v3.6), indexed by tier
        public CropDef[] Crops =
        {
            new CropDef("crop.carrot", 2.5f, 2.0, Season.Spring),
            new CropDef("crop.tomato", 5f, 4, Season.Summer),
            new CropDef("crop.corn", 8f, 12, Season.Summer),
            new CropDef("crop.pumpkin", 13f, 35, Season.Autumn),
            new CropDef("crop.grapes", 19f, 100, Season.Autumn),
            new CropDef("crop.golden_wheat", 27f, 300, Season.Spring),
        };
        public int MaxTier => Crops.Length - 1;
        /// <summary>A crop in its liked season sells for this much more (GDD §2.3 v1.7). Timings never change with the season.</summary>
        public double InSeasonValue = 1.25;
        /// <summary>A crop's value grows with the layer it was dug from: value × (1 + DepthValue × layer) (GDD §2v3.6).</summary>
        public double DepthValue = 0.5;

        // Ground (GDD §2v3.3)
        public double BaseHp = 10;
        public double HpGrowth = 1.22; // v3.3 balance: 1.35 outran the hoe within a generation (income fell year on year)
        public double BreakCoins = 1;
        public double BreakGrowth = 1.28; // a hair above the HP growth: a deeper layer pays a little more per hit
        public double ChestChance = 0.04;
        /// <summary>A chest pays this many times the layer's break bonus (the two-reward choice is a later phase).</summary>
        public double ChestMult = 7.5;
        public double HardpanChance = 0.02;
        public double HardpanHpMult = 3;
        public double HardpanCoinsMult = 8;
        /// <summary>Layer index at which each ground type begins (clay before the first entry).</summary>
        public int[] GroundLayers = { 2, 4, 6, 8 };
        /// <summary>
        /// Bedrock: the deepest layer a plot reaches this generation. Past it a reap brings the same layer back, so a
        /// field can never outgrow the hoe (unbounded HP growth locked a long generation's field solid; v3.3).
        /// </summary>
        public int MaxLayer = 12;

        // Striking and the beat (GDD §2v3.4)
        public double BaseStrikeDamage = 3;
        /// <summary>The field's beat: 100 bpm. A player on the beat swings ~1.5 times a second.</summary>
        public float PulseSeconds = 0.6f;
        public float BaseCritWindow = 0.3f;
        public float MaxCritWindow = 0.4f; // past this, timing stops being a skill: a random tap crits half the time
        public double CritMult = 2.5;
        public float BaseStrikeCooldown = 0.35f;
        public float MinStrikeCooldown = 0.15f;
        /// <summary>Hybrid crit: on the beat it is certain; off the beat this chance still applies.</summary>
        public double BaseCritChance = 0.10;
        public double MaxCritChance = 0.5;
        /// <summary>A strike with no stamina still lands, at this share of the damage and never a crit: the finger never waits.</summary>
        public double TiredDamage = 0.3;

        // Stamina (GDD §2v3.5)
        public float BaseStaminaMax = 100f;
        public float StrikeCost = 3f;
        public float WaterCostPerSecond = 6f;
        public float BaseStaminaRegen = 2f;
        /// <summary>The ground gives back: each crop reaped refills this much, so reaping feeds the next strikes.</summary>
        public float ReapStamina = 8f;

        // Growth and reaping (GDD §2v3.6–7)
        public float WaterBoost = 3f;
        public double ComboPerCrop = 0.1;

        // Field variety (GDD §2.3/§2.4 v1.8)
        /// <summary>Each side neighbour growing a different crop adds this much to a harvest's value.</summary>
        public double NeighbourVarietyBonus = 0.04;
        /// <summary>A plot growing a different crop than it did last year sells for this much more.</summary>
        public double RotationBonus = 1.15;
        /// <summary>A fertile plot sells for this much more.</summary>
        public double FertileValue = 1.3;
        /// <summary>Chance that a plot added by expanding the field is fertile.</summary>
        public double FertileChance = 0.15;

        // Seasons, goals, grade and weather (GDD §3/§5.4 v1.9, re-themed v3)
        /// <summary>Spring rain softens the ground: strike damage × this.</summary>
        public float SpringSoftness = 1.25f;
        /// <summary>Summer drought hardens it (damage × this) and slows growth.</summary>
        public float SummerHardness = 0.83f;
        public float SummerGrowth = 0.8f;
        /// <summary>Autumn harvest festival: the swipe combo pays this much more per crop.</summary>
        public float AutumnCombo = 1.5f;
        /// <summary>Harvests during the frost warning are worth this much more.</summary>
        public double FrostRushValue = 1.25;
        /// <summary>Average harvest freshness needed for 2 and for 3 stars (crops lost to crows count as 0).</summary>
        public double[] GradeThresholds = { 0.85, 0.97 };
        /// <summary>Share of the year's coins paid on top, indexed by stars (0–3).</summary>
        public double[] GradeBonusByStars = { 0, 0, 0.02, 0.04 };
        /// <summary>Goals start in this year of a generation.</summary>
        public int GoalFirstYear = 2;
        public double GoalHarvestsPerPlot = 2;
        public int GoalComboBase = 3, GoalComboPerYear = 1, GoalComboMax = 9;
        /// <summary>A coins goal asks for last year's coins times this.</summary>
        public double GoalCoinsGrowth = 1.2;
        /// <summary>A goal pays this share of last year's coins, never less than <see cref="GoalMinReward"/>.</summary>
        public double GoalRewardShare = 0.05;
        public double GoalMinReward = 10;
        /// <summary>Weather starts in this year of a generation, with this chance of one spell a year.</summary>
        public int WeatherFirstYear = 2;
        public double WeatherChance = 0.6;
        public float StormSeconds = 12f, HeatWaveSeconds = 15f, FogSeconds = 12f;
        /// <summary>A storm grows every crop this much faster and softens the ground by this much.</summary>
        public float StormGrowth = 1.5f, StormSoftness = 1.25f;
        /// <summary>A heat wave slows growth and bakes the ground by this much.</summary>
        public float HeatWaveGrowth = 0.7f, HeatWaveHardness = 0.8f;

        // Helpers and animals (GDD §4 v2.0)
        /// <summary>The farm dog goes for a crow that has sat this long, then rests for the cooldown.</summary>
        public float DogReactSeconds = 1.5f, DogCooldownSeconds = 10f;
        /// <summary>The beehive speeds growth by this much on the right-most columns (by the sunflowers).</summary>
        public float BeeGrowth = 1.3f;
        public int BeeColumns = 2;
        /// <summary>The tractor can be sent by hand once its timer is this far charged.</summary>
        public float TractorManualReady = 0.5f;

        // Events and threats (GDD §5.5–§5.7 v2.1)
        /// <summary>Pests come from this year of a generation: a check every so often, with this chance.</summary>
        public int PestFirstYear = 3;
        public float PestCheckSeconds = 15f;
        public double PestChance = 0.35;
        public float MoleDigSeconds = 5f, RabbitEatSeconds = 5f, LocustSeconds = 12f;
        /// <summary>Strikes inside the swarm needed to drive it off; the swarm covers a square this many plots out from its centre.</summary>
        public int LocustShooStrikes = 3;
        public int LocustRadius = 1;
        /// <summary>A bonked mole drops this many crop values.</summary>
        public double MoleBounty = 1;
        /// <summary>The hens go for a pest that has been there this long, then rest; after a meal, a golden egg now and then.</summary>
        public float HenReactSeconds = 2.5f, HenCooldownSeconds = 12f;
        public double GoldenEggChance = 0.2, GoldenEggValue = 6;
        /// <summary>Lucky moments from this year: a clover check every few seconds, the star at the frost warning.</summary>
        public int LuckyFirstYear = 2;
        public float LuckyCheckSeconds = 20f;
        public double CloverChance = 0.12, CloverValue = 8;
        public float CloverSeconds = 10f;
        public double ShootingStarChance = 0.35;
        public float ShootingStarSeconds = 4f, StarRushSeconds = 10f;
        public double StarRushValue = 2;
        /// <summary>The trader: from this year, with this chance a summer, for this long; prices scale from last year's coins.</summary>
        public int TraderFirstYear = 2;
        public double TraderChance = 0.5;
        public float TraderSeconds = 25f;
        public double TraderSeedPriceShare = 0.6, TraderSeedMinPrice = 60;
        public double TraderRarePriceShare = 0.15, TraderRareMinPrice = 20;
        public int TraderRarePlots = 2;

        // Winter (GDD §3.5/§6.3 v2.2)
        /// <summary>Crops the barn holds, per `barn` level.</summary>
        public int[] BarnCapacityByLevel = { 0, 20, 50, 120 };
        /// <summary>The shares of the harvest the player can send to the barn.</summary>
        public float[] StoreShares = { 0f, 0.25f, 0.5f };
        /// <summary>Each winter's market price is drawn between these (a multiple of the stored value).</summary>
        public double MarketMin = 0.8, MarketMax = 1.7;
        /// <summary>Stock held into a new year loses this share.</summary>
        public double BarnSpoil = 0.1;
        /// <summary>Preserves pay this multiple of the stored value, next spring.</summary>
        public double PreserveValue = 1.25;
        /// <summary>Winter closes the cracks: hard ground's HP refills to this share of its full HP at the frost (GDD §2v3.2 v3.2).</summary>
        public float WinterHpRefill = 1f;

        // Progression and rebirth (GDD §7.4–§7.6 v2.3)
        /// <summary>Offer heirs at each rebirth, and let heirlooms apply (rule tests turn both off).</summary>
        public bool HeirsEnabled = true, HeirloomsEnabled = true;
        /// <summary>Each heir trait's size.</summary>
        public double TraitGreenThumb = 0.12, TraitQuickHands = 0.08, TraitMerchant = 0.06, TraitSteady = 0.04, TraitWatchful = 0.3;
        public float TraitShepherd = 0.15f;
        /// <summary>A short-years challenge scales the year by this; any challenge multiplies the seeds at retirement.</summary>
        public float ChallengeShortYear = 0.7f;
        public double ChallengeSeedBonus = 1.5;
        /// <summary>Each New Game+ round: this many more crows, and years this much shorter (never below 70%) (GDD §8.3 v2.4).</summary>
        public double NgPlusCrows = 0.3;
        public float NgPlusYear = 0.06f;
        /// <summary>The album keeps this many pages.</summary>
        public int AlbumPages = 64;

        // Year (GDD §3)
        public float BaseYearLength = 90f;
        public float MaxYearLength = 180f;
        public float BaseFrostWarningSeconds = 10f;

        // Crows (GDD §2v3.8, §5.1)
        public int CrowFirstYear = 2;
        /// <summary>A crow may land on a crop that has stood ripe at least this long.</summary>
        public float CrowMinRipe = 3f;
        public float CrowSpawnInterval = 1f;
        /// <summary>Chance per spawn tick that a crow comes (GDD §5.1). Scarecrows guard an area rather than lowering it (v2.0).</summary>
        public float CrowSpawnChance = 0.08f;
        /// <summary>A scarecrow keeps crows off every plot whose centre is this close to its corner (12 plots at 1.6).</summary>
        public float ScarecrowRadius = 1.6f;
        public int MaxCrows = 2;
        public float CrowEatTime = 4f;
        /// <summary>A tap-scared crow drops this × crop value, on top of the crop it was sitting on.</summary>
        public double CrowScareValueMultiplier = 2;

        // Apprentices (GDD §2v3.9, §4)
        public float ApprenticeBaseSpeed = 1.5f;
        public float ApprenticeBaseWorkTime = 1.0f;
        public float ApprenticeMinWorkTime = 0.4f;
        /// <summary>Indexed by apprentice_yield level.</summary>
        public double[] ApprenticeYieldByLevel = { 0.5, 0.75, 1.0, 1.15, 1.3 };
        /// <summary>A digger's blow, as a share of the player's strike damage (never a crit).</summary>
        public double ApprenticeDigShare = 0.5;
        /// <summary>A waterer's visit adds this much growth (0..1) to the crop.</summary>
        public float ApprenticeWaterBoost = 0.25f;
        /// <summary>How far below the field's bottom row apprentices idle (plot units).</summary>
        public float ApprenticeIdleOffset = 1.2f;

        // Heritage (GDD §7)
        /// <summary>Lifetime coins in this generation needed before "Pass on the farm" unlocks.</summary>
        public double HeritageThreshold = 3500;
        /// <summary>Seeds = floor(sqrt(lifetimeCoinsThisGeneration / SeedDivisor)).</summary>
        public double SeedDivisor = 38;

        // Events and remaining nodes (GDD §4, §5, §6, §7)
        public float CloudDriftSeconds = 8f;
        /// <summary>The rain cloud's tap adds this much growth (0..1) to every growing crop.</summary>
        public float CloudGrowBoost = 0.25f;
        public double GoldenValueMultiplier = 10;
        /// <summary>Indexed by tractor level (0 unused).</summary>
        public float[] TractorIntervalByLevel = { 0f, 30f, 20f, 12f };
        public float TractorSecondsPerPlot = 0.15f;
        public double GreenhouseRatePerLevel = 0.02;
        public float GreenhouseWinterCapSeconds = 60f;
        /// <summary>A winter's greenhouse income never exceeds this share of the year that just ended (v2.8).</summary>
        public double GreenhouseWinterCapShare = 0.25;
        /// <summary>The Heritage Old Scarecrow leaves this fraction of the crow spawn chance (v2.8) instead of none.</summary>
        public float ScarecrowImmunityCrowFactor = 0.25f;
        /// <summary>Hoe Master: on top of its strike speed, the break bonus per level (v2.8, re-themed v3).</summary>
        public double HoeMasterCoinsPerLevel = 0.10;
        /// <summary>Early Thaw: hard ground opens spring with this share of its cracks already made (HP × (1 − this)).</summary>
        public float EarlyThawShare = 0.25f;
        /// <summary>Head Start: hard ground opens spring at this share of its HP, and growing crops this far grown (v2.8, re-themed v3).</summary>
        public float SpringHeadStartProgress = 0.5f;
        // --- M.1 (GDD §2.5 v1.6): ripe crops do not wait forever.
        /// <summary>A Ripe plot keeps full value for this long; after it the value falls off.</summary>
        public float RipeGraceSeconds = 12f;
        /// <summary>Seconds from the end of the grace to the lowest value.</summary>
        public float OverripeDecaySeconds = 24f;
        /// <summary>A crop never falls below this share of its value: waiting costs, it never wastes the crop.</summary>
        public double OverripeMinValue = 0.5;
        /// <summary>Swipe lengths that pay a bonus.</summary>
        public int[] ComboMilestones = { 5, 12, 25 };
        /// <summary>Bonus at each milestone, in crop values of the harvest that reached it.</summary>
        public double[] ComboMilestoneBonus = { 3, 8, 20 };
        /// <summary>Seconds the HUD keeps a swipe's combo count after the swipe.</summary>
        public float ComboWindowSeconds = 1.0f;
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
