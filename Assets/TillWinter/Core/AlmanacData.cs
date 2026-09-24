using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Static Almanac table (GDD §6, re-themed for the hoe in v3: §2v3.11). Costs in coins. Reset on retire.</summary>
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
            { "hoe_damage", "arrowUp" },
            { "steady_hand", "target" },
            { "lucky_hoe", "star" },
            { "strike_speed", "fastForward" },
            { "splash", "share1" },
            { "stamina_depot", "barsVertical" },
            { "stamina_regen", "forward" },
            { "break_bonus", "medal1" },
            { "reap_combo", "leaderboardsSimple" },
            { "growth", "contrast" },
            { "soft_ground", "import" },
            { "soil_quality", "signal1" },
            { "crop_value", "medal2" },
            { "early_thaw", "power" },
            { "expand_field", "larger" },
            { "unlock_tomato", "plus" },
            { "upgrade_plot", "next" },
            { "unlock_corn", "signal2" },
            { "unlock_pumpkin", "checkmark" },
            { "unlock_grapes", "zoomIn" },
            { "unlock_golden_wheat", "trophy" },
            { "bulk_upgrade", "menuGrid" },
            { "apprentice_count", "multiplayer" },
            { "apprentice_speed", "joystick" },
            { "apprentice_work_time", "basket" },
            { "apprentice_yield", "cart" },
            { "apprentice_dig", "zoom" },
            { "scarecrow", "warning" },
            { "tractor", "gear" },
            { "farm_dog", "singleplayer" },
            { "beehive", "export" },
            { "hens", "basket" },
            { "barn", "home" },
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
            // Hand: the hoe (GDD §2v3.4–5)
            N("hoe_damage", Branch.Hand, None, 8, 120, EffectType.StrikeDamage, 1),
            N("stamina_depot", Branch.Hand, new[] { "hoe_damage" }, 5, 240, EffectType.StaminaMax, 15),
            N("stamina_regen", Branch.Hand, new[] { "stamina_depot" }, 5, 480, EffectType.StaminaRegen, 0.25),
            N("steady_hand", Branch.Hand, new[] { "hoe_damage" }, 3, 600, EffectType.CritWindow, 0.03),
            N("lucky_hoe", Branch.Hand, new[] { "hoe_damage" }, 6, 480, EffectType.CritChance, 0.03),
            N("strike_speed", Branch.Hand, new[] { "steady_hand", "lucky_hoe" }, 4, 1000, EffectType.StrikeSpeed, 0.04),
            N("splash", Branch.Hand, new[] { "strike_speed" }, 2, 2800, EffectType.Splash, 0.25),
            N("break_bonus", Branch.Hand, new[] { "strike_speed" }, 4, 1600, EffectType.BreakBonus, 0.1),
            N("reap_combo", Branch.Hand, new[] { "break_bonus" }, 3, 3200, EffectType.ReapCombo, 0.02),

            // Soil
            N("growth", Branch.Soil, None, 5, 160, EffectType.Growth, 0.15),
            N("soft_ground", Branch.Soil, new[] { "growth" }, 5, 600, EffectType.Softness, 0.06),
            N("soil_quality", Branch.Soil, new[] { "soft_ground" }, 6, 1000, EffectType.SoilQuality, 0.25),
            N("crop_value", Branch.Soil, new[] { "soil_quality" }, 5, 2000, EffectType.CropValue, 0.1),
            N("early_thaw", Branch.Soil, new[] { "soil_quality" }, 1, 2400, EffectType.EarlyThaw, 1),
            N("beehive", Branch.Soil, new[] { "growth" }, 1, 1800, EffectType.Beehive, 1),

            // Field
            N("expand_field", Branch.Field, None, 3, 240, EffectType.ExpandField, 1),
            N("unlock_tomato", Branch.Field, new[] { "expand_field" }, 1, 400, EffectType.UnlockTier, 1),
            N("upgrade_plot", Branch.Field, new[] { "unlock_tomato" }, -1, 100, EffectType.UpgradePlot, 1),
            N("unlock_corn", Branch.Field, new[] { "upgrade_plot" }, 1, 1200, EffectType.UnlockTier, 2),
            N("unlock_pumpkin", Branch.Field, new[] { "unlock_corn" }, 1, 2800, EffectType.UnlockTier, 3),
            N("unlock_grapes", Branch.Field, new[] { "unlock_pumpkin" }, 1, 4800, EffectType.UnlockTier, 4),
            N("unlock_golden_wheat", Branch.Field, new[] { "unlock_grapes" }, 1, 8000, EffectType.UnlockTier, 5),
            N("bulk_upgrade", Branch.Field, new[] { "unlock_corn" }, 1, 3600, EffectType.BulkUpgrade, 1),
            N("barn", Branch.Field, new[] { "expand_field" }, 3, 1000, EffectType.Barn, 1),

            // Helpers (GDD §2v3.9)
            N("apprentice_count", Branch.Helpers, None, 6, 320, EffectType.ApprenticeCount, 1),
            N("apprentice_speed", Branch.Helpers, new[] { "apprentice_count" }, 4, 480, EffectType.ApprenticeSpeed, 0.5),
            N("apprentice_work_time", Branch.Helpers, new[] { "apprentice_count" }, 3, 600, EffectType.ApprenticeWorkTime, 0.2),
            N("apprentice_yield", Branch.Helpers, new[] { "apprentice_speed", "apprentice_work_time" }, 4, 1200, EffectType.ApprenticeYield, 0),
            N("apprentice_dig", Branch.Helpers, new[] { "apprentice_work_time" }, 3, 2000, EffectType.ApprenticeDig, 0.25),
            N("scarecrow", Branch.Helpers, new[] { "apprentice_count" }, 2, 400, EffectType.Scarecrow, 0),
            N("tractor", Branch.Helpers, new[] { "apprentice_yield" }, 3, 4000, EffectType.Tractor, 1),
            N("farm_dog", Branch.Helpers, new[] { "scarecrow" }, 1, 1600, EffectType.FarmDog, 1),
            N("hens", Branch.Helpers, new[] { "farm_dog" }, 1, 2000, EffectType.Hens, 1),

            // Calendar
            N("year_length", Branch.Calendar, None, 6, 200, EffectType.YearLength, 15),
            N("frost_warning", Branch.Calendar, new[] { "year_length" }, 2, 480, EffectType.FrostWarning, 5),
            N("late_frost", Branch.Calendar, new[] { "frost_warning" }, 1, 2400, EffectType.LateFrost, 0.5),
            N("greenhouse", Branch.Calendar, new[] { "year_length" }, 3, 1600, EffectType.Greenhouse, 1),
            N("crow_bounty", Branch.Calendar, new[] { "frost_warning" }, 3, 1200, EffectType.CrowBounty, 0.5),
            N("spring_head_start", Branch.Calendar, new[] { "greenhouse" }, 1, 6000, EffectType.SpringHeadStart, 1),
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
