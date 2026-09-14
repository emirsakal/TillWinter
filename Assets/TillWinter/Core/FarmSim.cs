using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// The whole game. Pure C#, deterministic for a fixed dt and seed. The Unity layer calls
    /// <see cref="Tick"/> every frame, forwards input via <see cref="Tick"/>/<see cref="TapAt"/>/<see cref="TapCloud"/>,
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
        public event Action RainCloudAppeared;
        public event Action RainCloudTapped;
        public event Action RainCloudLeft;
        public event Action<int> TractorSweepStarted;
        public event Action<Hint> HintShown;

        private Rng _rng;
        private readonly List<Plot> _scratch = new List<Plot>();
        private float _crowSpawnTimer;
        private float? _ringRadiusOverride;
        private bool _offline;

        /// <summary>True while <see cref="SimulateOffline"/> runs; presentation skips per-event FX.</summary>
        public bool IsSimulatingOffline => _offline;
        private bool _forceGoldenNext;

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
                IsMaxedOverride = n => n.Effect == EffectType.UpgradePlot && FindLowestUpgradablePlot(null) == null,
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
            BuildField(State.Stats.StartGridSize, false);
            ResolveStats();
            ScheduleCloud();
        }

        // ------------------------------------------------------------------ public API

        /// <param name="dt">Seconds. Already scaled by any debug time scale.</param>
        /// <param name="ring">Ring centre in plot space, or null when the finger is up.</param>
        public void Tick(float dt, RingInput? ring)
        {
            if (dt < 0f) dt = 0f;
            State.Ring = State.Phase == Phase.Year ? ring : null;
            if (State.Phase == Phase.Winter)
            {
                UpdateGreenhouse(dt);
                return;
            }
            if (State.Phase != Phase.Year) return;

            AdvanceYear(dt);
            if (State.Phase != Phase.Year) return;

            UpdateCombo(dt);
            UpdatePlots(dt);
            UpdateApprentices(dt);
            UpdateTractor(dt);
            UpdateCrows(dt);
            UpdateCloud(dt);
        }

        /// <summary>Tap a plot. Scares a crow (dropping the bounty) if one is there. Returns true if something happened.</summary>
        public bool TapAt(GridPos pos)
        {
            if (State.Phase != Phase.Year || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (!plot.HasCrow) return false;
            ScareWithBounty(plot);
            return true;
        }

        /// <summary>Tap the rain cloud (GDD §5.2): waters every Dry plot, grows every Wet plot by the boost.</summary>
        public bool TapCloud()
        {
            var c = State.Cloud;
            if (State.Phase != Phase.Year || !c.Active) return false;
            foreach (var p in State.PlotArray)
            {
                if (p.State == PlotState.Dry)
                {
                    p.State = PlotState.Wet;
                    p.Progress = 0f;
                    PlotWatered?.Invoke(p.Pos);
                }
                else if (p.State == PlotState.Wet)
                {
                    p.Progress += Config.CloudWetBoost;
                    if (p.Progress >= 1f)
                    {
                        p.State = PlotState.Ripe;
                        p.Progress = 0f;
                        PlotRipened?.Invoke(p.Pos);
                    }
                }
            }
            c.Active = false;
            c.TimeLeft = 0f;
            RainCloudTapped?.Invoke();
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

        // ------------------------------------------------------------------ onboarding (GDD §10.5)

        /// <summary>Marks a hint as shown. Returns true the first time only.</summary>
        public bool MarkHint(Hint hint)
        {
            if (State.Onboarding.Has(hint)) return false;
            State.Onboarding.Set(hint);
            HintShown?.Invoke(hint);
            return true;
        }

        public bool HintPending(Hint hint) => !State.Onboarding.Has(hint);

        /// <summary>Remembers a tree canvas pan/zoom (saved).</summary>
        public void RememberTreeView(TreeKind tree, float panX, float panY, float zoom)
        {
            var m = tree == TreeKind.Almanac ? State.AlmanacView : State.HeritageView;
            m.HasView = true;
            m.PanX = panX;
            m.PanY = panY;
            m.Zoom = zoom;
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
            State.Combo = 0;
            _crowSpawnTimer = 0f;
            ResetCloud();
            ResolveStats();
            State.PlotArray = null;
            State.GridSize = 0;
            BuildField(State.Stats.StartGridSize, false);
            ResolveStats();
            ResetTractor();
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
            if (State.GridSize != State.Stats.TargetGridSize) BuildField(State.Stats.TargetGridSize, false);
            BeginSpring();
            GenerationStarted?.Invoke();
        }

        private void BeginSpring()
        {
            State.Phase = Phase.Year;
            State.YearTime = 0f;
            State.FrostWarning = false;
            State.Season = Season.Spring;
            State.Combo = 0;
            _crowSpawnTimer = 0f;
            ClearCrows();
            foreach (var p in State.PlotArray)
            {
                p.Reset();
                if (State.Stats.SpringHeadStart) p.State = PlotState.Wet;
            }
            ResetApprentices();
            ResetTractor();
            ScheduleCloud();
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
                CloudActive = s.Cloud.Active,
                CloudX = s.Cloud.X,
                CloudTimeLeft = s.Cloud.TimeLeft,
                CloudSpawnedThisYear = s.Cloud.SpawnedThisYear,
                CloudSpawnTime = s.Cloud.SpawnTime,
                TractorRow = s.Tractor.Row,
                TractorX = s.Tractor.X,
                TractorSweeping = s.Tractor.Sweeping,
                TractorTimeToNextSweep = s.Tractor.TimeToNextSweep,
                TractorPassed = s.Tractor.Passed,
                Combo = s.Combo,
                ComboTimer = s.ComboTimer,
                GreenhouseSecondsLeft = s.Greenhouse.SecondsLeftThisWinter,
                GreenhouseCoinsThisWinter = s.Greenhouse.CoinsThisWinter,
                OnboardingBits = s.Onboarding.Bits,
                AlmanacViewHas = s.AlmanacView.HasView, AlmanacViewX = s.AlmanacView.PanX, AlmanacViewY = s.AlmanacView.PanY, AlmanacViewZoom = s.AlmanacView.Zoom,
                HeritageViewHas = s.HeritageView.HasView, HeritageViewX = s.HeritageView.PanX, HeritageViewY = s.HeritageView.PanY, HeritageViewZoom = s.HeritageView.Zoom,
            };
            for (int i = 0; i < s.PlotArray.Length; i++)
            {
                var p = s.PlotArray[i];
                float crowTimer = 0f;
                foreach (var c in s.CrowList) if (c.Pos == p.Pos) crowTimer = c.Timer;
                d.Plots[i] = new PlotSave { X = p.Pos.X, Y = p.Pos.Y, Tier = p.Tier, State = (int)p.State, Progress = p.Progress, HasCrow = p.HasCrow, CrowTimer = crowTimer, Golden = p.IsGolden };
            }
            for (int i = 0; i < s.ApprenticeList.Count; i++)
                d.Apprentices[i] = new ApprenticeSave { X = s.ApprenticeList[i].X, Y = s.ApprenticeList[i].Y };
            return d;
        }

        /// <summary>Rebuilds a sim from a save. Returns null for an unknown schema version or malformed data.</summary>
        public static FarmSim FromSave(SaveData data, FarmConfig config, IReadOnlyList<SkillNode> almanacNodes = null, IReadOnlyList<SkillNode> heritageNodes = null)
        {
            data = SaveMigrations.Migrate(data, config);
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
            sim.BuildField(data.GridSize, false);
            foreach (var ps in data.Plots)
            {
                var pos = new GridPos(ps.X, ps.Y);
                if (!s.InBounds(pos)) return null;
                var plot = s.GetPlot(pos);
                plot.Tier = Math.Max(0, Math.Min(config.MaxTier, ps.Tier));
                plot.State = (PlotState)ps.State;
                plot.Progress = ps.Progress;
                plot.IsGolden = ps.Golden;
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
            s.Cloud.Active = data.CloudActive;
            s.Cloud.X = data.CloudX;
            s.Cloud.TimeLeft = data.CloudTimeLeft;
            s.Cloud.SpawnedThisYear = data.CloudSpawnedThisYear;
            s.Cloud.SpawnTime = data.CloudSpawnTime;
            s.Tractor.Row = data.TractorRow;
            s.Tractor.X = data.TractorX;
            s.Tractor.Sweeping = data.TractorSweeping && s.Tractor.Owned;
            s.Tractor.TimeToNextSweep = data.TractorTimeToNextSweep;
            s.Tractor.Passed = data.TractorPassed;
            s.Combo = data.Combo;
            s.ComboTimer = data.ComboTimer;
            s.Greenhouse.SecondsLeftThisWinter = data.GreenhouseSecondsLeft;
            s.Greenhouse.CoinsThisWinter = data.GreenhouseCoinsThisWinter;
            s.Onboarding.Bits = data.OnboardingBits;
            s.AlmanacView.HasView = data.AlmanacViewHas; s.AlmanacView.PanX = data.AlmanacViewX; s.AlmanacView.PanY = data.AlmanacViewY; s.AlmanacView.Zoom = data.AlmanacViewZoom <= 0f ? 1f : data.AlmanacViewZoom;
            s.HeritageView.HasView = data.HeritageViewHas; s.HeritageView.PanX = data.HeritageViewX; s.HeritageView.PanY = data.HeritageViewY; s.HeritageView.Zoom = data.HeritageViewZoom <= 0f ? 1f : data.HeritageViewZoom;
            sim.UpdateGreenhouseRate();
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
        /// Advances passive systems only (Irrigation → Sun → apprentices/tractor) for min(elapsed, cap) at a
        /// coarse fixed step. No ring, no crows, no cloud, no year timer, no season change. No-op outside Phase.Year.
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
            int apprentice = 0, tractor = 0;
            Action<HarvestEvent> count = e => { if (e.Source == HarvestSource.Apprentice) apprentice++; else if (e.Source == HarvestSource.Tractor) tractor++; };
            Harvested += count;
            var ringBefore = State.Ring;
            State.Ring = null;
            _offline = true;
            for (int i = 0; i < steps; i++)
            {
                UpdatePlots(step);
                UpdateApprentices(step);
                UpdateTractor(step);
            }
            _offline = false;
            Harvested -= count;
            State.Ring = ringBefore;
            return new OfflineReport(steps * (double)step, State.Coins - coinsBefore, State.Generation.Harvests - harvestsBefore, capped, apprentice, tractor);
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

        /// <summary>Spawns the rain cloud now (even if not unlocked or already spawned this year).</summary>
        public bool DebugSpawnCloud()
        {
            if (State.Phase != Phase.Year) return false;
            SpawnCloud();
            return true;
        }

        /// <summary>The next replanted crop is golden regardless of chance.</summary>
        public void DebugNextHarvestGolden() => _forceGoldenNext = true;

        /// <summary>Starts a tractor sweep now if the tractor is owned and a row has Ripe plots.</summary>
        public bool DebugForceTractorSweep()
        {
            if (State.Phase != Phase.Year || !State.Tractor.Owned) return false;
            State.Tractor.TimeToNextSweep = 0f;
            return TryStartSweep();
        }

        public void DebugAddGreenhouseSeconds(float seconds)
        {
            State.Greenhouse.SecondsLeftThisWinter += seconds;
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
                BuildField(State.Stats.TargetGridSize, State.Stats.FertileStart);
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
            // late_frost: nearly-grown crops pay half instead of being lost.
            if (State.Stats.LateFrost)
            {
                foreach (var p in State.PlotArray)
                    if (p.IsRipe || (p.State == PlotState.Wet && p.Progress >= Config.LateFrostThreshold))
                        Harvest(p, HarvestSource.LateFrost, -1);
            }
            State.Phase = Phase.Winter;
            State.Season = Season.Winter;
            State.YearTime = State.Stats.YearLength;
            State.FrostWarning = false;
            State.Ring = null;
            State.Combo = 0;
            ClearCrows();
            foreach (var p in State.PlotArray) p.Reset();
            ResetApprentices();
            ResetTractor();
            ResetCloud();
            State.Greenhouse.SecondsLeftThisWinter = Config.GreenhouseWinterCapSeconds;
            State.Greenhouse.CoinsThisWinter = 0;
            UpdateGreenhouseRate();
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
            bool golden = plot.IsGolden;
            double coins = Crop(plot).Value * st.CropValueMult * (golden ? Config.GoldenValueMultiplier : 1);
            switch (source)
            {
                case HarvestSource.Ring:
                    RegisterComboHit();
                    coins *= st.RingBonusMult * (1 + st.RingComboLevel * 0.01 * Math.Min(State.Combo, Config.ComboMaxStacks));
                    break;
                case HarvestSource.Apprentice:
                    coins *= st.ApprenticeYield;
                    break;
                case HarvestSource.LateFrost:
                    coins *= Config.LateFrostValueMultiplier;
                    break;
            }
            AddCoins(coins);
            State.Generation.Harvests++;
            int tier = plot.Tier;
            Replant(plot, source == HarvestSource.Apprentice && st.HelperWater);
            if (plot.HasCrow)
            {
                RemoveCrowAt(plot.Pos);
                CrowScared?.Invoke(new CrowEvent(plot.Pos, 0));
            }
            Harvested?.Invoke(new HarvestEvent(plot.Pos, tier, coins, source, apprenticeIndex, golden));
        }

        /// <summary>Same tier back to Dry (or Wet), rolling the golden chance.</summary>
        private void Replant(Plot plot, bool startWet)
        {
            plot.Reset();
            if (startWet) plot.State = PlotState.Wet;
            if (_forceGoldenNext)
            {
                plot.IsGolden = true;
                _forceGoldenNext = false;
            }
            else if (State.Stats.GoldenCropChance > 0 && _rng.NextDouble() < State.Stats.GoldenCropChance)
            {
                plot.IsGolden = true;
            }
        }

        private void RegisterComboHit()
        {
            State.Combo = State.ComboTimer <= Config.ComboWindowSeconds && State.Combo > 0 ? State.Combo + 1 : 1;
            State.ComboTimer = 0f;
        }

        private void UpdateCombo(float dt)
        {
            State.ComboTimer += dt;
            if (State.Combo > 0 && State.ComboTimer > Config.ComboWindowSeconds) State.Combo = 0;
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

        // ------------------------------------------------------------------ tractor (GDD §4)

        private float TractorInterval()
        {
            var t = Config.TractorIntervalByLevel;
            int l = Math.Max(0, Math.Min(t.Length - 1, State.Stats.TractorLevel));
            return t[l];
        }

        private void ResetTractor()
        {
            var t = State.Tractor;
            t.Sweeping = false;
            t.X = -0.5f;
            t.Row = 0;
            t.Passed = 0;
            t.TimeToNextSweep = t.Owned ? TractorInterval() : 0f;
        }

        private void UpdateTractor(float dt)
        {
            var t = State.Tractor;
            if (!t.Owned) return;
            if (!t.Sweeping)
            {
                t.TimeToNextSweep -= dt;
                if (t.TimeToNextSweep <= 0f)
                {
                    if (!TryStartSweep()) t.TimeToNextSweep = TractorInterval();
                }
                return;
            }

            t.X += dt / Math.Max(0.01f, Config.TractorSecondsPerPlot);
            int n = State.GridSize;
            while (t.Passed < n && t.X >= t.Passed)
            {
                var plot = State.GetPlot(t.Passed, t.Row);
                if (plot.HasCrow) ScareWithBounty(plot);
                if (plot.IsRipe) Harvest(plot, HarvestSource.Tractor, -1);
                t.Passed++;
            }
            if (t.X >= n)
            {
                t.Sweeping = false;
                t.X = -0.5f;
                t.Passed = 0;
                t.TimeToNextSweep = TractorInterval();
            }
        }

        /// <summary>Picks the row with the most Ripe plots (ties: lowest row). Returns false if none is Ripe.</summary>
        private bool TryStartSweep()
        {
            int n = State.GridSize;
            int bestRow = -1, bestCount = 0;
            for (int y = 0; y < n; y++)
            {
                int count = 0;
                for (int x = 0; x < n; x++) if (State.GetPlot(x, y).IsRipe) count++;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestRow = y;
                }
            }
            if (bestRow < 0) return false;
            var t = State.Tractor;
            t.Row = bestRow;
            t.X = -0.5f;
            t.Passed = 0;
            t.Sweeping = true;
            TractorSweepStarted?.Invoke(bestRow);
            return true;
        }

        // ------------------------------------------------------------------ greenhouse (GDD §4)

        private double FieldValue()
        {
            double v = 0;
            foreach (var p in State.PlotArray) v += Crop(p).Value;
            return v;
        }

        private void UpdateGreenhouseRate()
        {
            var st = State.Stats;
            double rate = st.GreenhouseLevel * Config.GreenhouseRatePerLevel * FieldValue();
            if (st.GreenhouseX2Level > 0) rate *= 2;
            State.Greenhouse.CoinsPerSecond = rate;
        }

        private void UpdateGreenhouse(float dt)
        {
            var gh = State.Greenhouse;
            if (State.Stats.GreenhouseLevel <= 0 || gh.SecondsLeftThisWinter <= 0f || dt <= 0f) return;
            float used = Math.Min(dt, gh.SecondsLeftThisWinter);
            gh.SecondsLeftThisWinter -= used;
            double coins = gh.CoinsPerSecond * used;
            gh.CoinsThisWinter += coins;
            AddCoins(coins);
        }

        // ------------------------------------------------------------------ rain cloud (GDD §5.2)

        private void ScheduleCloud()
        {
            var c = State.Cloud;
            c.Active = false;
            c.TimeLeft = 0f;
            c.X = 0f;
            c.SpawnedThisYear = false;
            float length = State.Stats.YearLength;
            // Random time in Summer (middle third); consume RNG only when the feature is unlocked so seeds stay stable.
            c.SpawnTime = State.Stats.RainCloudUnlocked ? length / 3f + (float)_rng.NextDouble() * length / 3f : float.MaxValue;
        }

        private void ResetCloud()
        {
            var c = State.Cloud;
            c.Active = false;
            c.TimeLeft = 0f;
            c.X = 0f;
        }

        private void SpawnCloud()
        {
            var c = State.Cloud;
            c.Active = true;
            c.X = 0f;
            c.TimeLeft = Config.CloudDriftSeconds;
            c.SpawnedThisYear = true;
            RainCloudAppeared?.Invoke();
        }

        private void UpdateCloud(float dt)
        {
            var c = State.Cloud;
            if (!c.Active)
            {
                if (State.Stats.RainCloudUnlocked && !c.SpawnedThisYear && State.YearTime >= c.SpawnTime) SpawnCloud();
                return;
            }
            c.TimeLeft -= dt;
            c.X = 1f - Math.Max(0f, c.TimeLeft) / Math.Max(0.01f, Config.CloudDriftSeconds);
            if (c.TimeLeft <= 0f)
            {
                c.Active = false;
                c.X = 1f;
                RainCloudLeft?.Invoke();
            }
        }

        // ------------------------------------------------------------------ crows

        private void ScareWithBounty(Plot plot)
        {
            double coins = (Config.CrowScareValueMultiplier + State.Stats.CrowBountyLevel) * Crop(plot).Value;
            AddCoins(coins);
            RemoveCrowAt(plot.Pos);
            State.Generation.CrowsScared++;
            CrowScared?.Invoke(new CrowEvent(plot.Pos, coins));
        }

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
            if (State.Stats.CrowSpawnChance <= 0f) return;
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

        private void BuildField(int size, bool newPlotsWet)
        {
            var old = State.PlotArray;
            int oldSize = State.GridSize;
            var plots = new Plot[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Existing plots keep their index (field is anchored bottom-left); new ones start at tier 0.
                if (old != null && x < oldSize && y < oldSize)
                {
                    plots[y * size + x] = old[y * oldSize + x];
                }
                else
                {
                    var p = new Plot(new GridPos(x, y), 0);
                    if (newPlotsWet && old != null) p.State = PlotState.Wet;
                    plots[y * size + x] = p;
                }
            }
            State.PlotArray = plots;
            State.GridSize = size;
            foreach (var a in State.ApprenticeList) PlaceIdle(a);
        }

        private Plot FindLowestUpgradablePlot(Plot exclude)
        {
            int cap = State.Stats.MaxTierUnlocked;
            Plot lowest = null;
            foreach (var p in State.PlotArray) // row-major: first lowest wins ties
                if (p != exclude && p.Tier < cap && (lowest == null || p.Tier < lowest.Tier)) lowest = p;
            return lowest;
        }

        private void ApplyPurchase(SkillNode node)
        {
            switch (node.Effect)
            {
                case EffectType.ExpandField:
                    BuildField(Math.Min(Config.MaxGridSize, State.GridSize + 1), State.Stats.FertileStart);
                    FieldExpanded?.Invoke();
                    break;
                case EffectType.UpgradePlot:
                {
                    var plot = FindLowestUpgradablePlot(null);
                    if (plot != null) plot.Tier++;
                    if (State.Stats.BulkUpgrade)
                    {
                        var second = FindLowestUpgradablePlot(null);
                        if (second != null) second.Tier++;
                    }
                    break;
                }
            }
        }

        private void ResolveStats()
        {
            State.Stats = StatResolver.Resolve(Config, Almanac.Levels, Heritage.Levels, Almanac.Nodes, Heritage.Nodes);
            State.RingRadius = _ringRadiusOverride ?? State.Stats.RingRadius;
            bool ownedBefore = State.Tractor.Owned;
            State.Tractor.Owned = State.Stats.TractorLevel > 0;
            if (State.Tractor.Owned && !ownedBefore) ResetTractor();
            if (State.PlotArray != null)
            {
                SyncApprenticeCount();
                UpdateGreenhouseRate();
            }
        }
    }
}
