using System.Collections.Generic;
using TillWinter.Core;

namespace TillWinter.Unity
{
    /// <summary>
    /// Placeholder EN strings for Core keys. Core holds no user-facing text (GDD §1); the real
    /// EN/TR table arrives in the localization session. Unknown keys fall back to the key itself.
    /// </summary>
    public static class Localize
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["crop.carrot"] = "Carrot", ["crop.tomato"] = "Tomato", ["crop.corn"] = "Corn",
            ["crop.pumpkin"] = "Pumpkin", ["crop.grapes"] = "Grapes", ["crop.golden_wheat"] = "Golden Wheat",

            ["almanac.ring_radius.name"] = "Ring Radius", ["almanac.ring_radius.desc"] = "+0.25 plots ring radius",
            ["almanac.ring_water_speed.name"] = "Ring Watering", ["almanac.ring_water_speed.desc"] = "+20% watering speed under the ring",
            ["almanac.ring_grow_speed.name"] = "Ring Growing", ["almanac.ring_grow_speed.desc"] = "+20% growing speed under the ring",
            ["almanac.ring_harvest_speed.name"] = "Ring Harvesting", ["almanac.ring_harvest_speed.desc"] = "+20% harvest speed under the ring",
            ["almanac.ring_bonus_coins.name"] = "Green Thumb", ["almanac.ring_bonus_coins.desc"] = "+10% coins on ring harvests",
            ["almanac.ring_combo.name"] = "Combo", ["almanac.ring_combo.desc"] = "Consecutive ring harvests within 1 s stack a small bonus",

            ["almanac.irrigation.name"] = "Irrigation", ["almanac.irrigation.desc"] = "Dry plots water on their own (+15% of ring speed)",
            ["almanac.sun.name"] = "Sun", ["almanac.sun.desc"] = "Wet plots grow on their own (+15% of ring speed)",
            ["almanac.soil_quality.name"] = "Soil", ["almanac.soil_quality.desc"] = "+25% growing speed everywhere",
            ["almanac.crop_value.name"] = "Crop Value", ["almanac.crop_value.desc"] = "+10% coins on every harvest",
            ["almanac.fertile_start.name"] = "Fertile Start", ["almanac.fertile_start.desc"] = "New plots start Wet",

            ["almanac.expand_field.name"] = "Expand Field", ["almanac.expand_field.desc"] = "Adds a ring of plots (up to 6x6)",
            ["almanac.upgrade_plot.name"] = "Upgrade Plot", ["almanac.upgrade_plot.desc"] = "Raises the lowest-tier plot by one crop tier",
            ["almanac.unlock_tomato.name"] = "Tomato", ["almanac.unlock_tomato.desc"] = "Unlocks tomato (4 coins)",
            ["almanac.unlock_corn.name"] = "Corn", ["almanac.unlock_corn.desc"] = "Unlocks corn (12 coins)",
            ["almanac.unlock_pumpkin.name"] = "Pumpkin", ["almanac.unlock_pumpkin.desc"] = "Unlocks pumpkin (35 coins)",
            ["almanac.unlock_grapes.name"] = "Grapes", ["almanac.unlock_grapes.desc"] = "Unlocks grapes (100 coins)",
            ["almanac.unlock_golden_wheat.name"] = "Golden Wheat", ["almanac.unlock_golden_wheat.desc"] = "Unlocks golden wheat (300 coins)",
            ["almanac.bulk_upgrade.name"] = "Bulk Upgrade", ["almanac.bulk_upgrade.desc"] = "Upgrade Plot raises 2 plots per purchase",

            ["almanac.apprentice_count.name"] = "Apprentice", ["almanac.apprentice_count.desc"] = "+1 helper who harvests ripe plots",
            ["almanac.apprentice_speed.name"] = "Quick Feet", ["almanac.apprentice_speed.desc"] = "+0.5 plots/s apprentice walk speed",
            ["almanac.apprentice_harvest_time.name"] = "Deft Hands", ["almanac.apprentice_harvest_time.desc"] = "Apprentices harvest 0.2 s faster",
            ["almanac.apprentice_yield.name"] = "Apprentice Yield", ["almanac.apprentice_yield.desc"] = "Apprentice harvests pay more (50% to 130%)",
            ["almanac.tractor.name"] = "Tractor", ["almanac.tractor.desc"] = "Periodically harvests a full row of ripe plots",
            ["almanac.scarecrow.name"] = "Scarecrow", ["almanac.scarecrow.desc"] = "Crow chance 25% to 15% to 8%",
            ["almanac.helper_water.name"] = "Watering Can", ["almanac.helper_water.desc"] = "Apprentices also water the plot they stand on",

