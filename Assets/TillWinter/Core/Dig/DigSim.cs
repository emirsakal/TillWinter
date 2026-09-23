using System;
using System.Collections.Generic;

namespace TillWinter.Core.Dig
{
    public enum GroundType { Clay, Stone, Roots, Gravel, Rock }
    public enum TileState { Hard, Growing, Ripe }
    public enum DigSeason { Spring, Summer, Autumn }

    /// <summary>One plot of the v3 prototype: hard ground with HP, then a growing crop, then a ripe one, then harder ground.</summary>
    public sealed class DigTile
    {
        public int X, Y;
        public TileState State;
        /// <summary>How many times this tile has been broken and reaped: the next layer is tougher and richer.</summary>
        public int Layer;
        public GroundType Ground;
        public bool Golden, Chest;
        public double Hp, MaxHp;
        /// <summary>What the break pays, decided when the layer appears and hidden until it breaks (GDD §2v3.3).</summary>
        public double HiddenBonus;
        public int Crop;
        public float Growth;
        public float RipeAge;
        public bool Watering;
        public bool Crow;
        public float CrowTimer;
    }

    /// <summary>
    /// The core-loop v3 prototype (GDD §2v3): strike hard ground with a timed hit, let the crop grow, reap it with a
    /// swipe, and meet the next layer. Pure C#, deterministic for a fixed dt and seed, no strings. The bots in
    /// <see cref="DigBots"/> drive it; the acceptance test compares them. Not wired to the shipped game.
    /// </summary>
    public sealed class DigSim
    {
        public readonly DigConfig Config;
        public readonly DigTile[] Tiles;
        public readonly Rng Rng;

        public double Coins { get; private set; }
        public float Stamina { get; private set; }
        public float YearTime { get; private set; }
        public int Year { get; private set; } = 1;
        public bool Winter { get; private set; }
        public float StrikeCooldownLeft { get; private set; }
        /// <summary>Whether the strike that just raised <see cref="Struck"/> was a tired one (presentation reads it in the handler).</summary>
        public bool LastStrikeTired { get; private set; }

        // Upgradable stats (winter purchases)
        public double Damage;
        public float CritWindow;
        public double CritChance;
        public float StaminaMax;
        public float StaminaRegen;
        public float GrowthMult = 1f;
        public int MaxTier;
        public int DamageLevel, CritLevel, CritChanceLevel, StaminaLevel, RegenLevel, GrowthLevel;

        // Counters for the tables
        public int Strikes, Crits, TiredStrikes, Breaks, Reaped, FrostReaped, CrowsEaten, CrowsScared, Chests, Golds;
        public double CoinsThisYear, CoinsFromBreaks, CoinsFromCrops;
        public float SecondsStaminaEmpty;
        public int Swipes;

        public event Action<DigTile, double, bool> Struck;
        public event Action<DigTile, double> Broke;
        public event Action<DigTile> Ripened;
        public event Action<int, double> ReapedEvent;
        public event Action<DigTile> CrowLanded, CrowAte;
        public event Action<int> YearEnded;

        public DigSim(DigConfig config, int seed)
        {
            Config = config;
            Rng = new Rng(seed);
            Damage = config.Damage;
            CritWindow = config.CritWindow;
            CritChance = config.BaseCritChance;
            StaminaMax = config.StaminaMax;
            StaminaRegen = config.StaminaRegen;
            MaxTier = config.MaxTier;
            Stamina = StaminaMax;
            Tiles = new DigTile[config.GridSize * config.GridSize];
            for (int i = 0; i < Tiles.Length; i++)
            {
                Tiles[i] = new DigTile { X = i % config.GridSize, Y = i / config.GridSize, Layer = -1 };
                NewLayer(Tiles[i]);
            }
        }

