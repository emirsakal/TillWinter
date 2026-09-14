using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Every derived number the sim uses, computed from both trees' levels by <see cref="StatResolver"/>.</summary>
    public sealed class Stats
    {
        public float RingRadius;
        /// <summary>Multipliers over crop base times (level 0 = 1.0).</summary>
        public float RingWaterMult = 1f, RingGrowMult = 1f, RingHarvestMult = 1f;
        /// <summary>1 + 0.25 × soil_quality (× Heritage global growth). Applies to ring and passive growing.</summary>
        public float SoilMultiplier = 1f;
        /// <summary>Fraction of base water/grow speed applied passively (0.15 × level).</summary>
        public float IrrigationFactor, SunFactor;
        public double RingBonusMult = 1, CropValueMult = 1;

        public int ApprenticeCount;
        public float ApprenticeSpeed, ApprenticeHarvestTime;
        public double ApprenticeYield;

        public float CrowSpawnChance;
        public float YearLength, FrostWarningSeconds;

        /// <summary>Highest crop tier plots may be upgraded to.</summary>
        public int MaxTierUnlocked;
        /// <summary>Field size the Heritage tree starts a generation at (before Almanac expansions).</summary>
        public int StartGridSize;
        public int TargetGridSize;

        /// <summary>Multiplier on Almanac node costs (Heritage discount).</summary>
        public double AlmanacCostMult = 1;

        // Table entries the sim does not apply yet (surfaced so the UI can show them).
        public int RingComboLevel, TractorLevel, GreenhouseLevel, CrowBountyLevel;
        public bool BulkUpgrade, FertileStart, SpringHeadStart, LateFrost, HelperWater;
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
                RingRadius = cfg.BaseRingRadius,
                ApprenticeSpeed = cfg.ApprenticeBaseSpeed,
                ApprenticeHarvestTime = cfg.ApprenticeBaseHarvestTime,
                ApprenticeYield = cfg.ApprenticeYieldByLevel[0],
                CrowSpawnChance = cfg.CrowSpawnChanceByScarecrow[0],
                YearLength = cfg.BaseYearLength,
                FrostWarningSeconds = cfg.BaseFrostWarningSeconds,
                MaxTierUnlocked = 0,
                StartGridSize = cfg.StartGridSize,
            };

            // --- Heritage: base modifiers, applied first (GDD §7).
            float ringSpeedMult = 1f, globalGrowth = 1f;
            double ringCoinsMult = 1, apprenticeYieldMult = 1;
            int startIrrigation = 0, startSun = 0;
            foreach (var n in heritageNodes)
            {
                int level = Level(heritageLevels, n.Id);
                if (level <= 0) continue;
                double v = n.ValuePerLevel * level;
                switch (n.Effect)
                {
                    case EffectType.HeritageStartRadius: s.RingRadius += (float)v; break;
                    case EffectType.HeritageRingSpeeds: ringSpeedMult += (float)v; break;
                    case EffectType.HeritageRingCoins: ringCoinsMult += v; break;
                    case EffectType.HeritageStartIrrigation: startIrrigation = (int)Math.Round(v); break;
                    case EffectType.HeritageStartSun: startSun = (int)Math.Round(v); break;
                    case EffectType.HeritageGlobalGrowth: globalGrowth += (float)v; break;
                    case EffectType.HeritageStartField: s.StartGridSize = cfg.StartGridSize + (int)Math.Round(v); break;
                    case EffectType.HeritageStartTomato: s.MaxTierUnlocked = Math.Max(s.MaxTierUnlocked, 1); break;
                    case EffectType.FreeApprentice: s.ApprenticeCount += (int)Math.Round(v); break;
                    case EffectType.HeritageApprenticeYield: apprenticeYieldMult += v; break;
                    case EffectType.HeritageStartYearLength: s.YearLength += (float)v; break;
                    case EffectType.AlmanacDiscount: s.AlmanacCostMult = Math.Max(0.05, 1 - v); break;
                    case EffectType.UnlockRainCloud: s.RainCloudUnlocked = true; break;
                    case EffectType.GoldenCropChance: s.GoldenCropChance = v; break;
                    case EffectType.ScarecrowImmunity: s.ScarecrowImmunity = true; break;
                    case EffectType.GreenhouseX2: s.GreenhouseX2Level = level; break;
                }
            }
            s.TargetGridSize = s.StartGridSize;

            // --- Almanac.
            int irrigation = 0, sun = 0;
            foreach (var n in almanacNodes)
            {
                int level = Level(almanacLevels, n.Id);
                if (level <= 0) continue;
                double v = n.ValuePerLevel * level;
                switch (n.Effect)
                {
                    case EffectType.RingRadius: s.RingRadius += (float)v; break;
                    case EffectType.RingWaterSpeed: s.RingWaterMult += (float)v; break;
                    case EffectType.RingGrowSpeed: s.RingGrowMult += (float)v; break;
                    case EffectType.RingHarvestSpeed: s.RingHarvestMult += (float)v; break;
                    case EffectType.RingBonusCoins: s.RingBonusMult += v; break;
                    case EffectType.Irrigation: irrigation = level; break;
                    case EffectType.Sun: sun = level; break;
                    case EffectType.SoilQuality: s.SoilMultiplier += (float)v; break;
                    case EffectType.CropValue: s.CropValueMult += v; break;
                    case EffectType.ExpandField: s.TargetGridSize += level; break;
                    case EffectType.UnlockTier: s.MaxTierUnlocked = Math.Max(s.MaxTierUnlocked, (int)Math.Round(n.ValuePerLevel)); break;
                    case EffectType.ApprenticeCount: s.ApprenticeCount += level; break;
                    case EffectType.ApprenticeSpeed: s.ApprenticeSpeed += (float)v; break;
                    case EffectType.ApprenticeHarvestTime: s.ApprenticeHarvestTime -= (float)v; break;
                    case EffectType.ApprenticeYield: s.ApprenticeYield = Index(cfg.ApprenticeYieldByLevel, level); break;
                    case EffectType.Scarecrow: s.CrowSpawnChance = Index(cfg.CrowSpawnChanceByScarecrow, level); break;
                    case EffectType.YearLength: s.YearLength += (float)v; break;
                    case EffectType.FrostWarning: s.FrostWarningSeconds += (float)v; break;
                    case EffectType.RingCombo: s.RingComboLevel = level; break;
                    case EffectType.Tractor: s.TractorLevel = level; break;
                    case EffectType.Greenhouse: s.GreenhouseLevel = level; break;
                    case EffectType.CrowBounty: s.CrowBountyLevel = level; break;
                    case EffectType.BulkUpgrade: s.BulkUpgrade = true; break;
                    case EffectType.FertileStart: s.FertileStart = true; break;
                    case EffectType.SpringHeadStart: s.SpringHeadStart = true; break;
                    case EffectType.LateFrost: s.LateFrost = true; break;
                    case EffectType.HelperWater: s.HelperWater = true; break;
                    case EffectType.UpgradePlot: break; // applied at purchase time, not a stat
                }
            }

            // Heritage "start with Irrigation/Sun 1" acts as a floor on the Almanac level.
            float perLevel = PerLevel(almanacNodes, EffectType.Irrigation, 0.15);
            s.IrrigationFactor = perLevel * Math.Max(irrigation, startIrrigation);
            s.SunFactor = PerLevel(almanacNodes, EffectType.Sun, 0.15) * Math.Max(sun, startSun);

            s.RingWaterMult *= ringSpeedMult;
            s.RingGrowMult *= ringSpeedMult;
            s.RingHarvestMult *= ringSpeedMult;
            s.RingBonusMult *= ringCoinsMult;
            s.SoilMultiplier *= globalGrowth;
            s.ApprenticeYield *= apprenticeYieldMult;

            s.RingRadius = Math.Min(cfg.MaxRingRadius, s.RingRadius);
            s.YearLength = Math.Min(cfg.MaxYearLength, s.YearLength);
            s.ApprenticeHarvestTime = Math.Max(cfg.ApprenticeMinHarvestTime, s.ApprenticeHarvestTime);
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

        private static float Index(float[] table, int level) => table[Math.Max(0, Math.Min(table.Length - 1, level))];
        private static double Index(double[] table, int level) => table[Math.Max(0, Math.Min(table.Length - 1, level))];
    }
}
