using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>One row of the Almanac table (GDD §6). Adding a node is data, not code.</summary>
    public sealed class AlmanacNode
    {
        public string Id { get; }
        public Branch Branch { get; }
        public string[] Prerequisites { get; }
        /// <summary>-1 = dynamic (upgrade_plot: until every plot is at the highest unlocked tier).</summary>
        public int MaxLevel { get; }
        public double BaseCost { get; }
        public double CostGrowth { get; }
        public EffectType Effect { get; }
        public double ValuePerLevel { get; }
        public string NameKey { get; }
        public string DescKey { get; }

        public AlmanacNode(string id, Branch branch, string[] prerequisites, int maxLevel, double baseCost, double costGrowth,
            EffectType effect, double valuePerLevel, string nameKey, string descKey)
        {
            Id = id;
            Branch = branch;
            Prerequisites = prerequisites ?? new string[0];
            MaxLevel = maxLevel;
            BaseCost = baseCost;
            CostGrowth = costGrowth;
            Effect = effect;
            ValuePerLevel = valuePerLevel;
            NameKey = nameKey;
            DescKey = descKey;
        }

        public bool IsImplemented => AlmanacData.IsImplemented(Effect);
    }

    /// <summary>Static Almanac table. Costs are placeholders (GDD §6: tune from playtests).</summary>
    public static class AlmanacData
    {
        private const double G = 1.6;
        private static readonly string[] None = new string[0];

        private static AlmanacNode N(string id, Branch b, string[] pre, int max, double cost, EffectType fx, double perLevel) =>
            new AlmanacNode(id, b, pre, max, cost, G, fx, perLevel, "almanac." + id + ".name", "almanac." + id + ".desc");

        public static readonly AlmanacNode[] Nodes =
        {
            // Hand
            N("ring_radius", Branch.Hand, None, 5, 30, EffectType.RingRadius, 0.25),
            N("ring_water_speed", Branch.Hand, new[] { "ring_radius" }, 5, 120, EffectType.RingWaterSpeed, 0.2),
            N("ring_grow_speed", Branch.Hand, new[] { "ring_radius" }, 5, 120, EffectType.RingGrowSpeed, 0.2),
            N("ring_harvest_speed", Branch.Hand, new[] { "ring_water_speed", "ring_grow_speed" }, 3, 200, EffectType.RingHarvestSpeed, 0.2),
            N("ring_bonus_coins", Branch.Hand, new[] { "ring_harvest_speed" }, 4, 400, EffectType.RingBonusCoins, 0.1),
            N("ring_combo", Branch.Hand, new[] { "ring_bonus_coins" }, 3, 800, EffectType.RingCombo, 0.05),

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

        /// <summary>Effects the sim applies today. Everything else is in the table, purchasable, but a no-op (UI shows NotImplemented).</summary>
        public static readonly HashSet<EffectType> Implemented = new HashSet<EffectType>
        {
            EffectType.RingRadius, EffectType.RingWaterSpeed, EffectType.RingGrowSpeed, EffectType.RingHarvestSpeed,
            EffectType.RingBonusCoins, EffectType.Irrigation, EffectType.Sun, EffectType.SoilQuality, EffectType.CropValue,
            EffectType.ExpandField, EffectType.UpgradePlot, EffectType.UnlockTier,
            EffectType.ApprenticeCount, EffectType.ApprenticeSpeed, EffectType.ApprenticeHarvestTime, EffectType.ApprenticeYield,
            EffectType.Scarecrow, EffectType.YearLength, EffectType.FrostWarning,
        };

        public static bool IsImplemented(EffectType effect) => Implemented.Contains(effect);

        private static Dictionary<string, AlmanacNode> _byId;

        public static bool TryGet(string id, out AlmanacNode node)
        {
            if (_byId == null)
            {
                var map = new Dictionary<string, AlmanacNode>();
                foreach (var n in Nodes) map[n.Id] = n;
                _byId = map;
            }
            return _byId.TryGetValue(id ?? "", out node);
        }

        public static AlmanacNode Get(string id) => TryGet(id, out var n) ? n : null;

        /// <summary>Returns a list of problems (empty when the table is valid): duplicate ids, missing prerequisites, cycles.</summary>
        public static List<string> Validate(IReadOnlyList<AlmanacNode> nodes)
        {
            var errors = new List<string>();
            var ids = new Dictionary<string, AlmanacNode>();
            foreach (var n in nodes)
            {
                if (string.IsNullOrEmpty(n.Id)) { errors.Add("node with empty id"); continue; }
                if (ids.ContainsKey(n.Id)) errors.Add("duplicate id: " + n.Id);
                else ids[n.Id] = n;
                if (n.MaxLevel == 0 || n.MaxLevel < -1) errors.Add(n.Id + ": invalid MaxLevel " + n.MaxLevel);
                if (n.BaseCost < 0 || n.CostGrowth <= 0) errors.Add(n.Id + ": invalid cost");
            }
            foreach (var n in nodes)
            foreach (var p in n.Prerequisites)
            {
                if (!ids.ContainsKey(p)) errors.Add(n.Id + ": unknown prerequisite " + p);
                else if (p == n.Id) errors.Add(n.Id + ": depends on itself");
            }

            // Cycle detection (DFS with colours).
            var colour = new Dictionary<string, int>();
            foreach (var n in nodes)
                if (ids.ContainsKey(n.Id) && Visit(n.Id, ids, colour))
                    errors.Add("cycle through " + n.Id);
            return errors;
        }

        private static bool Visit(string id, Dictionary<string, AlmanacNode> ids, Dictionary<string, int> colour)
        {
            colour.TryGetValue(id, out int c);
            if (c == 1) return true;  // grey: back edge
            if (c == 2) return false; // black
            colour[id] = 1;
            foreach (var p in ids[id].Prerequisites)
                if (ids.ContainsKey(p) && Visit(p, ids, colour)) return true;
            colour[id] = 2;
            return false;
        }
    }
}