        public DigSeason Season => YearTime < Config.YearLength / 3f ? DigSeason.Spring : YearTime < 2f * Config.YearLength / 3f ? DigSeason.Summer : DigSeason.Autumn;
        /// <summary>0..1 through the pulse; the beat is at 0.5 (GDD §2v3.4).</summary>
        public float Pulse => (YearTime % Config.PulseSeconds) / Config.PulseSeconds;
        public bool OnBeat => Math.Abs(Pulse - 0.5f) <= CritWindow * 0.5f;
        /// <summary>Stamina no longer gates a strike: without it the swing is tired (GDD §2v3.5), it still lands.</summary>
        public bool CanStrike => !Winter && StrikeCooldownLeft <= 0f;
        public bool Tired => Stamina < Config.StrikeCost;
        public int CrowCount { get { int n = 0; foreach (var t in Tiles) if (t.Crow) n++; return n; } }
        public DigCrop CropOf(DigTile t) => Config.Crops[Math.Min(t.Crop, Config.Crops.Length - 1)];
        public double CropValue(DigTile t) => CropOf(t).Value * (1 + Config.DepthValue * t.Layer);
        /// <summary>Hits a strike of the current damage needs on this tile, no crits (what a planning bot can see).</summary>
        public int HitsLeft(DigTile t) => (int)Math.Ceiling(t.Hp / Math.Max(1e-6, Damage * SeasonDamage()));

        private float SeasonDamage() => Season == DigSeason.Spring ? Config.SpringSoftness : Season == DigSeason.Summer ? Config.SummerHardness : 1f;

        // ------------------------------------------------------------------ the layer under the crop

        private void NewLayer(DigTile t)
        {
            t.Layer++;
            t.State = TileState.Hard;
            t.Golden = Rng.NextDouble() < Config.GoldChance;
            t.Chest = !t.Golden && Rng.NextDouble() < Config.ChestChance;
            t.Ground = t.Layer < 2 ? GroundType.Clay : t.Layer < 4 ? GroundType.Stone : t.Layer < 6 ? GroundType.Roots : t.Layer < 8 ? GroundType.Gravel : GroundType.Rock;
            t.MaxHp = Config.BaseHp * Math.Pow(Config.HpGrowth, t.Layer) * (t.Golden ? Config.GoldHpMult : 1);
            t.Hp = t.MaxHp;
            double bonus = Config.BreakCoins * Math.Pow(Config.BreakGrowth, t.Layer);
            if (t.Golden) bonus *= Config.GoldCoinsMult;
            else if (t.Chest) bonus *= Config.ChestMult;
            t.HiddenBonus = bonus;
            t.Growth = 0f;
            t.RipeAge = 0f;
            t.Watering = false;
            t.Crow = false;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// A short press on a hard tile. A rested swing crits on the beat for certain and off the beat by chance; a tired
        /// swing (no stamina) lands at <see cref="DigConfig.TiredDamage"/> and never crits. False if nothing happened.
        /// </summary>
        public bool Strike(DigTile t)
        {
            if (t.State != TileState.Hard || !CanStrike) return false;
            bool tired = Tired;
            if (!tired) Stamina -= Config.StrikeCost;
            StrikeCooldownLeft = Config.StrikeCooldown;
            bool crit = !tired && (OnBeat || Rng.NextDouble() < CritChance);
            double dmg = Damage * SeasonDamage() * (crit ? Config.CritMult : 1) * (tired ? Config.TiredDamage : 1);
            Strikes++;
            if (crit) Crits++;
            if (tired) TiredStrikes++;
            LastStrikeTired = tired;
            t.Hp -= dmg;
            Struck?.Invoke(t, dmg, crit);
            if (t.Hp <= 0) Break(t);
            return true;
        }

        private void Break(DigTile t)
        {
            Breaks++;
            if (t.Chest) Chests++;
            if (t.Golden) Golds++;
            AddCoins(t.HiddenBonus, true);
            Broke?.Invoke(t, t.HiddenBonus);
            // The seed drops by itself: the tile's chosen crop, the best unlocked (GDD §2v3.6).
            t.Crop = MaxTier;
            t.State = TileState.Growing;
            t.Growth = 0f;
            t.Hp = 0;
        }

        /// <summary>Holding on a growing tile: ×WaterBoost growth while the hold costs stamina each second.</summary>
        public void SetWatering(DigTile t, bool holding)
        {
            t.Watering = holding && t.State == TileState.Growing && !Winter;
        }

        /// <summary>A swipe: every ripe tile on the path is reaped, n in one swipe pays the combo (GDD §2v3.7). Free.</summary>
        public double Reap(IList<DigTile> path)
        {
            if (Winter && !_frost) return 0;
            int n = 0;
            double sum = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var t = path[i];
                if (t.State != TileState.Ripe) continue;
                n++;
                sum += CropValue(t);
                t.Crow = false;
                Stamina = Math.Min(StaminaMax, Stamina + Config.ReapStamina);
                Reaped++;
                NewLayer(t);
            }
            if (n == 0) return 0;
            Swipes++;
            double combo = 1 + Config.ComboPerCrop * (Season == DigSeason.Autumn ? Config.AutumnCombo : 1f) * (n - 1);
            double coins = sum * combo;
            AddCoins(coins, false);
            ReapedEvent?.Invoke(n, coins);
            return coins;
        }

