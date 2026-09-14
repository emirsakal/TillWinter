using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// The whole game. Pure C#, deterministic for a fixed dt and seed. The Unity layer calls
    /// <see cref="Tick"/> every frame, forwards input via <see cref="Tick"/>/<see cref="TapAt"/>,
    /// buys Almanac nodes with <see cref="TryBuy"/>, and renders <see cref="State"/>.
    /// </summary>
    public sealed class FarmSim
    {
        public FarmConfig Config { get; }
        public FarmState State { get; }
        public IReadOnlyList<AlmanacNode> Nodes { get; }

        public event Action<HarvestEvent> Harvested;
        public event Action<GridPos> PlotWatered;
        public event Action<GridPos> PlotRipened;
        public event Action<CrowEvent> CrowLanded;
        public event Action<CrowEvent> CrowScared;
        public event Action<CrowEvent> CrowAte;
        public event Action<Season> SeasonChanged;
        public event Action WinterStarted;
        public event Action FrostWarningStarted;
        public event Action YearStarted;
        public event Action<PurchaseEvent> Purchased;
        public event Action FieldExpanded;

        private readonly Random _rng;
        private readonly List<Plot> _scratch = new List<Plot>();
        private float _crowSpawnTimer;
        private float? _ringRadiusOverride;

        public FarmSim(FarmConfig config, int seed, IReadOnlyList<AlmanacNode> nodes = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Nodes = nodes ?? AlmanacData.Nodes;
            var errors = AlmanacData.Validate(Nodes);
            if (errors.Count > 0) throw new InvalidOperationException("Almanac table invalid: " + string.Join("; ", errors));
            State = new FarmState();
            _rng = new Random(seed);
            BuildField(config.StartGridSize);
            ResolveStats();
        }

        // ------------------------------------------------------------------ public API

        /// <param name="dt">Seconds. Already scaled by any debug time scale.</param>
        /// <param name="ring">Ring centre in plot space, or null when the finger is up.</param>
        public void Tick(float dt, RingInput? ring)
        {
            if (dt < 0f) dt = 0f;
            State.Ring = State.IsWinter ? null : ring;
            if (State.IsWinter) return;

            AdvanceYear(dt);
            if (State.IsWinter) return;

            UpdatePlots(dt);
            UpdateApprentices(dt);
            UpdateCrows(dt);
        }

        /// <summary>Tap a plot. Scares a crow (dropping coins) if one is there. Returns true if something happened.</summary>
        public bool TapAt(GridPos pos)
        {
            if (State.IsWinter || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (!plot.HasCrow) return false;
            double coins = Config.CrowScareValueMultiplier * Crop(plot).Value;
            AddCoins(coins);
            RemoveCrowAt(pos);
            CrowScared?.Invoke(new CrowEvent(pos, coins));
            return true;
        }

        public AlmanacNode GetNode(string id)
        {
            foreach (var n in Nodes) if (n.Id == id) return n;
            return null;
        }

        public double CostOf(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return double.PositiveInfinity;
            return Math.Round(node.BaseCost * Math.Pow(node.CostGrowth, State.GetLevel(nodeId)));
        }

        public int GetMaxLevel(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return 0;
            if (node.Effect == EffectType.UpgradePlot)
                return State.GridSize * State.GridSize * State.Stats.MaxTierUnlocked;
            return node.MaxLevel;
        }

        public bool IsMaxed(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return true;
            if (node.Effect == EffectType.UpgradePlot) return FindLowestUpgradablePlot() == null;
            return State.GetLevel(nodeId) >= node.MaxLevel;
        }

        /// <summary>GDD §6: available when it has no prerequisites or at least one prerequisite is at level ≥ 1.</summary>
        public bool IsAvailable(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return false;
            if (node.Prerequisites.Length == 0) return true;
            foreach (var p in node.Prerequisites)
                if (State.GetLevel(p) >= 1) return true;
            return false;
        }

        public bool CanBuy(string nodeId) =>
            State.IsWinter && IsAvailable(nodeId) && !IsMaxed(nodeId) && State.Coins >= CostOf(nodeId);

        public bool TryBuy(string nodeId)
        {
            if (!CanBuy(nodeId)) return false;
            var node = GetNode(nodeId);
            State.Coins -= CostOf(nodeId);
            int level = State.GetLevel(nodeId) + 1;
            State.LevelMap[nodeId] = level;
            ApplyPurchase(node);
            ResolveStats();
            Purchased?.Invoke(new PurchaseEvent(nodeId, level));
            return true;
        }

        /// <summary>Leave the winter Almanac and start Spring of the next year.</summary>
        public void StartNextYear()
        {
            if (!State.IsWinter) return;
            State.Year++;
            State.YearTime = 0f;
            State.FrostWarning = false;
            State.Season = Season.Spring;
            _crowSpawnTimer = 0f;
            ClearCrows();
            foreach (var p in State.PlotArray) p.Reset();
            ResetApprentices();
            SeasonChanged?.Invoke(Season.Spring);
            YearStarted?.Invoke();
        }

        // ------------------------------------------------------------------ debug hooks

        public void DebugAddCoins(double amount) => State.Coins += amount;

        public void DebugSkipToWinter()
        {
            if (!State.IsWinter) EnterWinter();
        }

        /// <summary>Force a crow onto a plot (ripening it if needed). Ignores year/scarecrow rules.</summary>
        public bool DebugSpawnCrow()
        {
            if (State.IsWinter) return false;
            _scratch.Clear();
            foreach (var p in State.PlotArray)
                if (!p.HasCrow && p.IsRipe && !State.IsUnderRing(p.Pos)) _scratch.Add(p);
            if (_scratch.Count == 0)
                foreach (var p in State.PlotArray)
                    if (!p.HasCrow) _scratch.Add(p);
            if (_scratch.Count == 0) return false;
            var plot = _scratch[_rng.Next(_scratch.Count)];
            plot.State = PlotState.Ripe;
            plot.Progress = 0f;
            SpawnCrowAt(plot.Pos);
            return true;
        }

        public void DebugForceRipeAll()
        {
            if (State.IsWinter) return;
            foreach (var p in State.PlotArray)
            {
                p.State = PlotState.Ripe;
                p.Progress = 0f;
            }
        }

        /// <summary>Sets a node level directly (clamped to its max), applying field/tier side effects.</summary>
        public void DebugSetLevel(string nodeId, int level)
        {
            var node = GetNode(nodeId);
            if (node == null) return;
            int max = node.MaxLevel < 0 ? int.MaxValue : node.MaxLevel;
            level = Math.Max(0, Math.Min(max, level));
            State.LevelMap[nodeId] = level;
            ResolveStats();
            if (node.Effect == EffectType.ExpandField && State.GridSize != State.Stats.TargetGridSize)
            {
                BuildField(State.Stats.TargetGridSize);
                FieldExpanded?.Invoke();
            }
        }

        /// <summary>null restores the Almanac-driven radius.</summary>
        public void DebugSetRingRadiusOverride(float? radius)
        {
            _ringRadiusOverride = radius;
            ResolveStats();
        }

        public float? DebugRingRadiusOverride => _ringRadiusOverride;

        // ------------------------------------------------------------------ year

        private void AdvanceYear(float dt)
        {
            State.YearTime += dt;
            float length = State.Stats.YearLength;

            if (!State.FrostWarning && State.YearTime >= length - State.Stats.FrostWarningSeconds)
            {
                State.FrostWarning = true;
                FrostWarningStarted?.Invoke();
            }

            if (State.YearTime >= length)
            {
                EnterWinter();
                return;
            }

            var season = SeasonAt(State.YearTime, length);
            if (season != State.Season)
            {
                State.Season = season;
                SeasonChanged?.Invoke(season);
            }
        }

        private static Season SeasonAt(float t, float length)
        {
            float f = t / length;
            if (f < 1f / 3f) return Season.Spring;
            if (f < 2f / 3f) return Season.Summer;
            return Season.Autumn;
        }

        private void EnterWinter()
        {
            State.Season = Season.Winter;
            State.YearTime = State.Stats.YearLength;
            State.FrostWarning = false;
            State.Ring = null;
            ClearCrows();
            foreach (var p in State.PlotArray) p.Reset();
            ResetApprentices();
            SeasonChanged?.Invoke(Season.Winter);
            WinterStarted?.Invoke();
        }

        // ------------------------------------------------------------------ plots

        private CropDef Crop(Plot plot) => Config.Crops[Math.Max(0, Math.Min(Config.MaxTier, plot.Tier))];

        private void UpdatePlots(float dt)
        {
            var st = State.Stats;
            foreach (var plot in State.PlotArray)
            {
                bool under = State.IsUnderRing(plot.Pos);
                var crop = Crop(plot);
                switch (plot.State)
                {
                    case PlotState.Dry:
                    {
                        // Ring rate replaces the passive rate; passive uses the crop's base speed (no ring upgrades).
                        float speed = under ? st.RingWaterMult : st.IrrigationFactor;
                        if (speed <= 0f) break;
                        plot.Progress += speed / crop.Water * dt;
                        if (plot.Progress >= 1f)
                        {
                            plot.State = PlotState.Wet;
                            plot.Progress = 0f;
                            PlotWatered?.Invoke(plot.Pos);
                        }
                        break;
                    }
                    case PlotState.Wet:
                    {
                        float speed = (under ? st.RingGrowMult : st.SunFactor) * st.SoilMultiplier;
                        if (speed <= 0f) break;
                        plot.Progress += speed / crop.Grow * dt;
                        if (plot.Progress >= 1f)
                        {
                            plot.State = PlotState.Ripe;
                            plot.Progress = 0f;
                            PlotRipened?.Invoke(plot.Pos);
                        }
                        break;
                    }
                    case PlotState.Ripe:
                    {
                        if (!under) break;
                        plot.Progress += st.RingHarvestMult / crop.Harvest * dt;
                        if (plot.Progress >= 1f)
                            Harvest(plot, HarvestSource.Ring, -1);
                        break;
                    }
                }
            }
        }

        private void Harvest(Plot plot, HarvestSource source, int apprenticeIndex)
        {
            var st = State.Stats;
            double coins = Crop(plot).Value * st.CropValueMult;
            coins *= source == HarvestSource.Ring ? st.RingBonusMult : st.ApprenticeYield;
            AddCoins(coins);
            int tier = plot.Tier;
            plot.Reset();
            if (plot.HasCrow)
            {
                RemoveCrowAt(plot.Pos);
                CrowScared?.Invoke(new CrowEvent(plot.Pos, 0));
            }
            Harvested?.Invoke(new HarvestEvent(plot.Pos, tier, coins, source, apprenticeIndex));
        }

        private void AddCoins(double coins)
        {
            State.Coins += coins;
            State.LifetimeCoins += coins;
        }

        // ------------------------------------------------------------------ apprentices

        private void SyncApprenticeCount()
        {
            var list = State.ApprenticeList;
            int want = State.Stats.ApprenticeCount;
            while (list.Count < want)
            {
                var a = new ApprenticeState { Index = list.Count };
                list.Add(a);
                PlaceIdle(a);
                a.X = a.IdleX;
                a.Y = a.IdleY;
            }
            while (list.Count > want) list.RemoveAt(list.Count - 1);
            foreach (var a in list) PlaceIdle(a);
        }

        private void PlaceIdle(ApprenticeState a)
        {
            int count = Math.Max(1, State.ApprenticeList.Count);
            float span = State.GridSize - 1f;
            a.IdleX = count == 1 ? span * 0.5f : span * a.Index / (count - 1f);
            a.IdleY = -Config.ApprenticeIdleOffset;
        }

        private void ResetApprentices()
        {
            foreach (var a in State.ApprenticeList)
            {
                PlaceIdle(a);
                a.X = a.IdleX;
                a.Y = a.IdleY;
                a.HasTarget = false;
                a.IsHarvesting = false;
                a.HarvestProgress = 0f;
                a.IsWalking = false;
            }
        }

        private bool IsTargetedByOther(GridPos pos, ApprenticeState self)
        {
            foreach (var o in State.ApprenticeList)
                if (o != self && o.HasTarget && o.Target == pos) return true;
            return false;
        }

        private void UpdateApprentices(float dt)
        {
            var st = State.Stats;
            foreach (var a in State.ApprenticeList)
            {
                if (a.HasTarget && !State.GetPlot(a.Target).IsRipe)
                {
                    // The ring (or another helper) took it first.
                    a.HasTarget = false;
                    a.IsHarvesting = false;
                    a.HarvestProgress = 0f;
                }

                if (!a.HasTarget)
                {
                    Plot best = null;
                    float bestDist = float.MaxValue;
                    foreach (var p in State.PlotArray)
                    {
                        if (!p.IsRipe || IsTargetedByOther(p.Pos, a)) continue;
                        float dx = p.Pos.X - a.X, dy = p.Pos.Y - a.Y;
                        float d = dx * dx + dy * dy;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = p;
                        }
                    }
                    if (best != null)
                    {
                        a.Target = best.Pos;
                        a.HasTarget = true;
                    }
                }

                if (a.IsHarvesting)
                {
                    float time = st.ApprenticeHarvestTime;
                    a.HarvestProgress += time <= 0f ? 1f : dt / time;
                    a.IsWalking = false;
                    if (a.HarvestProgress >= 1f)
                    {
                        Harvest(State.GetPlot(a.Target), HarvestSource.Apprentice, a.Index);
                        a.IsHarvesting = false;
                        a.HasTarget = false;
                        a.HarvestProgress = 0f;
                    }
                    continue;
                }

                float tx = a.HasTarget ? a.Target.X : a.IdleX;
                float ty = a.HasTarget ? a.Target.Y : a.IdleY;
                float ddx = tx - a.X, ddy = ty - a.Y;
                float dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                float step = st.ApprenticeSpeed * dt;
                if (dist <= step || dist < 1e-4f)
                {
                    a.X = tx;
                    a.Y = ty;
                    a.IsWalking = false;
                    if (a.HasTarget)
                    {
                        a.IsHarvesting = true;
                        a.HarvestProgress = 0f;
                    }
                }
                else
                {
                    a.X += ddx / dist * step;
                    a.Y += ddy / dist * step;
                    a.IsWalking = true;
                }
            }
        }

        // ------------------------------------------------------------------ crows

        private void UpdateCrows(float dt)
        {
            var crows = State.CrowList;
            for (int i = crows.Count - 1; i >= 0; i--)
            {
                var crow = crows[i];
                crow.Timer += dt;
                if (crow.Timer >= crow.EatTime)
                {
                    var plot = State.GetPlot(crow.Pos);
                    plot.Reset();
                    plot.HasCrow = false;
                    crows.RemoveAt(i);
                    CrowAte?.Invoke(new CrowEvent(crow.Pos));
                }
            }

            if (State.Year < Config.CrowFirstYear) return;

            _crowSpawnTimer += dt;
            while (_crowSpawnTimer >= Config.CrowSpawnInterval)
            {
                _crowSpawnTimer -= Config.CrowSpawnInterval;
                TrySpawnCrow();
            }
        }

        private void TrySpawnCrow()
        {
            if (State.CrowList.Count >= Config.MaxCrows) return;
            _scratch.Clear();
            foreach (var p in State.PlotArray)
                if (p.IsRipe && !p.HasCrow && !State.IsUnderRing(p.Pos)) _scratch.Add(p);
            if (_scratch.Count == 0) return;
            if (_rng.NextDouble() >= State.Stats.CrowSpawnChance) return;
            SpawnCrowAt(_scratch[_rng.Next(_scratch.Count)].Pos);
        }

        private void SpawnCrowAt(GridPos pos)
        {
            State.GetPlot(pos).HasCrow = true;
            State.CrowList.Add(new Crow { Pos = pos, Timer = 0f, EatTime = Config.CrowEatTime });
            CrowLanded?.Invoke(new CrowEvent(pos));
        }

        private void RemoveCrowAt(GridPos pos)
        {
            State.GetPlot(pos).HasCrow = false;
            var crows = State.CrowList;
            for (int i = crows.Count - 1; i >= 0; i--)
                if (crows[i].Pos == pos) crows.RemoveAt(i);
        }

        private void ClearCrows()
        {
            foreach (var p in State.PlotArray) p.HasCrow = false;
            State.CrowList.Clear();
        }

        // ------------------------------------------------------------------ field & almanac

        private void BuildField(int size)
        {
            var old = State.PlotArray;
            int oldSize = State.GridSize;
            var plots = new Plot[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Existing plots keep their index (field is anchored bottom-left); new ones start at tier 0, Dry.
                plots[y * size + x] = old != null && x < oldSize && y < oldSize
                    ? old[y * oldSize + x]
                    : new Plot(new GridPos(x, y), 0);
            }
            State.PlotArray = plots;
            State.GridSize = size;
            foreach (var a in State.ApprenticeList) PlaceIdle(a);
        }

        private Plot FindLowestUpgradablePlot()
        {
            int cap = State.Stats.MaxTierUnlocked;
            Plot lowest = null;
            foreach (var p in State.PlotArray) // row-major: first lowest wins ties
                if (p.Tier < cap && (lowest == null || p.Tier < lowest.Tier)) lowest = p;
            return lowest;
        }

        private void ApplyPurchase(AlmanacNode node)
        {
            switch (node.Effect)
            {
                case EffectType.ExpandField:
                    BuildField(Math.Min(Config.MaxGridSize, State.GridSize + 1));
                    FieldExpanded?.Invoke();
                    break;
                case EffectType.UpgradePlot:
                {
                    var plot = FindLowestUpgradablePlot();
                    if (plot != null) plot.Tier++;
                    break;
                }
            }
        }

        private void ResolveStats()
        {
            State.Stats = StatResolver.Resolve(Config, State.LevelMap, Nodes);
            State.RingRadius = _ringRadiusOverride ?? State.Stats.RingRadius;
            SyncApprenticeCount();
        }
    }
}
