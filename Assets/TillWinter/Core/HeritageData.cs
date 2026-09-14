using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>Static Heritage table (GDD §7). Costs in Heritage Seeds, placeholders to tune. Never reset.</summary>
    public static class HeritageData
    {
        private const double G = 1.5;
        private static readonly string[] None = new string[0];

        private static SkillNode N(string id, Branch b, string[] pre, int max, double cost, EffectType fx, double perLevel) =>
            new SkillNode(id, b, pre, max, cost, G, fx, perLevel, "heritage." + id + ".name", "heritage." + id + ".desc");

        public static readonly SkillNode[] Nodes =
        {
            // Hand
            N("h_start_radius", Branch.Hand, None, 3, 3, EffectType.HeritageStartRadius, 0.25),
            N("h_ring_speeds", Branch.Hand, new[] { "h_start_radius" }, 5, 4, EffectType.HeritageRingSpeeds, 0.10),
            N("h_ring_coins", Branch.Hand, new[] { "h_ring_speeds" }, 4, 6, EffectType.HeritageRingCoins, 0.05),

            // Soil
            N("h_start_irrigation", Branch.Soil, None, 1, 3, EffectType.HeritageStartIrrigation, 1),
            N("h_start_sun", Branch.Soil, new[] { "h_start_irrigation" }, 1, 4, EffectType.HeritageStartSun, 1),
            N("h_global_growth", Branch.Soil, new[] { "h_start_sun" }, 5, 5, EffectType.HeritageGlobalGrowth, 0.05),
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
        };

        /// <summary>Heritage effects the sim applies today; the rest are stored as flags/values for Session 3.</summary>
        public static readonly HashSet<EffectType> Implemented = new HashSet<EffectType>
        {
            EffectType.HeritageStartRadius, EffectType.HeritageRingSpeeds, EffectType.HeritageRingCoins,
            EffectType.HeritageStartIrrigation, EffectType.HeritageStartSun, EffectType.HeritageGlobalGrowth,
            EffectType.HeritageStartField, EffectType.HeritageStartTomato,
            EffectType.FreeApprentice, EffectType.HeritageApprenticeYield,
            EffectType.HeritageStartYearLength, EffectType.AlmanacDiscount,
        };

        public static bool IsImplemented(EffectType effect) => Implemented.Contains(effect);

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