        /// <summary>Tapping a crow scares it and reaps the crop under it (GDD §2v3.8).</summary>
        public bool TapCrow(DigTile t)
        {
            if (!t.Crow || t.State != TileState.Ripe) return false;
            CrowsScared++;
            t.Crow = false;
            Reap(new[] { t });
            return true;
        }

        private void AddCoins(double c, bool fromBreak)
        {
            Coins += c;
            CoinsThisYear += c;
            if (fromBreak) CoinsFromBreaks += c; else CoinsFromCrops += c;
        }

        // ------------------------------------------------------------------ time

        public void Tick(float dt)
        {
            if (Winter || dt <= 0f) return;
            YearTime += dt;
            if (StrikeCooldownLeft > 0f) StrikeCooldownLeft -= dt;
            float growthSeason = Season == DigSeason.Summer ? Config.SummerGrowth : 1f;
            if (Stamina <= 0f) SecondsStaminaEmpty += dt;
            Stamina = Math.Min(StaminaMax, Stamina + StaminaRegen * dt);
            foreach (var t in Tiles)
            {
                switch (t.State)
                {
                    case TileState.Growing:
                    {
                        float boost = 1f;
                        if (t.Watering)
                        {
                            float cost = Config.WaterCostPerSecond * dt;
                            if (Stamina >= cost) { Stamina -= cost; boost = Config.WaterBoost; }
                            else t.Watering = false;
                        }
                        t.Growth += dt * boost * growthSeason * GrowthMult / Math.Max(0.01f, CropOf(t).Grow);
                        if (t.Growth >= 1f)
                        {
                            t.State = TileState.Ripe;
                            t.RipeAge = 0f;
                            t.Watering = false;
                            Ripened?.Invoke(t);
                        }
                        break;
                    }
                    case TileState.Ripe:
                    {
                        t.RipeAge += dt;
                        if (t.Crow)
                        {
                            t.CrowTimer -= dt;
                            if (t.CrowTimer <= 0f)
                            {
                                // Eaten: the crop is gone, the ground comes back one layer deeper all the same.
                                CrowsEaten++;
                                t.Crow = false;
                                CrowAte?.Invoke(t);
                                NewLayer(t);
                            }
                        }
                        else if (t.RipeAge >= Config.CrowMinRipe && CrowCount < Config.MaxCrows && Rng.NextDouble() < Config.CrowChancePerSecond * dt)
                        {
                            t.Crow = true;
                            t.CrowTimer = Config.CrowWait;
                            CrowLanded?.Invoke(t);
                        }
                        break;
                    }
                }
            }
            if (YearTime >= Config.YearLength) EnterWinter();
        }

        /// <summary>
        /// Frost (GDD §2v3.2 v3.2): whatever is ripe is reaped by itself, one tile at a time so no combo is paid; a
        /// growing crop waits for spring; hard ground keeps its layer but the winter closes every half-made crack —
        /// HP refills. Nothing else changes; the depth is the farm's memory between years.
        /// </summary>
        private bool _frost;

