using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// The whole game. Pure C#, deterministic for a fixed dt and seed. The Unity layer calls
    /// <see cref="Tick"/> every frame, forwards input via <see cref="Tick"/>/<see cref="TapAt"/>,
    /// buys tree nodes with <see cref="TryBuy"/>, and renders <see cref="State"/>.
    /// </summary>
    public sealed class FarmSim
    {
        public FarmConfig Config { get; }
        public FarmState State { get; }
        public SkillTree Almanac { get; }
        public SkillTree Heritage { get; }
        /// <summary>Almanac nodes (kept for the list UI).</summary>
        public IReadOnlyList<SkillNode> Nodes => Almanac.Nodes;
        public IReadOnlyList<SkillNode> HeritageNodes => Heritage.Nodes;

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
        public event Action<RetireEvent> Retired;
        public event Action GenerationStarted;

        private Rng _rng;
        private readonly List<Plot> _scratch = new List<Plot>();
        private float _crowSpawnTimer;
        private float? _ringRadiusOverride;
        private bool _offline;

        public FarmSim(FarmConfig config, int seed, IReadOnlyList<SkillNode> almanacNodes = null, IReadOnlyList<SkillNode> heritageNodes = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            almanacNodes = almanacNodes ?? AlmanacData.Nodes;
            heritageNodes = heritageNodes ?? HeritageData.Nodes;
            var errors = SkillTree.ValidateAll(almanacNodes, heritageNodes);
            if (errors.Count > 0) throw new InvalidOperationException("Skill tree table invalid: " + string.Join("; ", errors));

            State = new FarmState();
            _rng = new Rng(seed);
            Almanac = new SkillTree(TreeKind.Almanac, almanacNodes)
            {
                GetCurrency = () => State.Coins,
                Spend = c => State.Coins -= c,
                PurchaseAllowed = () => State.Phase == Phase.Winter,
                IsMaxedOverride = n => n.Effect == EffectType.UpgradePlot && FindLowestUpgradablePlot() == null,
                CostMultiplier = () => State.Stats.AlmanacCostMult,
            };
            Heritage = new SkillTree(TreeKind.Heritage, heritageNodes)
            {
                GetCurrency = () => State.Generation.SeedsBanked,
                Spend = c => State.Generation.SeedsBanked -= (int)Math.Round(c),
                PurchaseAllowed = () => State.Phase == Phase.Winter || State.Phase == Phase.Heritage,
            };
            State.Almanac = Almanac;
            State.Heritage = Heritage;
            ResolveStats();
            BuildField(State.Stats.StartGridSize);
            ResolveStats();
        }

        // ------------------------------------------------------------------ public API

        /// <param name="dt">Seconds. Already scaled by any debug time scale.</param>
        /// <param name="ring">Ring centre in plot space, or null when the finger is up.</param>
        public void Tick(float dt, RingInput? ring)
        {
            if (dt < 0f) dt = 0f;
            State.Ring = State.Phase == Phase.Year ? ring : null;
            if (State.Phase != Phase.Year) return;

            AdvanceYear(dt);
            if (State.Phase != Phase.Year) return;

            UpdatePlots(dt);
            UpdateApprentices(dt);
            UpdateCrows(dt);
        }

        /// <summary>Tap a plot. Scares a crow (dropping coins) if one is there. Returns true if something happened.</summary>
        public bool TapAt(GridPos pos)
        {
            if (State.Phase != Phase.Year || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (!plot.HasCrow) return false;
            double coins = Config.CrowScareValueMultiplier * Crop(plot).Value;
            AddCoins(coins);
            RemoveCrowAt(pos);
            State.Generation.CrowsScared++;
            CrowScared?.Invoke(new CrowEvent(pos, coins));
            return true;
        }

        private SkillTree TreeOf(string nodeId) => Almanac.Contains(nodeId) ? Almanac : Heritage.Contains(nodeId) ? Heritage : null;

        public SkillNode GetNode(string id) => TreeOf(id)?.GetNode(id);
        public double CostOf(string nodeId) => TreeOf(nodeId)?.CostOf(nodeId) ?? double.PositiveInfinity;
        public bool IsMaxed(string nodeId) => TreeOf(nodeId)?.IsMaxed(nodeId) ?? true;
        public bool IsAvailable(string nodeId) => TreeOf(nodeId)?.IsAvailable(nodeId) ?? false;
        public bool CanBuy(string nodeId) => TreeOf(nodeId)?.CanBuy(nodeId) ?? false;

        public int GetMaxLevel(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return 0;
            if (node.Effect == EffectType.UpgradePlot)
                return State.GridSize * State.GridSize * State.Stats.MaxTierUnlocked;
            return node.MaxLevel;
        }

        public bool TryBuy(string nodeId)
        {
            var tree = TreeOf(nodeId);
            if (tree == null) return false;
            int level = tree.Buy(nodeId);
            if (level <= 0) return false;
            ApplyPurchase(tree.GetNode(nodeId));
            ResolveStats();
            Purchased?.Invoke(new PurchaseEvent(nodeId, level, tree.Kind));
            return true;
        }

        /// <summary>Leave the winter Almanac and start Spring of the next year.</summary>
        public void StartNextYear()
        {
            if (State.Phase != Phase.Winter) return;
            State.Year++;
            State.Generation.YearsThisGeneration++;
            BeginSpring();
        }

        // ------------------------------------------------------------------ heritage (GDD §7)

        public bool CanRetire => State.Generation.LifetimeCoinsThisGeneration >= Config.HeritageThreshold;

        /// <summary>floor(sqrt(lifetimeCoinsThisGeneration / SeedDivisor)).</summary>
        public int SeedsIfRetiredNow => SeedsFor(State.Generation.LifetimeCoinsThisGeneration);

        public int SeedsFor(double lifetimeCoins) =>
            Config.SeedDivisor <= 0 ? 0 : (int)Math.Floor(Math.Sqrt(Math.Max(0, lifetimeCoins) / Config.SeedDivisor));

        /// <summary>Hand the farm to the next generation. Winter only, and only once <see cref="CanRetire"/>.</summary>
        public bool Retire()
        {
            if (State.Phase != Phase.Winter || !CanRetire) return false;
            var g = State.Generation;
            int seeds = SeedsIfRetiredNow;
            g.SeedsBanked += seeds;
            g.SeedsEarnedTotal += seeds;
            g.Generation++;
            g.LifetimeCoinsThisGeneration = 0;
            g.YearsThisGeneration = 0;

            State.Coins = 0;
            Almanac.Reset();
            ClearCrows();
            State.Year = 1;
            State.YearTime = 0f;
            State.FrostWarning = false;
            _crowSpawnTimer = 0f;
            ResolveStats();
            State.PlotArray = null;
            State.GridSize = 0;
            BuildField(State.Stats.StartGridSize);
            ResolveStats();
            State.Phase = Phase.Heritage;
            State.Season = Season.Winter;
            Retired?.Invoke(new RetireEvent(seeds, g.Generation));
            return true;
        }

        /// <summary>Leave the Heritage screen: Spring of year 1 with the Heritage starting bonuses applied.</summary>
        public void StartNewGeneration()
        {
            if (State.Phase != Phase.Heritage) return;
            State.Year = 1;
            ResolveStats();
            if (State.GridSize != State.Stats.TargetGridSize) BuildField(State.Stats.TargetGridSize);
            BeginSpring();
            GenerationStarted?.Invoke();
        }

        private void BeginSpring()
        {
            State.Phase = Phase.Year;
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

        // ------------------------------------------------------------------ save / load (GDD §9)

        public SaveData ToSave()
        {
            var s = State;
            var d = new SaveData
            {
                SchemaVersion = SaveData.CurrentSchemaVersion,
                SavedAtUnixSeconds = 0,
                Phase = (int)s.Phase,
                Year = s.Year,
                Season = (int)s.Season,
                YearTime = s.YearTime,
                FrostWarning = s.FrostWarning,
                Coins = s.Coins,
                CrowSpawnTimer = _crowSpawnTimer,
                RngState = _rng.State,
                Generation = s.Generation.Generation,
                LifetimeCoinsThisGeneration = s.Generation.LifetimeCoinsThisGeneration,
                LifetimeCoinsTotal = s.Generation.LifetimeCoinsTotal,
                YearsThisGeneration = s.Generation.YearsThisGeneration,
                SeedsBanked = s.Generation.SeedsBanked,
                SeedsEarnedTotal = s.Generation.SeedsEarnedTotal,
                CrowsScared = s.Generation.CrowsScared,
                Harvests = s.Generation.Harvests,
                GridSize = s.GridSize,
                Plots = new PlotSave[s.PlotArray.Length],
                Apprentices = new ApprenticeSave[s.ApprenticeList.Count],
                AlmanacLevels = Pairs(Almanac.Levels),
                HeritageLevels = Pairs(Heritage.Levels),
            };
            for (int i = 0; i < s.PlotArray.Length; i++)
            {
                var p = s.PlotArray[i];
                float crowTimer = 0f;
                foreach (var c in s.CrowList) if (c.Pos == p.Pos) crowTimer = c.Timer;
                d.Plots[i] = new PlotSave { X = p.Pos.X, Y = p.Pos.Y, Tier = p.Tier, State = (int)p.State, Progress = p.Progress, HasCrow = p.HasCrow, CrowTimer = crowTimer };
            }
            for (int i = 0; i < s.ApprenticeList.Count; i++)
                d.Apprentices[i] = new ApprenticeSave { X = s.ApprenticeList[i].X, Y = s.ApprenticeList[i].Y };
            return d;
        }

        /// <summary>Rebuilds a sim from a save. Returns null for an unknown schema version or malformed data.</summary>
        public static FarmSim FromSave(SaveData data, FarmConfig config, IReadOnlyList<SkillNode> almanacNodes = null, IReadOnlyList<SkillNode> heritageNodes = null)
        {
            data = SaveMigrations.Migrate(data);
            if (data == null || config == null) return null;
            if (data.GridSize < 1 || data.Plots == null || data.Plots.Length != data.GridSize * data.GridSize) return null;

            var sim = new FarmSim(config, 0, almanacNodes, heritageNodes);
            var s = sim.State;
            sim._rng = new Rng(data.RngState);
            sim._crowSpawnTimer = data.CrowSpawnTimer;
            s.Phase = (Phase)data.Phase;
            s.Year = data.Year;
            s.Season = (Season)data.Season;
            s.YearTime = data.YearTime;
            s.FrostWarning = data.FrostWarning;
            s.Coins = data.Coins;
            var g = s.Generation;
            g.Generation = Math.Max(1, data.Generation);
            g.LifetimeCoinsThisGeneration = data.LifetimeCoinsThisGeneration;
            g.LifetimeCoinsTotal = data.LifetimeCoinsTotal;
            g.YearsThisGeneration = data.YearsThisGeneration;
            g.SeedsBanked = data.SeedsBanked;
            g.SeedsEarnedTotal = data.SeedsEarnedTotal;
            g.CrowsScared = data.CrowsScared;
            g.Harvests = data.Harvests;

            foreach (var pair in data.AlmanacLevels ?? new LevelPair[0]) sim.Almanac.SetLevel(pair.Id, pair.Level);
            foreach (var pair in data.HeritageLevels ?? new LevelPair[0]) sim.Heritage.SetLevel(pair.Id, pair.Level);

            s.PlotArray = null;
            s.GridSize = 0;
            sim.BuildField(data.GridSize);
            foreach (var ps in data.Plots)
            {
                var pos = new GridPos(ps.X, ps.Y);
                if (!s.InBounds(pos)) return null;
                var plot = s.GetPlot(pos);
                plot.Tier = Math.Max(0, Math.Min(config.MaxTier, ps.Tier));
                plot.State = (PlotState)ps.State;
                plot.Progress = ps.Progress;
                if (ps.HasCrow)
                {
                    plot.HasCrow = true;
                    s.CrowList.Add(new Crow { Pos = pos, Timer = ps.CrowTimer, EatTime = config.CrowEatTime });
                }
            }
            sim.ResolveStats();
            var saved = data.Apprentices ?? new ApprenticeSave[0];
            for (int i = 0; i < s.ApprenticeList.Count && i < saved.Length; i++)
            {
                s.ApprenticeList[i].X = saved[i].X;
                s.ApprenticeList[i].Y = saved[i].Y;
            }
            return sim;
        }

        private static LevelPair[] Pairs(IReadOnlyDictionary<string, int> levels)
        {
            var list = new List<LevelPair>();
            foreach (var kv in levels) list.Add(new LevelPair { Id = kv.Key, Level = kv.Value });
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list.ToArray();
        }

        // ------------------------------------------------------------------ offline (GDD §9)

        /// <summary>
        /// Advances passive systems only (Irrigation → Sun → apprentices) for min(elapsed, cap) at a
        /// coarse fixed step. No ring, no crows, no year timer, no season change. No-op outside Phase.Year.
        /// </summary>
        public OfflineReport SimulateOffline(double elapsedSeconds)
        {
            if (State.Phase != Phase.Year || elapsedSeconds <= 0 || double.IsNaN(elapsedSeconds))
                return new OfflineReport(0, 0, 0, false);
            bool capped = elapsedSeconds > Config.OfflineCapSeconds;
            double total = Math.Min(elapsedSeconds, Config.OfflineCapSeconds);
            float step = Math.Max(0.05f, Config.OfflineStepSeconds);
            int steps = (int)Math.Floor(total / step);
            int maxSteps = (int)Math.Ceiling(Config.OfflineCapSeconds / step) + 1;
            if (steps > maxSteps) steps = maxSteps;

            double coinsBefore = State.Coins;
            int harvestsBefore = State.Generation.Harvests;
            var ringBefore = State.Ring;
            State.Ring = null;
            _offline = true;
            for (int i = 0; i < steps; i++)
            {
                UpdatePlots(step);
                UpdateApprentices(step);
            }
            _offline = false;
            State.Ring = ringBefore;
            return new OfflineReport(steps * (double)step, State.Coins - coinsBefore, State.Generation.Harvests - harvestsBefore, capped);
        }

        // ------------------------------------------------------------------ debug hooks

        public void DebugAddCoins(double amount) => State.Coins += amount;
        public void DebugAddSeeds(int amount) => State.Generation.SeedsBanked += amount;
        public void DebugAddLifetimeCoins(double amount)
        {
            State.Generation.LifetimeCoinsThisGeneration += amount;
            State.Generation.LifetimeCoinsTotal += amount;
        }

        public void DebugSkipToWinter()
        {
            if (State.Phase == Phase.Year) EnterWinter();
        }

        /// <summary>Force a crow onto a plot (ripening it if needed). Ignores year/scarecrow rules.</summary>
        public bool DebugSpawnCrow()
        {
            if (State.Phase != Phase.Year) return false;
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
            if (State.Phase != Phase.Year) return;
            foreach (var p in State.PlotArray)
            {
                p.State = PlotState.Ripe;
                p.Progress = 0f;
            }
        }

        /// <summary>Sets a node level directly (clamped to its max), applying field/tier side effects.</summary>
        public void DebugSetLevel(string nodeId, int level)
        {
            var tree = TreeOf(nodeId);
            var node = tree?.GetNode(nodeId);
            if (node == null) return;
            int max = node.MaxLevel < 0 ? int.MaxValue : node.MaxLevel;
            tree.SetLevel(nodeId, Math.Max(0, Math.Min(max, level)));
            ResolveStats();
            if (State.GridSize < State.Stats.TargetGridSize)
            {
                BuildField(State.Stats.TargetGridSize);
                FieldExpanded?.Invoke();
            }
        }

        /// <summary>null restores the tree-driven radius.</summary>
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
            State.Phase = Phase.Winter;
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
                bool under = !_offline && State.IsUnderRing(plot.Pos);
                var crop = Crop(plot);
                switch (plot.State)
                {
                    case PlotState.Dry:
                    {
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
            State.Generation.Harvests++;
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
            State.Generation.LifetimeCoinsThisGeneration += coins;
            State.Generation.LifetimeCoinsTotal += coins;
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

        // ------------------------------------------------------------------ field & trees

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

        private void ApplyPurchase(SkillNode node)
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
            State.Stats = StatResolver.Resolve(Config, Almanac.Levels, Heritage.Levels, Almanac.Nodes, Heritage.Nodes);
            State.RingRadius = _ringRadiusOverride ?? State.Stats.RingRadius;
            if (State.PlotArray != null) SyncApprenticeCount();
        }
    }
}
