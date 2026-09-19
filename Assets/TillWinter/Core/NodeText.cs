using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TillWinter.Core
{
    /// <summary>
    /// Values for node description templates: {cur}, {next}, {level}, {max}, {cost}. Core computes the
    /// numbers through <see cref="StatResolver"/>; the string table (presentation) owns the words.
    /// </summary>
    public static class NodeText
    {
        public static readonly string[] Placeholders = { "cur", "next", "level", "max", "cost" };

        public static Dictionary<string, string> Values(FarmSim sim, string nodeId)
        {
            var node = sim.GetNode(nodeId);
            var v = new Dictionary<string, string>();
            if (node == null) return v;
            int level = sim.State.GetLevel(nodeId);
            int max = sim.GetMaxLevel(nodeId);
            bool maxed = sim.IsMaxed(nodeId);
            v["level"] = level.ToString(CultureInfo.InvariantCulture);
            v["max"] = max.ToString(CultureInfo.InvariantCulture);
            v["cost"] = maxed ? "-" : NumberFormat.Short(sim.CostOf(nodeId));
            v["cur"] = EffectValue(sim, node, level);
            v["next"] = maxed ? v["cur"] : EffectValue(sim, node, level + 1);
            return v;
        }

        /// <summary>Human-readable value of a node's effect at a hypothetical level (other levels unchanged).</summary>
        public static string EffectValue(FarmSim sim, SkillNode node, int level)
        {
            var almanac = new Dictionary<string, int>(sim.Almanac.Levels);
            var heritage = new Dictionary<string, int>(sim.Heritage.Levels);
            (sim.Almanac.Contains(node.Id) ? almanac : heritage)[node.Id] = level;
            var s = sim.ResolveWith(almanac, heritage); // with the heir, challenge, New Game+ and heirlooms, as it will play
            var cfg = sim.Config;
            switch (node.Effect)
            {
                case EffectType.RingRadius:
                case EffectType.HeritageStartRadius: return F(s.RingRadius, "0.00");
                case EffectType.RingWaterSpeed: return "x" + F(s.RingWaterMult, "0.0");
                case EffectType.RingGrowSpeed: return "x" + F(s.RingGrowMult, "0.0");
                case EffectType.RingHarvestSpeed: return "x" + F(s.RingHarvestMult, "0.0");
                case EffectType.HeritageRingSpeeds: return "+" + Pct(node.ValuePerLevel * level);
                case EffectType.RingBonusCoins: return "+" + Pct(s.RingBonusMult - 1);
                case EffectType.HeritageRingCoins: return "+" + Pct(node.ValuePerLevel * level);
                case EffectType.RingCombo: return "+" + Pct(node.ValuePerLevel * level * cfg.ComboMaxStacks);
                case EffectType.Irrigation: return Pct(s.IrrigationFactor);
                case EffectType.Sun: return Pct(s.SunFactor);
                case EffectType.SoilQuality: return "x" + F(s.SoilMultiplier, "0.00");
                case EffectType.HeritageGlobalGrowth: return "+" + Pct(node.ValuePerLevel * level);
                case EffectType.CropValue: return "+" + Pct(s.CropValueMult - 1);
                case EffectType.ExpandField: return s.TargetGridSize + "x" + s.TargetGridSize;
                case EffectType.HeritageStartField: return s.StartGridSize + "x" + s.StartGridSize;
                case EffectType.UpgradePlot: return level.ToString(CultureInfo.InvariantCulture);
                case EffectType.UnlockTier: return level > 0 ? "unlocked" : "locked";
                case EffectType.HeritageStartTomato: return level > 0 ? "unlocked" : "locked";
                case EffectType.ApprenticeCount:
                case EffectType.FreeApprentice: return s.ApprenticeCount.ToString(CultureInfo.InvariantCulture);
                case EffectType.ApprenticeSpeed: return F(s.ApprenticeSpeed, "0.0");
                case EffectType.ApprenticeHarvestTime: return F(s.ApprenticeHarvestTime, "0.0") + " s";
                case EffectType.ApprenticeYield: return Pct(s.ApprenticeYield);
                case EffectType.HeritageApprenticeYield: return "+" + Pct(node.ValuePerLevel * level);
                case EffectType.Tractor: return level > 0 && level < cfg.TractorIntervalByLevel.Length ? F(cfg.TractorIntervalByLevel[level], "0") + " s" : "-";
                case EffectType.Scarecrow: return s.ScarecrowCount.ToString(CultureInfo.InvariantCulture);
                case EffectType.Barn: return s.BarnCapacity.ToString(CultureInfo.InvariantCulture);
                case EffectType.ScarecrowImmunity: return level > 0 ? "owned" : "-";
                case EffectType.YearLength:
                case EffectType.HeritageStartYearLength: return F(s.YearLength, "0") + " s";
                case EffectType.FrostWarning: return F(s.FrostWarningSeconds, "0") + " s";
                case EffectType.Greenhouse: return Pct(level * cfg.GreenhouseRatePerLevel) + "/s";
                case EffectType.GreenhouseX2: return level > 0 ? "x2" : "x1";
                case EffectType.CrowBounty: return "x" + F(cfg.CrowScareValueMultiplier + level, "0");
                case EffectType.AlmanacDiscount: return "-" + Pct(1 - s.AlmanacCostMult);
                case EffectType.GoldenCropChance: return Pct(s.GoldenCropChance);
                case EffectType.RingShape: return level >= 2 ? "rake+cross" : level == 1 ? "rake" : "round";
                case EffectType.TapHarvest: return level > 0 && level < cfg.TapHarvestCooldownByLevel.Length
                    ? F(cfg.TapHarvestCooldownByLevel[level], "0") + " s" : "-";
                case EffectType.BulkUpgrade:
                case EffectType.FertileStart:
                case EffectType.SpringHeadStart:
                case EffectType.LateFrost:
                case EffectType.HelperWater:
                case EffectType.FarmDog:
                case EffectType.Beehive:
                case EffectType.Hens:
                case EffectType.UnlockRainCloud:
                case EffectType.HeritageStartIrrigation:
                case EffectType.HeritageStartSun: return level > 0 ? "on" : "off";
                default: return F(node.ValuePerLevel * level, "0.##");
            }
        }

        /// <summary>Replaces {name} placeholders; unknown placeholders are left as-is, never throws.</summary>
        public static string Fill(string template, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return template ?? "";
            var sb = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                int open = template.IndexOf('{', i);
                if (open < 0) { sb.Append(template, i, template.Length - i); break; }
                int close = template.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(template, i, template.Length - i); break; }
                sb.Append(template, i, open - i);
                string key = template.Substring(open + 1, close - open - 1);
                if (values != null && values.TryGetValue(key, out var v)) sb.Append(v);
                else sb.Append('{').Append(key).Append('}');
                i = close + 1;
            }
            return sb.ToString();
        }

        private static string F(double v, string fmt) => NumberFormat.Decimal(v, fmt);
        private static string Pct(double v) => NumberFormat.Percent(v);
    }
}