            ["almanac.year_length.name"] = "Calendar", ["almanac.year_length.desc"] = "+15 s year length (max 180 s)",
            ["almanac.frost_warning.name"] = "Weather Vane", ["almanac.frost_warning.desc"] = "+5 s frost warning",
            ["almanac.late_frost.name"] = "Late Frost", ["almanac.late_frost.desc"] = "At Winter, plots >= 80% grown pay half value",
            ["almanac.greenhouse.name"] = "Greenhouse", ["almanac.greenhouse.desc"] = "Earns coins during Winter",
            ["almanac.crow_bounty.name"] = "Crow Bounty", ["almanac.crow_bounty.desc"] = "Scared crows drop more coins",
            ["almanac.spring_head_start.name"] = "Head Start", ["almanac.spring_head_start.desc"] = "The year starts with all plots Wet",

            ["heritage.h_start_radius.name"] = "Wide Hands", ["heritage.h_start_radius.desc"] = "Every generation starts with +0.25 ring radius",
            ["heritage.h_ring_speeds.name"] = "Practiced Hands", ["heritage.h_ring_speeds.desc"] = "+10% to all ring speeds",
            ["heritage.h_ring_coins.name"] = "Family Recipe", ["heritage.h_ring_coins.desc"] = "+5% coins on ring harvests",
            ["heritage.h_start_irrigation.name"] = "Old Well", ["heritage.h_start_irrigation.desc"] = "Start every generation with Irrigation 1",
            ["heritage.h_start_sun.name"] = "Sunny Slope", ["heritage.h_start_sun.desc"] = "Start every generation with Sun 1",
            ["heritage.h_global_growth.name"] = "Rich Land", ["heritage.h_global_growth.desc"] = "+5% growth everywhere",
            ["heritage.h_unlock_rain_cloud.name"] = "Rain Cloud", ["heritage.h_unlock_rain_cloud.desc"] = "A tappable cloud waters the field once a year",
            ["heritage.h_start_field.name"] = "Bigger Barn", ["heritage.h_start_field.desc"] = "Start every generation at 4x4",
            ["heritage.h_start_tomato.name"] = "Tomato Seeds", ["heritage.h_start_tomato.desc"] = "Start every generation with tomato unlocked",
            ["heritage.h_golden_crop.name"] = "Golden Crop", ["heritage.h_golden_crop.desc"] = "+1% chance a replanted crop is golden (10x)",
            ["heritage.h_free_apprentice.name"] = "Family Helper", ["heritage.h_free_apprentice.desc"] = "The first apprentice is free",
            ["heritage.h_apprentice_yield.name"] = "Trusted Hands", ["heritage.h_apprentice_yield.desc"] = "+5% apprentice yield",
            ["heritage.h_scarecrow_immunity.name"] = "Old Scarecrow", ["heritage.h_scarecrow_immunity.desc"] = "Scarecrow level 3: no crows",
            ["heritage.h_start_year_length.name"] = "Long Summers", ["heritage.h_start_year_length.desc"] = "+10 s starting year length",
            ["heritage.h_greenhouse_x2.name"] = "Glass Roof", ["heritage.h_greenhouse_x2.desc"] = "Greenhouse earns x2",
            ["heritage.h_almanac_discount.name"] = "Old Notes", ["heritage.h_almanac_discount.desc"] = "Almanac costs -5%",
        };

        public static string Get(string key) => key != null && En.TryGetValue(key, out var s) ? s : key;
        public static string Name(SkillNode node) => Get(node.NameKey);
        public static string Desc(SkillNode node) => Get(node.DescKey);
        public static string Crop(CropDef crop) => Get(crop.Key);

        public static string[] Names(string[] nodeIds)
        {
            var result = new string[nodeIds.Length];
            for (int i = 0; i < nodeIds.Length; i++)
                result[i] = nodeIds[i].StartsWith("h_") ? Get("heritage." + nodeIds[i] + ".name") : Get("almanac." + nodeIds[i] + ".name");
            return result;
        }
    }
}
