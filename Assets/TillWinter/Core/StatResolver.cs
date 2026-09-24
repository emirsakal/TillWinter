using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Every derived number the sim uses, computed from both trees' levels by <see cref="StatResolver"/>.</summary>
    public sealed class Stats
    {
        // The hoe (GDD §2v3.4–5)
        public double StrikeDamage;
        public float CritWindow;
        public double CritChance;
        public float StrikeCooldown;
        /// <summary>Share of a strike's damage the four side neighbours take (0 = none).</summary>
        public float SplashShare;
        public float StaminaMax, StaminaRegen;
        /// <summary>Multiplier on what a break pays.</summary>
        public double BreakBonusMult = 1;
        /// <summary>Extra value per crop beyond the first in one swipe.</summary>
        public double ComboPerCrop;

        // Soil
        /// <summary>1 + growth levels (× Heritage global growth × heirlooms): applies to every growing crop.</summary>
        public float GrowthMult = 1f;
        /// <summary>1 + 0.25 × soil_quality, on top of <see cref="GrowthMult"/>.</summary>
        public float SoilMultiplier = 1f;
        /// <summary>Multiplier on hard ground's HP (softness lowers it).</summary>
        public float HpMult = 1f;
        public double CropValueMult = 1;

        public int ApprenticeCount;
        public float ApprenticeSpeed, ApprenticeWorkTime;
        public double ApprenticeYield;
        /// <summary>A digger's blow as a share of the player's strike damage.</summary>
        public double ApprenticeDigShare;

        public float CrowSpawnChance;
        /// <summary>Scarecrows the player can place (GDD §5.1 v2.0).</summary>
        public int ScarecrowCount;
        /// <summary>GDD §4.3 (v2.0): the farm dog and the beehive.</summary>
        public bool FarmDog, Beehive;
        /// <summary>GDD §4.3/§5.5 (v2.1): hens that eat pests.</summary>
        public bool Hens;
        /// <summary>Crops the barn holds (GDD §3.5 v2.2); 0 = no barn.</summary>
        public int BarnCapacity;
        public float YearLength, FrostWarningSeconds;
        /// <summary>How far Long Summer lifts the year-length ceiling (v2.8).</summary>
        public float YearLengthCapBonus;

        /// <summary>Highest crop tier plots may be upgraded to.</summary>
        public int MaxTierUnlocked;
        /// <summary>Field size the Heritage tree starts a generation at (before Almanac expansions).</summary>
        public int StartGridSize;
        public int TargetGridSize;

        /// <summary>Multiplier on Almanac node costs (Heritage discount).</summary>
        public double AlmanacCostMult = 1;

        // Feature levels and flags applied by FarmSim rather than as a rate.
        public int TractorLevel, GreenhouseLevel, CrowBountyLevel;
        public bool BulkUpgrade, EarlyThaw, SpringHeadStart, LateFrost;
        public bool RainCloudUnlocked, ScarecrowImmunity;
        public double GoldenCropChance;
        public int GreenhouseX2Level;
    }

    public static class StatResolver
    {
        public static Stats Resolve(FarmConfig cfg, IReadOnlyDictionary<string, int> almanacLevels,
            IReadOnlyDictionary<string, int> heritageLevels = null,
            IReadOnlyList<SkillNode> almanacNodes = null, IReadOnlyList<SkillNode> heritageNodes = null)
        {
            almanacNodes = almanacNodes ?? AlmanacData.Nodes;
            heritageNodes = heritageNodes ?? HeritageData.Nodes;
            var s = new Stats
            {
                StrikeDamage = cfg.BaseStrikeDamage,
                CritWindow = cfg.BaseCritWindow,
                CritChance = cfg.BaseCritChance,
                StrikeCooldown = cfg.BaseStrikeCooldown,
                StaminaMax = cfg.BaseStaminaMax,
                StaminaRegen = cfg.BaseStaminaRegen,
                ComboPerCrop = cfg.ComboPerCrop,
                ApprenticeSpeed = cfg.ApprenticeBaseSpeed,
                ApprenticeWorkTime = cfg.ApprenticeBaseWorkTime,
                ApprenticeYield = cfg.ApprenticeYieldByLevel[0],
                ApprenticeDigShare = cfg.ApprenticeDigShare,
                CrowSpawnChance = cfg.CrowSpawnChance,
                YearLength = cfg.BaseYearLength,
                FrostWarningSeconds = cfg.BaseFrostWarningSeconds,
                MaxTierUnlocked = 0,
                StartGridSize = cfg.StartGridSize,
            };

            // --- Heritage: base modifiers, applied first (GDD §7).
            float strikeSpeed = 0f, globalGrowth = 1f;
            double breakCoins = 1, apprenticeYieldMult = 1;
            int startGrowth = 0, startSoft = 0;
            foreach (var n in heritageNodes)
            {
                int level = Level(heritageLevels, n.Id);
                if (level <= 0) continue;
                double v = n.ValuePerLevel * level;
                switch (n.Effect)
                {
                    case EffectType.HeritageStartDamage: s.StrikeDamage += v; break;
                    case EffectType.HeritageStrikeSpeed: strikeSpeed += (float)v; break;
                    case EffectType.HeritageBreakCoins: breakCoins += v; break;
                    case EffectType.HeritageStartGrowth: startGrowth = (int)Math.Round(v); break;
                    case EffectType.HeritageStartSoft: startSoft = (int)Math.Round(v); break;
                    case EffectType.HeritageGlobalGrowth: globalGrowth += (float)v; break;
                    case EffectType.HeritageStartField: s.StartGridSize = cfg.StartGridSize + (int)Math.Round(v); break;
                    case EffectType.HeritageStartTomato: s.MaxTierUnlocked = Math.Max(s.MaxTierUnlocked, 1); break;
                    case EffectType.FreeApprentice: s.ApprenticeCount += (int)Math.Round(v); break;
                    case EffectType.HeritageApprenticeYield: apprenticeYieldMult += v; break;
                    case EffectType.HeritageStartYearLength: s.YearLength += (float)v; break;
                    // Long Summer counts past the ceiling, or it would be dead once the Almanac's year_length is maxed.
                    case EffectType.LongSummer: s.YearLength += (float)v; s.YearLengthCapBonus += (float)v; break;
                    // Hoe Master pays the active player twice: a faster hoe, and more coins from what it breaks.
                    case EffectType.HeritageHoeMaster: strikeSpeed += (float)v; breakCoins += cfg.HoeMasterCoinsPerLevel * level; break;
                    case EffectType.AlmanacDiscount: s.AlmanacCostMult = Math.Max(0.05, 1 - v); break;
                    case EffectType.UnlockRainCloud: s.RainCloudUnlocked = true; break;
                    case EffectType.GoldenCropChance: s.GoldenCropChance = v; break;
                    case EffectType.ScarecrowImmunity: s.ScarecrowImmunity = true; break;
                    case EffectType.GreenhouseX2: s.GreenhouseX2Level = level; break;
                }
            }
            s.TargetGridSize = s.StartGridSize;

            // --- Almanac.
            int growth = 0, soft = 0;
            foreach (var n in almanacNodes)
            {
                int level = Level(almanacLevels, n.Id);
                if (level <= 0) continue;
                double v = n.ValuePerLevel * level;
                switch (n.Effect)
                {
                    case EffectType.StrikeDamage: s.StrikeDamage += v; break;
                    case EffectType.CritWindow: s.CritWindow += (float)v; break;
                    case EffectType.CritChance: s.CritChance += v; break;
                    case EffectType.StrikeSpeed: s.StrikeCooldown -= (float)v; break;
                    case EffectType.Splash: s.SplashShare += (float)v; break;
                    case EffectType.StaminaMax: s.StaminaMax += (float)v; break;
                    case EffectType.StaminaRegen: s.StaminaRegen += (float)v; break;
                    case EffectType.BreakBonus: s.BreakBonusMult += v; break;
                    case EffectType.ReapCombo: s.ComboPerCrop += v; break;
                    case EffectType.Growth: growth = level; break;
                    case EffectType.Softness: soft = level; break;
                    case EffectType.SoilQuality: s.SoilMultiplier += (float)v; break;
                    case EffectType.CropValue: s.CropValueMult += v; break;
                    case EffectType.ExpandField: s.TargetGridSize += level; break;
                    case EffectType.UnlockTier: s.MaxTierUnlocked = Math.Max(s.MaxTierUnlocked, (int)Math.Round(n.ValuePerLevel)); break;
                    case EffectType.ApprenticeCount: s.ApprenticeCount += level; break;
                    case EffectType.ApprenticeSpeed: s.ApprenticeSpeed += (float)v; break;
                    case EffectType.ApprenticeWorkTime: s.ApprenticeWorkTime -= (float)v; break;
                    case EffectType.ApprenticeYield: s.ApprenticeYield = Index(cfg.ApprenticeYieldByLevel, level); break;
                    case EffectType.ApprenticeDig: s.ApprenticeDigShare += v; break;
                    case EffectType.Scarecrow: s.ScarecrowCount = level; break;
                    case EffectType.FarmDog: s.FarmDog = true; break;
                    case EffectType.Beehive: s.Beehive = true; break;
                    case EffectType.Hens: s.Hens = true; break;
                    case EffectType.Barn: s.BarnCapacity = cfg.BarnCapacityByLevel[Math.Max(0, Math.Min(cfg.BarnCapacityByLevel.Length - 1, level))]; break;
                    case EffectType.YearLength: s.YearLength += (float)v; break;
                    case EffectType.FrostWarning: s.FrostWarningSeconds += (float)v; break;
                    case EffectType.Tractor: s.TractorLevel = level; break;
                    case EffectType.Greenhouse: s.GreenhouseLevel = level; break;
                    case EffectType.CrowBounty: s.CrowBountyLevel = level; break;
                    case EffectType.BulkUpgrade: s.BulkUpgrade = true; break;
                    case EffectType.EarlyThaw: s.EarlyThaw = true; break;
                    case EffectType.SpringHeadStart: s.SpringHeadStart = true; break;
                    case EffectType.LateFrost: s.LateFrost = true; break;
                    case EffectType.UpgradePlot: break; // applied at purchase time, not a stat
                }
            }

            // Heritage "start with growth/soft ground 1" acts as a floor on the Almanac level.
            s.GrowthMult = 1f + PerLevel(almanacNodes, EffectType.Growth, 0.15) * Math.Max(growth, startGrowth);
            s.HpMult = Math.Max(0.2f, 1f - PerLevel(almanacNodes, EffectType.Softness, 0.06) * Math.Max(soft, startSoft));

            s.StrikeCooldown /= 1f + strikeSpeed;
            s.BreakBonusMult *= breakCoins;
            s.GrowthMult *= globalGrowth;
            s.ApprenticeYield *= apprenticeYieldMult;

            // GDD §7 (v2.8): Heritage Old Scarecrow + Almanac scarecrow 2 -> crows come a quarter as often, never never.
            if (s.ScarecrowImmunity && Level(almanacLevels, "scarecrow") >= 2) s.CrowSpawnChance *= cfg.ScarecrowImmunityCrowFactor;

            s.CritWindow = Math.Min(cfg.MaxCritWindow, s.CritWindow);
            s.CritChance = Math.Min(cfg.MaxCritChance, s.CritChance);
            s.StrikeCooldown = Math.Max(cfg.MinStrikeCooldown, s.StrikeCooldown);
            s.YearLength = Math.Min(cfg.MaxYearLength + s.YearLengthCapBonus, s.YearLength);
            s.ApprenticeWorkTime = Math.Max(cfg.ApprenticeMinWorkTime, s.ApprenticeWorkTime);
            s.StartGridSize = Math.Min(cfg.MaxGridSize, s.StartGridSize);
            s.TargetGridSize = Math.Min(cfg.MaxGridSize, s.TargetGridSize);
            s.MaxTierUnlocked = Math.Min(cfg.MaxTier, s.MaxTierUnlocked);
            return s;
        }

        private static int Level(IReadOnlyDictionary<string, int> levels, string id) =>
            levels != null && levels.TryGetValue(id, out var l) ? l : 0;

        private static float PerLevel(IReadOnlyList<SkillNode> nodes, EffectType effect, double fallback)
        {
            foreach (var n in nodes) if (n.Effect == effect) return (float)n.ValuePerLevel;
            return (float)fallback;
        }

        private static double Index(double[] table, int level) => table[Math.Max(0, Math.Min(table.Length - 1, level))];
    }
}
