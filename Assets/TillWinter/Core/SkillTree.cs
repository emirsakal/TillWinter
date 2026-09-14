using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>One row of a skill tree table (Almanac or Heritage). Adding a node is data, not code.</summary>
    public sealed class SkillNode
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

        public SkillNode(string id, Branch branch, string[] prerequisites, int maxLevel, double baseCost, double costGrowth,
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

        public bool IsImplemented => AlmanacData.IsImplemented(Effect) || HeritageData.IsImplemented(Effect);
    }

    /// <summary>
    /// Generic skill tree: a node table, current levels, a currency and the purchase rules
    /// (phase gate, prerequisites any-of, max level, cost curve with optional discount).
    /// The Almanac and the Heritage tree are two instances (GDD §6, §7).
    /// </summary>
    public sealed class SkillTree
    {
        public TreeKind Kind { get; }
        public IReadOnlyList<SkillNode> Nodes { get; }
        internal readonly Dictionary<string, int> LevelMap = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> Levels => LevelMap;

        private readonly Dictionary<string, SkillNode> _byId = new Dictionary<string, SkillNode>();

        /// <summary>Hooks supplied by the owner (FarmSim): currency, phase gate, dynamic max, discount.</summary>
        internal Func<double> GetCurrency;
        internal Action<double> Spend;
        internal Func<bool> PurchaseAllowed;
        internal Func<SkillNode, bool> IsMaxedOverride;
        internal Func<double> CostMultiplier;

        public SkillTree(TreeKind kind, IReadOnlyList<SkillNode> nodes)
        {
            Kind = kind;
            Nodes = nodes;
            foreach (var n in nodes) _byId[n.Id] = n;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);
        public SkillNode GetNode(string id) => id != null && _byId.TryGetValue(id, out var n) ? n : null;
        public int GetLevel(string id) => LevelMap.TryGetValue(id ?? "", out var l) ? l : 0;

        public double CostOf(string id)
        {
            var node = GetNode(id);
            if (node == null) return double.PositiveInfinity;
            double mult = CostMultiplier != null ? CostMultiplier() : 1.0;
            return Math.Max(0, Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, GetLevel(id)) * mult));
        }

        /// <summary>GDD §6: available when it has no prerequisites or at least one prerequisite is at level ≥ 1.</summary>
        public bool IsAvailable(string id)
        {
            var node = GetNode(id);
            if (node == null) return false;
            if (node.Prerequisites.Length == 0) return true;
            foreach (var p in node.Prerequisites)
                if (GetLevel(p) >= 1) return true;
            return false;
        }

        public bool IsMaxed(string id)
        {
            var node = GetNode(id);
            if (node == null) return true;
            if (node.MaxLevel < 0) return IsMaxedOverride == null || IsMaxedOverride(node);
            return GetLevel(id) >= node.MaxLevel;
        }

        public bool CanBuy(string id)
        {
            if (!Contains(id)) return false;
            if (PurchaseAllowed != null && !PurchaseAllowed()) return false;
            if (!IsAvailable(id) || IsMaxed(id)) return false;
            double currency = GetCurrency != null ? GetCurrency() : 0;
            return currency >= CostOf(id);
        }

        /// <summary>Spends currency and raises the level. Returns the new level, or 0 if refused.</summary>
        internal int Buy(string id)
        {
            if (!CanBuy(id)) return 0;
            Spend?.Invoke(CostOf(id));
            int level = GetLevel(id) + 1;
            LevelMap[id] = level;
            return level;
        }

        internal void SetLevel(string id, int level)
        {
            if (!Contains(id)) return;
            if (level <= 0) LevelMap.Remove(id);
            else LevelMap[id] = level;
        }

        internal void Reset() => LevelMap.Clear();

        /// <summary>Returns a list of problems (empty when valid): duplicate ids, missing prerequisites, cycles, bad numbers.</summary>
        public static List<string> Validate(IReadOnlyList<SkillNode> nodes)
        {
            var errors = new List<string>();
            var ids = new Dictionary<string, SkillNode>();
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
            var colour = new Dictionary<string, int>();
            foreach (var n in nodes)
                if (ids.ContainsKey(n.Id) && Visit(n.Id, ids, colour))
                    errors.Add("cycle through " + n.Id);
            return errors;
        }

        /// <summary>Validates both tables and checks ids are unique across them.</summary>
        public static List<string> ValidateAll(IReadOnlyList<SkillNode> almanac, IReadOnlyList<SkillNode> heritage)
        {
            var errors = Validate(almanac);
            foreach (var e in Validate(heritage)) errors.Add("heritage: " + e);
            var seen = new HashSet<string>();
            foreach (var n in almanac) seen.Add(n.Id);
            foreach (var n in heritage)
                if (!seen.Add(n.Id)) errors.Add("id used in both trees: " + n.Id);
            return errors;
        }

        private static bool Visit(string id, Dictionary<string, SkillNode> ids, Dictionary<string, int> colour)
        {
            colour.TryGetValue(id, out int c);
            if (c == 1) return true;
            if (c == 2) return false;
            colour[id] = 1;
            foreach (var p in ids[id].Prerequisites)
                if (ids.ContainsKey(p) && Visit(p, ids, colour)) return true;
            colour[id] = 2;
            return false;
        }
    }
}