        private void EnterWinter()
        {
            Winter = true;
            _frost = true;
            foreach (var t in Tiles)
            {
                t.Watering = false;
                t.Crow = false;
                if (t.State == TileState.Ripe) { FrostReaped++; Reap(new[] { t }); }
                if (t.State == TileState.Hard) t.Hp = t.MaxHp;
            }
            _frost = false;
            YearEnded?.Invoke(Year);
        }

        /// <summary>Rebirth: the field returns to the surface — every tile a fresh first layer, nothing growing.</summary>
        public void ResetField()
        {
            foreach (var t in Tiles)
            {
                t.Layer = -1;
                NewLayer(t);
            }
        }

        /// <summary>Spring: the clock restarts; layers, crops and coins are kept (winter carries no penalty, GDD §2v3.2).</summary>
        public void StartNextYear()
        {
            if (!Winter) return;
            Winter = false;
            Year++;
            YearTime = 0f;
            CoinsThisYear = 0;
            Stamina = StaminaMax;
        }

        // ------------------------------------------------------------------ winter shop (the same for every bot)

        public enum Upgrade { Damage, Crit, CritChance, Stamina, Regen, Growth, Tier }

        public double CostOf(Upgrade u)
        {
            var c = Config;
            switch (u)
            {
                case Upgrade.Damage: return c.UpgradeDamageCost * Math.Pow(c.UpgradeDamageGrowth, DamageLevel);
                case Upgrade.Crit: return CritWindow >= c.MaxCritWindow ? double.PositiveInfinity : c.UpgradeCritCost * Math.Pow(c.UpgradeCritGrowth, CritLevel);
                case Upgrade.CritChance: return CritChance >= c.MaxCritChance ? double.PositiveInfinity : c.UpgradeCritChanceCost * Math.Pow(c.UpgradeCritChanceGrowth, CritChanceLevel);
                case Upgrade.Stamina: return c.UpgradeStaminaCost * Math.Pow(c.UpgradeStaminaGrowth, StaminaLevel);
                case Upgrade.Regen: return c.UpgradeRegenCost * Math.Pow(c.UpgradeRegenGrowth, RegenLevel);
                case Upgrade.Growth: return c.UpgradeGrowthCost * Math.Pow(c.UpgradeGrowthGrowth, GrowthLevel);
                default: return MaxTier >= c.Crops.Length - 1 ? double.PositiveInfinity : c.UpgradeTierCost * Math.Pow(c.UpgradeTierGrowth, MaxTier);
            }
        }

        public bool Buy(Upgrade u)
        {
            if (!Winter) return false;
            double cost = CostOf(u);
            if (double.IsInfinity(cost) || Coins < cost) return false;
            Coins -= cost;
            var c = Config;
            switch (u)
            {
                case Upgrade.Damage: Damage += c.UpgradeDamage; DamageLevel++; break;
                case Upgrade.Crit: CritWindow = Math.Min(c.MaxCritWindow, CritWindow + c.UpgradeCritWindow); CritLevel++; break;
                case Upgrade.CritChance: CritChance = Math.Min(c.MaxCritChance, CritChance + c.UpgradeCritChance); CritChanceLevel++; break;
                case Upgrade.Stamina: StaminaMax += c.UpgradeStamina; StaminaLevel++; break;
                case Upgrade.Regen: StaminaRegen += c.UpgradeRegen; RegenLevel++; break;
                case Upgrade.Growth: GrowthMult += c.UpgradeGrowth; GrowthLevel++; break;
                case Upgrade.Tier: MaxTier++; break;
            }
            return true;
        }

        /// <summary>The one shopping rule every bot shares: buy the cheapest affordable upgrade until nothing is.</summary>
        public int ShopGreedy()
        {
            int bought = 0;
            var all = (Upgrade[])Enum.GetValues(typeof(Upgrade));
            while (true)
            {
                Upgrade best = Upgrade.Damage;
                double bestCost = double.PositiveInfinity;
                foreach (var u in all)
                {
                    double cost = CostOf(u);
                    if (cost < bestCost) { bestCost = cost; best = u; }
                }
                if (double.IsInfinity(bestCost) || Coins < bestCost) return bought;
                Buy(best);
                bought++;
            }
        }
    }
}
