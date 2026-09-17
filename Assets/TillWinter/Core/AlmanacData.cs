using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Static Almanac table (GDD §6). Costs in coins, placeholders to tune. Reset on retire.</summary>
    public static class AlmanacData
    {
        /// <summary>Cost growth per level, per branch (S9: the balance pass tunes growth per branch before base costs).</summary>
        public static double Growth(Branch b)
        {
            switch (b)
            {
                case Branch.Hand: return 1.6;
                case Branch.Soil: return 1.6;
                case Branch.Field: return 1.18;
                case Branch.Helpers: return 1.25;
                case Branch.Calendar: return 1.6;
                default: return 1.6;
            }
        }
        private static readonly string[] None = new string[0];

        private static SkillNode N(string id, Branch b, string[] pre, int max, double cost, EffectType fx, double perLevel) =>
            new SkillNode(id, b, pre, max, cost, Growth(b), fx, perLevel, "almanac." + id + ".name", "almanac." + id + ".desc", null, IconFor(id));

        /// <summary>Node id -> sprite name in the node icon atlas (Kenney Game Icons). Every node must have one (IconTests).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> Icons = new System.Collections.Generic.Dictionary<string, string>
        {
            { "ring_radius", "zoomIn" },
            { "ring_water_speed", "fastForward" },
            { "ring_grow_speed", "forward" },
            { "ring_harvest_speed", "next" },
            { "ring_bonus_coins", "star" },
            { "ring_combo", "leaderboardsSimple" },
            { "ring_shape", "zoom" },
            { "tap_harvest", "export" },
            { "irrigation", "import" },
            { "sun", "contrast" },
            { "soil_quality", "barsVertical" },
            { "crop_value", "medal1" },
            { "fertile_start", "checkmark" },
            { "expand_field", "larger" },
            { "unlock_tomato", "plus" },
            { "upgrade_plot", "arrowUp" },
            { "unlock_corn", "signal1" },
            { "unlock_pumpkin", "signal2" },
            { "unlock_grapes", "medal2" },
            { "unlock_golden_wheat", "trophy" },
            { "bulk_upgrade", "menuGrid" },
            { "apprentice_count", "multiplayer" },
            { "apprentice_speed", "joystick" },
            { "apprentice_harvest_time", "basket" },
            { "apprentice_yield", "cart" },
            { "scarecrow", "warning" },
            { "tractor", "gear" },
            { "helper_water", "share1" },
            { "year_length", "scrollHorizontal" },
            { "frost_warning", "exclamation" },
            { "late_frost", "pause" },
            { "greenhouse", "home" },
            { "crow_bounty", "target" },
            { "spring_head_start", "power" },
        };

        private static string IconFor(string id) => Icons.TryGetValue(id, out var k) ? k : "";

        public static readonly SkillNode[] Nodes =
        {
            // Hand
            N("ring_radius", Branch.Hand, None, 5, 30, EffectType.RingRadius, 0.25),
            N("ring_water_speed", Branch.Hand, new[] { "ring_radius" }, 5, 120, EffectType.RingWaterSpeed, 0.2),
            N("ring_grow_speed", Branch.Hand, new[] { "ring_radius" }, 5, 120, EffectType.RingGrowSpeed, 0.2),
            N("ring_harvest_speed", Branch.Hand, new[] { "ring_water_speed", "ring_grow_speed" }, 3, 200, EffectType.RingHarvestSpeed, 0.2),
            N("ring_bonus_coins", Branch.Hand, new[] { "ring_harvest_speed" }, 4, 400, EffectType.RingBonusCoins, 0.1),
            N("ring_combo", Branch.Hand, new[] { "ring_bonus_coins" }, 3, 800, EffectType.RingCombo, 0.05),
            N("ring_shape", Branch.Hand, new[] { "ring_radius" }, 2, 350, EffectType.RingShape, 1),
            N("tap_harvest", Branch.Hand, new[] { "ring_harvest_speed" }, 2, 450, EffectType.TapHarvest, 1),

            // Soil
            N("irrigation", Branch.Soil, None, 5, 40, EffectType.Irrigation, 0.15),
            N("sun", Branch.Soil, new[] { "irrigation" }, 5, 150, EffectType.Sun, 0.15),
            N("soil_quality", Branch.Soil, new[] { "sun" }, 6, 250, EffectType.SoilQuality, 0.25),
            N("crop_value", Branch.Soil, new[] { "soil_quality" }, 5, 500, EffectType.CropValue, 0.1),
            N("fertile_start", Branch.Soil, new[] { "soil_quality" }, 1, 600, EffectType.FertileStart, 1),

            // Field
            N("expand_field", Branch.Field, None, 3, 60, EffectType.ExpandField, 1),
            N("unlock_tomato", Branch.Field, new[] { "expand_field" }, 1, 100, EffectType.UnlockTier, 1),
            N("upgrade_plot", Branch.Field, new[] { "unlock_tomato" }, -1, 25, EffectType.UpgradePlot, 1),
            N("unlock_corn", Branch.Field, new[] { "upgrade_plot" }, 1, 300, EffectType.UnlockTier, 2),
            N("unlock_pumpkin", Branch.Field, new[] { "unlock_corn" }, 1, 700, EffectType.UnlockTier, 3),
            N("unlock_grapes", Branch.Field, new[] { "unlock_pumpkin" }, 1, 1200, EffectType.UnlockTier, 4),
            N("unlock_golden_wheat", Branch.Field, new[] { "unlock_grapes" }, 1, 2000, EffectType.UnlockTier, 5),
            N("bulk_upgrade", Branch.Field, new[] { "unlock_corn" }, 1, 900, EffectType.BulkUpgrade, 1),

            // Helpers
            N("apprentice_count", Branch.Helpers, None, 6, 80, EffectType.ApprenticeCount, 1),
            N("apprentice_speed", Branch.Helpers, new[] { "apprentice_count" }, 4, 120, EffectType.ApprenticeSpeed, 0.5),
            N("apprentice_harvest_time", Branch.Helpers, new[] { "apprentice_count" }, 3, 150, EffectType.ApprenticeHarvestTime, 0.2),
            N("apprentice_yield", Branch.Helpers, new[] { "apprentice_speed", "apprentice_harvest_time" }, 4, 300, EffectType.ApprenticeYield, 0),
            N("scarecrow", Branch.Helpers, new[] { "apprentice_count" }, 2, 100, EffectType.Scarecrow, 0),
            N("tractor", Branch.Helpers, new[] { "apprentice_yield" }, 3, 1000, EffectType.Tractor, 1),
            N("helper_water", Branch.Helpers, new[] { "apprentice_harvest_time" }, 1, 500, EffectType.HelperWater, 1),

            // Calendar
            N("year_length", Branch.Calendar, None, 6, 50, EffectType.YearLength, 15),
            N("frost_warning", Branch.Calendar, new[] { "year_length" }, 2, 120, EffectType.FrostWarning, 5),
            N("late_frost", Branch.Calendar, new[] { "frost_warning" }, 1, 600, EffectType.LateFrost, 0.5),
            N("greenhouse", Branch.Calendar, new[] { "year_length" }, 3, 400, EffectType.Greenhouse, 1),
            N("crow_bounty", Branch.Calendar, new[] { "frost_warning" }, 3, 300, EffectType.CrowBounty, 0.5),
            N("spring_head_start", Branch.Calendar, new[] { "greenhouse" }, 1, 1500, EffectType.SpringHeadStart, 1),
        };


        private static Dictionary<string, SkillNode> _byId;

        public static bool TryGet(string id, out SkillNode node)
        {
            if (_byId == null)
            {
                var map = new Dictionary<string, SkillNode>();
                foreach (var n in Nodes) map[n.Id] = n;
                _byId = map;
            }
            return _byId.TryGetValue(id ?? "", out node);
        }

        public static SkillNode Get(string id) => TryGet(id, out var n) ? n : null;

        public static List<string> Validate(IReadOnlyList<SkillNode> nodes) => SkillTree.Validate(nodes);
    }
}
