using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Static Heritage table (GDD §7, re-themed for the hoe in v3). Costs in Heritage Seeds. Never reset.</summary>
    public static class HeritageData
    {
        /// <summary>Cost growth per level, per branch (S9: the balance pass tunes growth per branch before base costs).</summary>
        public static double Growth(Branch b)
        {
            switch (b)
            {
                case Branch.Hand: return 1.8;
                case Branch.Soil: return 1.8;
                case Branch.Field: return 1.8;
                case Branch.Helpers: return 1.8;
                case Branch.Calendar: return 1.8;
                default: return 1.5;
            }
        }
        private static readonly string[] None = new string[0];

        private static SkillNode N(string id, Branch b, string[] pre, int max, double cost, EffectType fx, double perLevel) =>
            new SkillNode(id, b, pre, max, cost, Growth(b), fx, perLevel, "heritage." + id + ".name", "heritage." + id + ".desc", null, IconFor(id));

        /// <summary>Node id -> sprite name in the node icon atlas (Kenney Game Icons). Every node must have one (IconTests).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> Icons = new System.Collections.Generic.Dictionary<string, string>
        {
            { "h_start_damage", "arrowUp" },
            { "h_strike_speed", "fastForward" },
            { "h_break_coins", "star" },
            { "h_start_growth", "contrast" },
            { "h_start_soft", "import" },
            { "h_global_growth", "signal1" },
            { "h_unlock_rain_cloud", "export" },
            { "h_start_field", "larger" },
            { "h_start_tomato", "plus" },
            { "h_golden_crop", "trophy" },
            { "h_free_apprentice", "singleplayer" },
            { "h_apprentice_yield", "cart" },
            { "h_scarecrow_immunity", "locked" },
            { "h_start_year_length", "scrollHorizontal" },
            { "h_greenhouse_x2", "home" },
            { "h_almanac_discount", "minus" },
            { "h_hoe_master", "target" },
            { "h_steward", "multiplayer" },
            { "h_long_summer", "scrollHorizontal" },
            { "h_rich_soil", "contrast" },
        };

        private static string IconFor(string id) => Icons.TryGetValue(id, out var k) ? k : "";

        public static readonly SkillNode[] Nodes =
        {
            // Hand: the hoe
            N("h_start_damage", Branch.Hand, None, 3, 3, EffectType.HeritageStartDamage, 1),
            N("h_strike_speed", Branch.Hand, new[] { "h_start_damage" }, 5, 4, EffectType.HeritageStrikeSpeed, 0.05),
            N("h_break_coins", Branch.Hand, new[] { "h_strike_speed" }, 4, 6, EffectType.HeritageBreakCoins, 0.05),

            // Soil
            N("h_start_growth", Branch.Soil, None, 1, 3, EffectType.HeritageStartGrowth, 1),
            N("h_start_soft", Branch.Soil, new[] { "h_start_growth" }, 1, 4, EffectType.HeritageStartSoft, 1),
            N("h_global_growth", Branch.Soil, new[] { "h_start_soft" }, 5, 5, EffectType.HeritageGlobalGrowth, 0.05),
            N("h_unlock_rain_cloud", Branch.Soil, new[] { "h_global_growth" }, 1, 12, EffectType.UnlockRainCloud, 1),

            // Field
            N("h_start_field", Branch.Field, None, 1, 6, EffectType.HeritageStartField, 1),
            N("h_start_tomato", Branch.Field, new[] { "h_start_field" }, 1, 5, EffectType.HeritageStartTomato, 1),
            N("h_golden_crop", Branch.Field, new[] { "h_start_tomato" }, 5, 8, EffectType.GoldenCropChance, 0.01),

            // Helpers
            N("h_free_apprentice", Branch.Helpers, None, 1, 5, EffectType.FreeApprentice, 1),
            N("h_apprentice_yield", Branch.Helpers, new[] { "h_free_apprentice" }, 4, 5, EffectType.HeritageApprenticeYield, 0.05),
            N("h_scarecrow_immunity", Branch.Helpers, new[] { "h_apprentice_yield" }, 1, 15, EffectType.ScarecrowImmunity, 1),

            // Calendar
            N("h_start_year_length", Branch.Calendar, None, 4, 4, EffectType.HeritageStartYearLength, 10),
            N("h_greenhouse_x2", Branch.Calendar, new[] { "h_start_year_length" }, 2, 10, EffectType.GreenhouseX2, 1),
            N("h_almanac_discount", Branch.Calendar, new[] { "h_start_year_length" }, 4, 6, EffectType.AlmanacDiscount, 0.05),

            // Paths (GDD §7.3 v2.3): two pairs where taking one rules out the other.
            X(N("h_hoe_master", Branch.Hand, new[] { "h_break_coins" }, 2, 6, EffectType.HeritageHoeMaster, 0.08), "h_steward"),
            X(N("h_steward", Branch.Helpers, new[] { "h_apprentice_yield" }, 2, 6, EffectType.HeritageApprenticeYield, 0.1), "h_hoe_master"),
            X(N("h_long_summer", Branch.Calendar, new[] { "h_start_year_length" }, 1, 8, EffectType.LongSummer, 15), "h_rich_soil"),
            X(N("h_rich_soil", Branch.Soil, new[] { "h_global_growth" }, 1, 8, EffectType.HeritageGlobalGrowth, 0.08), "h_long_summer"),
        };

        private static SkillNode X(SkillNode node, string excludes)
        {
            node.Excludes = excludes;
            return node;
        }


        private static Dictionary<string, SkillNode> _byId;

        public static SkillNode Get(string id)
        {
            if (_byId == null)
            {
                var map = new Dictionary<string, SkillNode>();
                foreach (var n in Nodes) map[n.Id] = n;
                _byId = map;
            }
            return _byId.TryGetValue(id ?? "", out var found) ? found : null;
        }
    }
}
