using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Every derived number the sim uses, computed from Almanac levels by <see cref="StatResolver"/>.</summary>
    public sealed class Stats
    {
        public float RingRadius;
        /// <summary>Multipliers over crop base times (level 0 = 1.0).</summary>
        public float RingWaterMult = 1f, RingGrowMult = 1f, RingHarvestMult = 1f;
        /// <summary>1 + 0.25 × soil_quality. Applies to ring and passive growing.</summary>
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
        public int TargetGridSize;

        // Table entries the sim does not apply yet (surfaced so the UI can show them).
        public int RingComboLevel, TractorLevel, GreenhouseLevel, CrowBountyLevel;
        public bool BulkUpgrade, FertileStart, SpringHeadStart, LateFrost, HelperWater;
    }

    public static class StatResolver
    {
        public static Stats Resolve(FarmConfig cfg, IReadOnlyDictionary<string, int> levels, IReadOnlyList<AlmanacNode> nodes = null)
        {
            nodes = nodes ?? AlmanacData.Nodes;
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
                TargetGridSize = cfg.StartGridSize,
            };

            foreach (var n in nodes)
            {
                int level = levels != null && levels.TryGetValue(n.Id, out var l) ? l : 0;
                if (level <= 0) continue;
                double v = n.ValuePerLevel * level;
                switch (n.Effect)
                {
                    case EffectType.RingRadius: s.RingRadius += (float)v; break;
                    case EffectType.RingWaterSpeed: s.RingWaterMult += (float)v; break;
                    case EffectType.RingGrowSpeed: s.RingGrowMult += (float)v; break;
                    case EffectType.RingHarvestSpeed: s.RingHarvestMult += (float)v; break;
                    case EffectType.RingBonusCoins: s.RingBonusMult += v; break;
                    case EffectType.Irrigation: s.IrrigationFactor += (float)v; break;
                    case EffectType.Sun: s.SunFactor += (float)v; break;
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

            s.RingRadius = Math.Min(cfg.MaxRingRadius, s.RingRadius);
            s.YearLength = Math.Min(cfg.MaxYearLength, s.YearLength);
            s.ApprenticeHarvestTime = Math.Max(cfg.ApprenticeMinHarvestTime, s.ApprenticeHarvestTime);
            s.TargetGridSize = Math.Min(cfg.MaxGridSize, s.TargetGridSize);
            s.MaxTierUnlocked = Math.Min(cfg.MaxTier, s.MaxTierUnlocked);
            return s;
        }

        private static float Index(float[] table, int level) => table[Math.Max(0, Math.Min(table.Length - 1, level))];
        private static double Index(double[] table, int level) => table[Math.Max(0, Math.Min(table.Length - 1, level))];
    }
}
