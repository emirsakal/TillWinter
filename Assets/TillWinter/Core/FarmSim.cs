using System;
using System.Collections.Generic;

namespace TillWinter.Core
{
    /// <summary>
    /// The whole game. Pure C#, deterministic for a fixed dt and seed. The Unity layer calls
    /// <see cref="Tick"/> every frame, forwards input via <see cref="Tick"/>/<see cref="TapAt"/>/<see cref="TapCloud"/>,
    /// buys tree nodes with <see cref="TryBuy"/>, and renders <see cref="State"/>.
    /// </summary>
    public sealed partial class FarmSim
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
        /// <summary>The ring has cleared the stones off a plot (GDD §2.4 v1.8).</summary>
        public event Action<GridPos> PlotCleared;
        /// <summary>Summer drought turned a neglected Wet plot back to Dry (GDD §3.2 v1.9).</summary>
        public event Action<GridPos> PlotDriedOut;
        /// <summary>The year's goal was met: the goal, with its reward already paid (GDD §3.3 v1.9).</summary>
        public event Action<YearGoal> GoalCompleted;
        /// <summary>A new year set a goal.</summary>
        public event Action<YearGoal> GoalSet;
        /// <summary>A harvest went to the barn instead of the purse (plot, stored value) (GDD §3.5 v2.2).</summary>
        public event Action<GridPos, double> Stored;
        /// <summary>The barn sold at the winter market, put up preserves, or the jars sold in spring (coins or jar value).</summary>
        public event Action<double> BarnSold;
        public event Action<double> PreservesMade;
        public event Action<double> PreservesSold;
        /// <summary>The Almanac was reset for its refund (GDD §6.3 v2.2).</summary>
        public event Action<double> Respecced;
        /// <summary>The finished year's stars and the coins they paid, fired before WinterStarted (GDD §3.4 v1.9).</summary>
        public event Action<int, double> YearGraded;
        /// <summary>Weather began (a spell) or ended (Clear) (GDD §5.4 v1.9).</summary>
        public event Action<Weather> WeatherChanged;
        /// <summary>The farm dog chased a crow off this plot (GDD §4.3 v2.0).</summary>
        public event Action<GridPos> DogChased;
        /// <summary>A scarecrow was placed or moved (index).</summary>
        public event Action<int> ScarecrowMoved;
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
        /// <summary>A combo reached one of <see cref="FarmConfig.ComboMilestones"/>: the length and the bonus paid.</summary>
        public event Action<int, double> ComboMilestone;
        /// <summary>The Golden Year began (every Heritage node maxed, first time).</summary>
        public event Action GoldenYearStarted;
        /// <summary>The Golden Year's last day passed: fired before WinterStarted so the ending (credits, stats) can go first.</summary>
        public event Action GoldenYearEnded;

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
            State.RakeLength = Config.RakeLength;
            State.RakeWidth = Config.RakeWidth;
            State.CrossLength = Config.CrossLength;
            State.CrossWidth = Config.CrossWidth;
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

            UpdateWeather(dt);
            UpdateFlow(dt);
            if (State.TapCooldown > 0f) State.TapCooldown = Math.Max(0f, State.TapCooldown - dt);
            UpdateCombo(dt);
            UpdatePlots(dt);
            UpdateApprentices(dt);
            UpdateTractor(dt);
            UpdateCrows(dt);
            UpdatePests(dt);
            UpdateLuck(dt);
            UpdateTrader(dt);
            UpdateCloud(dt);
        }

        /// <summary>Tap a plot. Scares a crow (dropping the bounty) if one is there. Returns true if something happened.</summary>
        public bool TapAt(GridPos pos)
        {
            if (State.Phase != Phase.Year || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (plot.HasCrow)
            {
                ScareWithBounty(plot);
                return true;
            }
            if (TapPest(pos)) return true; // GDD §5.5 (v2.1): a mole is bonked, a rabbit shooed
            // GDD §2.1 (v1.6): with `tap_harvest`, a tap finishes one Ripe plot outright, on a cooldown.
            int level = State.Stats.TapHarvestLevel;
            if (level <= 0 || !plot.IsRipe || State.TapCooldown > 0f) return false;
            State.TapCooldown = Index(Config.TapHarvestCooldownByLevel, level);
            Harvest(plot, HarvestSource.Ring, -1);
            return true;
        }

        private static float Index(float[] table, int level) =>
            table == null || table.Length == 0 ? 0f : table[Math.Max(0, Math.Min(table.Length - 1, level))];

        /// <summary>Picks the ring's footprint; Rake needs `ring_shape` 1 and Cross level 2. Returns true when it changed.</summary>
        public bool SetRingShape(RingShape shape)
        {
            int level = State.Stats.RingShapeLevel;
            if (shape == RingShape.Rake && level < 1) return false;
            if (shape == RingShape.Cross && level < 2) return false;
            if (State.RingShape == shape) return false;
            State.RingShape = shape;
            return true;
        }

        /// <summary>Tap the rain cloud (GDD §5.2): waters every Dry plot, grows every Wet plot by the boost.</summary>
        public bool TapCloud()
        {
            var c = State.Cloud;
            if (State.Phase != Phase.Year || !c.Active) return false;
            foreach (var p in State.PlotArray)
            {
                if (p.IsStony) continue; // rain does not move stones
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
            double cost = tree.CostOf(nodeId);
            int level = tree.Buy(nodeId);
            if (level <= 0) return false;
            if (tree == Almanac) State.Generation.AlmanacSpent += cost;
            ApplyPurchase(tree.GetNode(nodeId));
            ResolveStats();
            Purchased?.Invoke(new PurchaseEvent(nodeId, level, tree.Kind));
            return true;
        }

        /// <summary>
        /// Ends the running year now and brings Winter forward (GDD §2.4 v1.5). Waiting out the clock was the only
        /// way into the Almanac, which made a finished field dead time; the harvest still standing is resolved by the
        /// same <see cref="EnterWinter"/> the timer uses, so ending early costs whatever late frost would have cost.
        /// </summary>
        public void EndYearNow()
        {
            if (State.Phase != Phase.Year) return;
            EnterWinter();
        }

        /// <summary>Leave the winter Almanac and start Spring of the next year.</summary>
        public void StartNextYear()
        {
            if (State.Phase != Phase.Winter) return;
            State.Year++;
            State.Generation.YearsThisGeneration++;
            State.Generation.YearsTotal++;
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

        /// <summary>Every Heritage node is at its max level (GDD §8 ending trigger).</summary>
        public bool HeritageComplete
        {
            get
            {
                if (Heritage.Nodes.Count == 0) return false;
                foreach (var n in Heritage.Nodes) if (!Heritage.IsMaxed(n.Id)) return false;
                return true;
            }
        }

        /// <summary>Real play time for the stats screen (the presentation layer feeds unpaused, unscaled seconds).</summary>
        public void AddPlayTime(double seconds)
        {
            if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) State.Generation.TimePlayedSeconds += seconds;
        }

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
            ResetBarn();
            g.AlmanacSpent = 0;
            g.RespecUsed = false;
            State.LastYearCoins = 0;
            State.LastGrade = 0;
            State.LastGradeBonus = 0;
            State.Goal.Clear();
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
            State.GoldenYearActive = HeritageComplete && !State.EndingSeen;
            ResolveStats();
            if (!State.GoldenYearActive && State.GridSize != State.Stats.TargetGridSize) BuildField(State.Stats.TargetGridSize, false);
            BeginSpring();
            if (State.GoldenYearActive)
            {
                PlantGoldenField(); // after BeginSpring, which resets every plot for the new year
                GoldenYearStarted?.Invoke();
            }
            GenerationStarted?.Invoke();
        }

        private void BeginSpring()
        {
            State.Phase = Phase.Year;
            State.YearTime = 0f;
            State.FrostWarning = false;
            State.Season = Season.Spring;
            State.CoinsThisYear = 0;
            State.HarvestsThisYear = 0;
            State.Combo = 0;
            _crowSpawnTimer = 0f;
            ClearCrows();
            foreach (var p in State.PlotArray)
            {
                p.Reset();
                if (State.Stats.SpringHeadStart && !p.IsStony) p.State = PlotState.Wet;
            }
            ResetApprentices();
            ResetTractor();
            ScheduleCloud();
            State.YearFreshSum = 0;
            State.CropsLostThisYear = 0;
            PlanWeather();
            SetYearGoal();
            ClearEvents();
            PlanTrader();
            OpenBarnForSpring();
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
                RingShape = (int)s.RingShape,
                ComboTimer = s.ComboTimer,
                GreenhouseSecondsLeft = s.Greenhouse.SecondsLeftThisWinter,
                GreenhouseCoinsThisWinter = s.Greenhouse.CoinsThisWinter,
                OnboardingBits = s.Onboarding.Bits,
                AlmanacViewHas = s.AlmanacView.HasView, AlmanacViewX = s.AlmanacView.PanX, AlmanacViewY = s.AlmanacView.PanY, AlmanacViewZoom = s.AlmanacView.Zoom,
                HeritageViewHas = s.HeritageView.HasView, HeritageViewX = s.HeritageView.PanX, HeritageViewY = s.HeritageView.PanY, HeritageViewZoom = s.HeritageView.Zoom,
                EndingSeen = s.EndingSeen,
                GoldenYearActive = s.GoldenYearActive,
                HarvestsRing = s.Generation.HarvestsRing,
                HarvestsApprentice = s.Generation.HarvestsApprentice,
                HarvestsTractor = s.Generation.HarvestsTractor,
                HarvestsLateFrost = s.Generation.HarvestsLateFrost,
                GoldenHarvests = s.Generation.GoldenHarvests,
                BestCombo = s.Generation.BestCombo,
                TimePlayedSeconds = s.Generation.TimePlayedSeconds,
                YearsTotal = s.Generation.YearsTotal,
                CoinsThisYear = s.CoinsThisYear,
                HarvestsThisYear = s.HarvestsThisYear,
                YearFreshSum = s.YearFreshSum,
                CropsLostThisYear = s.CropsLostThisYear,
                LastGrade = s.LastGrade,
                LastGradeBonus = s.LastGradeBonus,
                LastYearCoins = s.LastYearCoins,
                GoalType = (int)s.Goal.Type,
                GoalTier = s.Goal.Tier,
                GoalTarget = s.Goal.Target,
                GoalProgress = s.Goal.Progress,
                GoalDone = s.Goal.Done,
                GoalReward = s.Goal.Reward,
                Weather = (int)s.Weather,
                WeatherLeft = s.WeatherLeft,
                PlannedWeather = (int)s.PlannedWeather,
                PlannedWeatherTime = s.PlannedWeatherTime,
            };
            for (int i = 0; i < s.PlotArray.Length; i++)
            {
                var p = s.PlotArray[i];
                float crowTimer = 0f;
                foreach (var c in s.CrowList) if (c.Pos == p.Pos) crowTimer = c.Timer;
                d.Plots[i] = new PlotSave { X = p.Pos.X, Y = p.Pos.Y, Tier = p.BedTier, Choice = p.Choice, State = (int)p.State, Progress = p.Progress, HasCrow = p.HasCrow, CrowTimer = crowTimer, Golden = p.IsGolden, RipeAge = p.RipeAge, Kind = (int)p.Kind, LastYearTier = p.LastYearTier, DryTimer = p.DryTimer };
            }
            for (int i = 0; i < s.ApprenticeList.Count; i++)
                d.Apprentices[i] = new ApprenticeSave { X = s.ApprenticeList[i].X, Y = s.ApprenticeList[i].Y, Role = (int)s.ApprenticeList[i].Role };
            d.ScarecrowX = new int[s.ScarecrowList.Count];
            d.ScarecrowY = new int[s.ScarecrowList.Count];
            for (int i = 0; i < s.ScarecrowList.Count; i++)
            {
                d.ScarecrowX[i] = s.ScarecrowList[i].X;
                d.ScarecrowY[i] = s.ScarecrowList[i].Y;
            }
            d.DogCooldown = s.DogCooldown;
            d.PestKind = (int)s.Pest.Kind;
            d.PestX = s.Pest.Pos.X;
            d.PestY = s.Pest.Pos.Y;
            d.PestTimer = s.Pest.Timer;
            d.PestShoo = s.Pest.Shoo;
            d.HenCooldown = s.HenCooldown;
            d.CloverX = s.Luck.CloverPos.X;
            d.CloverY = s.Luck.CloverPos.Y;
            d.CloverLeft = s.Luck.CloverLeft;
            d.StarLeft = s.Luck.StarLeft;
            d.RushLeft = s.Luck.RushLeft;
            d.TraderActive = s.Trader.Active;
            d.TraderTimeLeft = s.Trader.TimeLeft;
            d.TraderPlannedTime = s.Trader.PlannedTime;
            d.TraderSeedPrice = s.Trader.SeedPrice;
            d.TraderRarePrice = s.Trader.RarePrice;
            d.TraderSeedSold = s.Trader.SeedSold;
            d.TraderRareSold = s.Trader.RareSold;
            d.PestCheckTimer = _pestCheckTimer;
            d.BarnStock = s.Barn.Stock;
            d.BarnCount = s.Barn.Count;
            d.BarnStoreShare = s.Barn.StoreShare;
            d.BarnStoreAcc = s.Barn.StoreAcc;
            d.BarnMarketPrice = s.Barn.MarketPrice;
            d.BarnJars = s.Barn.Jars;
            d.AlmanacSpent = s.Generation.AlmanacSpent;
            d.RespecUsed = s.Generation.RespecUsed;
            d.LuckyCheckTimer = _luckyCheckTimer;
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
            g.HarvestsRing = data.HarvestsRing;
            g.HarvestsApprentice = data.HarvestsApprentice;
            g.HarvestsTractor = data.HarvestsTractor;
            g.HarvestsLateFrost = data.HarvestsLateFrost;
            g.GoldenHarvests = data.GoldenHarvests;
            g.BestCombo = data.BestCombo;
            g.TimePlayedSeconds = data.TimePlayedSeconds;
            g.YearsTotal = data.YearsTotal;
            s.CoinsThisYear = data.CoinsThisYear;
            s.HarvestsThisYear = data.HarvestsThisYear;
            s.YearFreshSum = data.YearFreshSum;
            s.CropsLostThisYear = data.CropsLostThisYear;
            s.LastGrade = Math.Max(0, Math.Min(3, data.LastGrade));
            s.LastGradeBonus = data.LastGradeBonus;
            s.LastYearCoins = data.LastYearCoins;
            var goal = s.Goal;
            goal.Type = data.GoalType >= 0 && data.GoalType <= (int)GoalType.Coins ? (GoalType)data.GoalType : GoalType.None;
            goal.Tier = Math.Max(0, Math.Min(config.MaxTier, data.GoalTier));
            goal.Target = data.GoalTarget;
            goal.Progress = data.GoalProgress;
            goal.Done = data.GoalDone;
            goal.Reward = data.GoalReward;
            s.Weather = data.Weather >= 0 && data.Weather <= (int)Weather.Fog ? (Weather)data.Weather : Weather.Clear;
            s.WeatherLeft = data.WeatherLeft;
            s.PlannedWeather = data.PlannedWeather >= 0 && data.PlannedWeather <= (int)Weather.Fog ? (Weather)data.PlannedWeather : Weather.Clear;
            s.PlannedWeatherTime = data.PlannedWeatherTime;
            s.EndingSeen = data.EndingSeen;
            s.GoldenYearActive = data.GoldenYearActive;

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
                plot.BedTier = Math.Max(0, Math.Min(config.MaxTier, ps.Tier));
                plot.Choice = Math.Max(-1, Math.Min(plot.BedTier, ps.Choice));
                plot.State = (PlotState)ps.State;
                plot.Progress = ps.Progress;
                plot.IsGolden = ps.Golden;
                plot.RipeAge = ps.RipeAge;
                plot.Kind = ps.Kind >= 0 && ps.Kind <= (int)PlotKind.Stony ? (PlotKind)ps.Kind : PlotKind.Normal;
                plot.LastYearTier = Math.Max(-1, Math.Min(config.MaxTier, ps.LastYearTier));
                plot.DryTimer = ps.DryTimer;
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
                s.ApprenticeList[i].Role = saved[i].Role == (int)ApprenticeRole.Waterer ? ApprenticeRole.Waterer : ApprenticeRole.Harvester;
            }
            if (data.ScarecrowX != null && data.ScarecrowY != null && data.ScarecrowX.Length == data.ScarecrowY.Length && data.ScarecrowX.Length > 0)
            {
                s.ScarecrowList.Clear();
                for (int i = 0; i < data.ScarecrowX.Length; i++) s.ScarecrowList.Add(new GridPos(data.ScarecrowX[i], data.ScarecrowY[i]));
                sim.SyncScarecrows(); // clamps, and fixes a count that does not match the levels
            }
            s.DogCooldown = Math.Max(0f, data.DogCooldown);
            var pestPos = new GridPos(data.PestX, data.PestY);
            bool pestOk = data.PestKind > 0 && data.PestKind <= (int)PestKind.Locusts && s.InBounds(pestPos);
            s.Pest.Kind = pestOk ? (PestKind)data.PestKind : PestKind.None;
            s.Pest.Pos = s.InBounds(pestPos) ? pestPos : new GridPos(0, 0);
            s.Pest.Timer = pestOk ? data.PestTimer : 0f;
            s.Pest.Shoo = pestOk ? data.PestShoo : 0f;
            s.HenCooldown = Math.Max(0f, data.HenCooldown);
            var cloverPos = new GridPos(data.CloverX, data.CloverY);
            bool cloverOk = data.CloverLeft > 0f && s.InBounds(cloverPos);
            s.Luck.CloverPos = s.InBounds(cloverPos) ? cloverPos : new GridPos(0, 0);
            s.Luck.CloverLeft = cloverOk ? data.CloverLeft : 0f;
            s.Luck.StarLeft = Math.Max(0f, data.StarLeft);
            s.Luck.RushLeft = Math.Max(0f, data.RushLeft);
            s.Trader.Active = data.TraderActive;
            s.Trader.TimeLeft = data.TraderTimeLeft;
            s.Trader.PlannedTime = data.TraderPlannedTime;
            s.Trader.SeedPrice = data.TraderSeedPrice;
            s.Trader.RarePrice = data.TraderRarePrice;
            s.Trader.SeedSold = data.TraderSeedSold;
            s.Trader.RareSold = data.TraderRareSold;
            sim._pestCheckTimer = data.PestCheckTimer;
            s.Barn.Stock = Math.Max(0, data.BarnStock);
            s.Barn.Count = Math.Max(0, data.BarnCount);
            s.Barn.StoreShare = Array.IndexOf(config.StoreShares, data.BarnStoreShare) >= 0 ? data.BarnStoreShare : 0f;
            s.Barn.StoreAcc = Math.Max(0f, Math.Min(1f, data.BarnStoreAcc));
            s.Barn.MarketPrice = data.BarnMarketPrice > 0 ? data.BarnMarketPrice : 1;
            s.Barn.Jars = Math.Max(0, data.BarnJars);
            g.AlmanacSpent = Math.Max(0, data.AlmanacSpent);
            g.RespecUsed = data.RespecUsed;
            sim._luckyCheckTimer = data.LuckyCheckTimer;
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
            s.RingShape = (RingShape)Math.Max(0, Math.Min(2, data.RingShape));
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
            if (State.Phase != Phase.Year || elapsedSeconds <= 0 || double.IsNaN(elapsedSeconds) || elapsedSeconds < Config.OfflineMinSeconds)
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

        /// <summary>Every Heritage node to its max level (smoke / developer panel: reach the ending).</summary>
        public void DebugMaxHeritage()
        {
            foreach (var n in Heritage.Nodes) Heritage.SetLevel(n.Id, n.MaxLevel < 0 ? 1 : n.MaxLevel);
            ResolveStats();
        }

        public void DebugSkipToWinter()
        {
            if (State.Phase == Phase.Year) EnterWinter();
        }

        /// <summary>Jumps the year clock to a season (start of it; Autumn lands just inside the frost warning). Smoke/art hook.</summary>
        public void DebugSetSeason(Season season)
        {
            if (State.Phase != Phase.Year) return;
            float length = State.Stats.YearLength;
            switch (season)
            {
                case Season.Spring: State.YearTime = 0f; break;
                case Season.Summer: State.YearTime = length * 0.4f; break;
                default: State.YearTime = Math.Max(length * 2f / 3f + 0.01f, length - State.Stats.FrostWarningSeconds * 0.9f); break;
            }
            var s = SeasonAt(State.YearTime, length);
            if (s != State.Season)
            {
                State.Season = s;
                SeasonChanged?.Invoke(s);
            }
        }

        /// <summary>Sets the generation number (decor/house progression) without touching seeds or trees. Smoke/art hook.</summary>
        public void DebugSetGeneration(int generation)
        {
            State.Generation.Generation = Math.Max(1, generation);
            GenerationStarted?.Invoke();
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
                if (p.IsStony) continue;
                p.State = PlotState.Ripe;
                p.Progress = 0f;
            }
        }

        /// <summary>Makes a plot fertile, stony or plain, as a field expansion might.</summary>
        public void DebugSetPlotKind(GridPos pos, PlotKind kind)
        {
            if (!State.InBounds(pos)) return;
            var p = State.GetPlot(pos);
            p.Kind = kind;
            if (kind == PlotKind.Stony) p.Reset();
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

        /// <summary>Clears the tap-harvest cooldown. Smoke/test hook.</summary>
        public void DebugClearTapCooldown() => State.TapCooldown = 0f;

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

            if (!State.GoldenYearActive && !State.FrostWarning && State.YearTime >= length - State.Stats.FrostWarningSeconds)
            {
                State.FrostWarning = true;
                MaybeShootingStar();
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
            bool goldenEnded = State.GoldenYearActive;
            if (goldenEnded)
            {
                State.GoldenYearActive = false;
                State.EndingSeen = true;
            }
            // late_frost: nearly-grown crops pay half instead of being lost.
            if (State.Stats.LateFrost)
            {
                foreach (var p in State.PlotArray)
                    if (p.IsRipe || (p.State == PlotState.Wet && p.Progress >= Config.LateFrostThreshold))
                        Harvest(p, HarvestSource.LateFrost, -1);
            }
            GradeYear();
            DrawMarketPrice();
            State.Weather = Weather.Clear;
            State.WeatherLeft = 0f;
            State.PlannedWeather = Weather.Clear;
            // Crop rotation (GDD §2.3 v1.8): each plot remembers what it grew this year; stony ground grew nothing.
            foreach (var p in State.PlotArray) p.LastYearTier = p.IsStony ? -1 : p.Tier;
            State.Phase = Phase.Winter;
            State.Season = Season.Winter;
            State.YearTime = State.Stats.YearLength;
            State.FrostWarning = false;
            State.Ring = null;
            State.Combo = 0;
            ClearCrows();
            ClearEvents();
            foreach (var p in State.PlotArray) p.Reset();
            ResetApprentices();
            ResetTractor();
            ResetCloud();
            State.Greenhouse.SecondsLeftThisWinter = Config.GreenhouseWinterCapSeconds;
            State.Greenhouse.CoinsThisWinter = 0;
            UpdateGreenhouseRate();
            if (goldenEnded)
            {
                // The game continues as a normal generation: the golden field gives way to the Heritage starting field.
                ResolveStats();
                State.PlotArray = null;
                State.GridSize = 0;
                BuildField(State.Stats.TargetGridSize, false);
                ResolveStats();
                GoldenYearEnded?.Invoke();
            }
            SeasonChanged?.Invoke(Season.Winter);
            WinterStarted?.Invoke();
        }

        // ------------------------------------------------------------------ plots

        private CropDef Crop(Plot plot) => Config.Crops[Math.Max(0, Math.Min(Config.MaxTier, plot.Tier))];

        /// <summary>True while the year is in the season this crop likes (GDD §2.3 v1.7).</summary>
        public bool InSeason(int tier)
        {
            if (State.Phase != Phase.Year || tier < 0 || tier > Config.MaxTier) return false;
            return Config.Crops[tier].Likes == State.Season;
        }

        /// <summary>
        /// Everything about where and what a crop is that changes its price (GDD §2.3/§2.4 v1.7–1.8): its liked season,
        /// fertile ground, a rotation from last year, and each side neighbour growing a different crop.
        /// </summary>
        public double PlotValueMultiplier(Plot plot)
        {
            double m = 1;
            if (InSeason(plot.Tier)) m *= Config.InSeasonValue;
            if (plot.Kind == PlotKind.Fertile) m *= Config.FertileValue;
            if (plot.IsRotated) m *= Config.RotationBonus;
            int variety = DifferentNeighbours(plot);
            if (variety > 0) m *= 1 + Config.NeighbourVarietyBonus * variety;
            return m;
        }

        /// <summary>Side neighbours (not diagonals) growing a crop other than this plot's; stony ground grows nothing.</summary>
        public int DifferentNeighbours(Plot plot)
        {
            int n = 0;
            var p = plot.Pos;
            n += Differs(plot, new GridPos(p.X - 1, p.Y));
            n += Differs(plot, new GridPos(p.X + 1, p.Y));
            n += Differs(plot, new GridPos(p.X, p.Y - 1));
            n += Differs(plot, new GridPos(p.X, p.Y + 1));
            return n;
        }

        private int Differs(Plot plot, GridPos at)
        {
            if (!State.InBounds(at)) return 0;
            var o = State.GetPlot(at);
            return !o.IsStony && o.Tier != plot.Tier ? 1 : 0;
        }

        /// <summary>
        /// The seed bag (GDD §2.3 v1.7): plants <paramref name="tier"/> on the plot, any crop up to what its bed can grow,
        /// or -1 to follow the bed again. A different crop replants the plot from Dry; the same crop keeps its progress.
        /// </summary>
        public bool SetPlotCrop(GridPos pos, int tier)
        {
            if (State.Phase != Phase.Year || State.GoldenYearActive || !State.InBounds(pos)) return false;
            var plot = State.GetPlot(pos);
            if (plot.IsStony || tier < -1 || tier > plot.BedTier) return false;
            int before = plot.Tier;
            plot.Choice = tier;
            if (plot.Tier != before) plot.Reset();
            return true;
        }

        private void UpdatePlots(float dt)
        {
            var st = State.Stats;
            var ringHarvest = _offline ? null : RingHarvestTarget();
            foreach (var plot in State.PlotArray)
            {
                bool under = !_offline && State.IsUnderRing(plot.Pos);
                var crop = Crop(plot);
                switch (plot.State)
                {
                    case PlotState.Dry:
                    {
                        if (plot.IsStony)
                        {
                            // GDD §2.4 (v1.8): only the ring clears stones; nothing grows until it has.
                            if (!under || Config.StoneClearSeconds <= 0f) break;
                            plot.Progress += dt / Config.StoneClearSeconds;
                            if (plot.Progress >= 1f)
                            {
                                plot.Kind = PlotKind.Normal;
                                plot.Progress = 0f;
                                PlotCleared?.Invoke(plot.Pos);
                            }
                            break;
                        }
                        float speed = under ? st.RingWaterMult * FlowMult : PassiveWater;
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
                        if (InLocusts(plot.Pos)) break; // GDD §5.5 (v2.1): nothing grows under a swarm
                        float speed = (under ? st.RingGrowMult * FlowMult : PassiveSun * BeeBoost(plot)) * st.SoilMultiplier;
                        if (speed <= 0f)
                        {
                            // GDD §3.2 (v1.9): in a drought a Wet plot nothing is growing dries back out.
                            if (!_offline && InDrought)
                            {
                                plot.DryTimer += dt * (State.Weather == Weather.HeatWave ? 2f : 1f);
                                if (plot.DryTimer >= Config.SummerDryOutSeconds)
                                {
                                    plot.State = PlotState.Dry;
                                    plot.Progress = 0f;
                                    plot.DryTimer = 0f;
                                    PlotDriedOut?.Invoke(plot.Pos);
                                }
                            }
                            break;
                        }
                        plot.DryTimer = 0f;
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
                        // GDD §2.5 (v1.6): a crop left standing slowly loses value, so the ring has to prioritise.
                        if (!_offline && State.Weather != Weather.Fog) plot.RipeAge += dt; // fog keeps them (GDD §5.4 v1.9)
                        if (plot != ringHarvest) break; // GDD §2.1 (v1.4): the ring harvests one plot at a time
                        plot.Progress += st.RingHarvestMult * FlowMult / crop.Harvest * dt;
                        if (plot.Progress >= 1f)
                            Harvest(plot, HarvestSource.Ring, -1);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// GDD §2.1 (v1.4): the ring waters and grows every plot under it, but harvests one Ripe plot at a time —
        /// the one furthest along (a started harvest finishes), ties to the plot nearest the ring centre, then array order.
        /// </summary>
        private Plot RingHarvestTarget()
        {
            if (State.Ring == null) return null;
            var r = State.Ring.Value;
            Plot best = null;
            float bestProgress = -1f, bestDist = float.MaxValue;
            foreach (var p in State.PlotArray)
            {
                if (!p.IsRipe || !State.IsUnderRing(p.Pos)) continue;
                float dx = p.Pos.X - r.X, dy = p.Pos.Y - r.Y;
                float d = dx * dx + dy * dy;
                if (p.Progress > bestProgress || (p.Progress == bestProgress && d < bestDist))
                {
                    best = p;
                    bestProgress = p.Progress;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>1 while the crop is fresh, falling to <see cref="FarmConfig.OverripeMinValue"/> once it has stood too long.</summary>
        public double Freshness(Plot plot)
        {
            if (plot == null || !plot.IsRipe) return 1;
            float over = plot.RipeAge - Config.RipeGraceSeconds;
            if (over <= 0f || Config.OverripeDecaySeconds <= 0f) return 1;
            double t = Math.Min(1.0, over / Config.OverripeDecaySeconds);
            return 1 - t * (1 - Config.OverripeMinValue);
        }

        private void Harvest(Plot plot, HarvestSource source, int apprenticeIndex)
        {
            var st = State.Stats;
            bool golden = plot.IsGolden;
            double coins = Crop(plot).Value * st.CropValueMult * (golden ? Config.GoldenValueMultiplier : 1) * Freshness(plot);
            coins *= PlotValueMultiplier(plot);
            // GDD §3.1 (v1.9): the last rush before frost pays double; late frost's rescue keeps its own price.
            if (State.FrostWarning && source != HarvestSource.LateFrost) coins *= Config.FrostRushValue;
            coins *= LuckMultiplier; // GDD §5.6 (v2.1): the shooting star's rush
            State.YearFreshSum += Freshness(plot);
            switch (source)
            {
                case HarvestSource.Ring:
                    RegisterComboHit();
                    coins *= st.RingBonusMult * (1 + st.RingComboLevel * 0.01 * Math.Min(State.Combo, Config.ComboMaxStacks));
                    coins += ComboMilestoneBonus(plot);
                    break;
                case HarvestSource.Apprentice:
                    coins *= st.ApprenticeYield;
                    break;
                case HarvestSource.LateFrost:
                    coins *= Config.LateFrostValueMultiplier;
                    break;
            }
            bool stored = TryStore(coins);
            if (!stored) AddCoins(coins);
            var gen = State.Generation;
            gen.Harvests++;
            switch (source)
            {
                case HarvestSource.Ring: gen.HarvestsRing++; break;
                case HarvestSource.Apprentice: gen.HarvestsApprentice++; break;
                case HarvestSource.Tractor: gen.HarvestsTractor++; break;
                case HarvestSource.LateFrost: gen.HarvestsLateFrost++; break;
            }
            if (golden) gen.GoldenHarvests++;
            State.HarvestsThisYear++;
            int tier = plot.Tier;
            var goal = State.Goal;
            if (goal.Type == GoalType.HarvestCrop && !goal.Done && tier == goal.Tier) goal.Progress++;
            CheckGoal();
            Replant(plot, source == HarvestSource.Apprentice && st.HelperWater);
            if (plot.HasCrow)
            {
                RemoveCrowAt(plot.Pos);
                CrowScared?.Invoke(new CrowEvent(plot.Pos, 0));
            }
            if (stored) Stored?.Invoke(plot.Pos, coins);
            Harvested?.Invoke(new HarvestEvent(plot.Pos, tier, stored ? 0 : coins, source, apprenticeIndex, golden));
        }

        /// <summary>Same tier back to Dry (or Wet), rolling the golden chance.</summary>
        private void Replant(Plot plot, bool startWet)
        {
            plot.Reset();
            if (startWet) plot.State = PlotState.Wet;
            if (State.GoldenYearActive)
            {
                plot.IsGolden = true;
                return;
            }
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

        /// <summary>A combo that reaches a milestone pays a lump sum in crop values (GDD §2.5 v1.6).</summary>
        private double ComboMilestoneBonus(Plot plot)
        {
            var milestones = Config.ComboMilestones;
            if (milestones == null) return 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (State.Combo != milestones[i]) continue;
                double bonus = Crop(plot).Value * State.Stats.CropValueMult * Index(Config.ComboMilestoneBonus, i);
                ComboMilestone?.Invoke(State.Combo, bonus);
                return bonus;
            }
            return 0;
        }

        private static double Index(double[] table, int i) =>
            table == null || table.Length == 0 ? 0 : table[Math.Max(0, Math.Min(table.Length - 1, i))];

        /// <summary>A ring that keeps moving works a little faster; standing still it settles back (GDD §2.1 v1.6).</summary>
        private void UpdateFlow(float dt)
        {
            var ring = State.Ring;
            if (ring == null || dt <= 0f)
            {
                _hasLastRing = false;
                State.Flow = Math.Max(0f, State.Flow - dt * 2f);
                return;
            }
            float speed = 0f;
            if (_hasLastRing)
            {
                float dx = ring.Value.X - _lastRingX, dy = ring.Value.Y - _lastRingY;
                speed = (float)Math.Sqrt(dx * dx + dy * dy) / dt;
            }
            _lastRingX = ring.Value.X;
            _lastRingY = ring.Value.Y;
            _hasLastRing = true;
            float target = Config.FlowSpeedThreshold <= 0f || speed >= Config.FlowSpeedThreshold ? 1f : 0f;
            float rate = target > State.Flow ? 4f : 1.5f;
            State.Flow += (target - State.Flow) * Math.Min(1f, rate * dt);
        }

        /// <summary>Ring rates including the flow bonus.</summary>
        private float FlowMult => 1f + Config.FlowBonus * State.Flow;

        private float _lastRingX, _lastRingY;
        private bool _hasLastRing;

        private void RegisterComboHit()
        {
            State.Combo = State.ComboTimer <= ComboWindow && State.Combo > 0 ? State.Combo + 1 : 1;
            State.ComboTimer = 0f;
            if (State.Combo > State.Generation.BestCombo) State.Generation.BestCombo = State.Combo;
            var goal = State.Goal;
            if (goal.Type == GoalType.Combo && !goal.Done) goal.Progress = Math.Max(goal.Progress, State.Combo);
        }

        private void UpdateCombo(float dt)
        {
            State.ComboTimer += dt;
            if (State.Combo > 0 && State.ComboTimer > ComboWindow) State.Combo = 0;
        }

        /// <summary>GDD §3.2 (v1.9): the autumn harvest festival gives a streak longer to breathe.</summary>
        private float ComboWindow => Config.ComboWindowSeconds * (State.Season == Season.Autumn ? Config.AutumnComboWindow : 1f);

        /// <summary>Passive watering: Irrigation, faster in spring rain; a storm waters every Dry plot (GDD §3.2/§5.4 v1.9).</summary>
        private float PassiveWater
        {
            get
            {
                float w = State.Stats.IrrigationFactor * (State.Season == Season.Spring ? Config.SpringWaterBoost : 1f);
                return State.Weather == Weather.Storm ? Math.Max(w, Config.StormWaterRate) : w;
            }
        }

        /// <summary>Passive growing: the Sun, gone in a storm, stronger in a heat wave.</summary>
        private float PassiveSun
        {
            get
            {
                float s = State.Stats.SunFactor;
                if (State.Weather == Weather.Storm) return 0f;
                return State.Weather == Weather.HeatWave ? s * Config.HeatWaveSun : s;
            }
        }

        /// <summary>Summer, or a heat wave; never while it storms.</summary>
        private bool InDrought => State.Weather != Weather.Storm && (State.Season == Season.Summer || State.Weather == Weather.HeatWave);

        /// <param name="yearTake">False for the grade's bonus: it is paid on top of the year, not part of what the year earned.</param>
        private void AddCoins(double coins, bool yearTake = true)
        {
            State.Coins += coins;
            if (yearTake) State.CoinsThisYear += coins;
            State.Generation.LifetimeCoinsThisGeneration += coins;
            State.Generation.LifetimeCoinsTotal += coins;
            var goal = State.Goal;
            if (goal.Type == GoalType.Coins && !goal.Done && State.Phase == Phase.Year)
            {
                goal.Progress = State.CoinsThisYear;
                CheckGoal();
            }
        }

        // ------------------------------------------------------------------ goals, grade, weather (GDD §3/§5.4 v1.9)

        /// <summary>Pests, clover, star, rush and a visiting trader all end with the year.</summary>
        private void ClearEvents()
        {
            var pest = State.Pest;
            pest.Kind = PestKind.None;
            pest.Timer = pest.Shoo = 0f;
            var luck = State.Luck;
            luck.CloverLeft = luck.StarLeft = luck.RushLeft = 0f;
            var t = State.Trader;
            if (t.Active) TraderLeft?.Invoke();
            t.Active = false;
            t.TimeLeft = 0f;
            t.PlannedTime = -1f;
        }

        // ------------------------------------------------------------------ barn and market (GDD §3.5 v2.2)

        /// <summary>Sends this harvest to the barn when the share says so and there is room. Deterministic: no RNG.</summary>
        private bool TryStore(double coins)
        {
            var barn = State.Barn;
            if (barn.StoreShare <= 0f || barn.Count >= State.Stats.BarnCapacity || coins <= 0) return false;
            barn.StoreAcc += barn.StoreShare;
            if (barn.StoreAcc < 1f) return false;
            barn.StoreAcc -= 1f;
            barn.Stock += coins;
            barn.Count++;
            return true;
        }

        /// <summary>Picks the share of the harvest the barn keeps (one of <see cref="FarmConfig.StoreShares"/>; needs a barn).</summary>
        public bool SetStoreShare(float share)
        {
            if (State.Stats.BarnCapacity <= 0 && share > 0f) return false;
            if (Array.IndexOf(Config.StoreShares, share) < 0) return false;
            State.Barn.StoreShare = share;
            return true;
        }

        /// <summary>Winter: sells everything in the barn at this winter's price. Counts toward the generation, not the year.</summary>
        public bool SellBarn()
        {
            var barn = State.Barn;
            if (State.Phase != Phase.Winter || barn.Stock <= 0) return false;
            double coins = barn.Stock * barn.MarketPrice;
            barn.Stock = 0;
            barn.Count = 0;
            AddCoins(coins, false);
            BarnSold?.Invoke(coins);
            return true;
        }

        /// <summary>Winter: puts the barn up as preserves, paid at a fixed multiple next spring.</summary>
        public bool MakePreserves()
        {
            var barn = State.Barn;
            if (State.Phase != Phase.Winter || barn.Stock <= 0) return false;
            double jars = barn.Stock * Config.PreserveValue;
            barn.Jars += jars;
            barn.Stock = 0;
            barn.Count = 0;
            PreservesMade?.Invoke(jars);
            return true;
        }

        /// <summary>Each winter draws a market price for the barn (only once there is one, so farms without keep their RNG).</summary>
        private void DrawMarketPrice()
        {
            var barn = State.Barn;
            if (State.Stats.BarnCapacity <= 0 && barn.Stock <= 0) return;
            barn.MarketPrice = Config.MarketMin + _rng.NextDouble() * (Config.MarketMax - Config.MarketMin);
        }

        /// <summary>Spring: stock held over loses a little, and last winter's jars sell.</summary>
        private void OpenBarnForSpring()
        {
            var barn = State.Barn;
            if (barn.Stock > 0)
            {
                barn.Stock *= 1 - Config.BarnSpoil;
                barn.Count = (int)Math.Round(barn.Count * (1 - Config.BarnSpoil));
            }
            if (barn.Jars > 0)
            {
                double jars = barn.Jars;
                barn.Jars = 0;
                AddCoins(jars);
                PreservesSold?.Invoke(jars);
            }
        }

        private void ResetBarn()
        {
            var barn = State.Barn;
            barn.Stock = 0;
            barn.Count = 0;
            barn.StoreAcc = 0f;
            barn.Jars = 0;
            barn.MarketPrice = 1;
        }

        // ------------------------------------------------------------------ Almanac respec (GDD §6.3 v2.2)

        /// <summary>Once a generation, in winter, with something bought and paid for (a respec that refunds nothing is no offer).</summary>
        public bool CanRespec => State.Phase == Phase.Winter && !State.Generation.RespecUsed && Almanac.Levels.Count > 0 && State.Generation.AlmanacSpent > 0;

        /// <summary>
        /// Resets the Almanac and refunds what it cost this generation. What the purchases built goes with them: beds go
        /// back to carrots and the field back to the size the trees now give (the plots nearest the house stay).
        /// </summary>
        public bool RespecAlmanac()
        {
            if (!CanRespec) return false;
            var g = State.Generation;
            double refund = g.AlmanacSpent;
            Almanac.Reset();
            foreach (var p in State.PlotArray)
            {
                p.BedTier = 0;
                p.Choice = -1;
            }
            ResolveStats();
            if (State.GridSize > State.Stats.TargetGridSize)
            {
                BuildField(State.Stats.TargetGridSize, false);
                ResolveStats();
                FieldExpanded?.Invoke();
            }
            State.Coins += refund;
            g.AlmanacSpent = 0;
            g.RespecUsed = true;
            Respecced?.Invoke(refund);
            return true;
        }

        private void CheckGoal()
        {
            var goal = State.Goal;
            if (!goal.Active || goal.Done || goal.Progress < goal.Target) return;
            goal.Done = true; // before paying: the reward's coins must not re-enter here
            AddCoins(goal.Reward);
            GoalCompleted?.Invoke(goal);
        }

        /// <summary>Each year from <see cref="FarmConfig.GoalFirstYear"/> sets one goal, scaled to the farm and last year.</summary>
        private void SetYearGoal()
        {
            var goal = State.Goal;
            goal.Clear();
            if (State.Year < Config.GoalFirstYear || State.GoldenYearActive) return;
            int kinds = State.LastYearCoins > 0 ? 3 : 2;
            var type = (GoalType)(1 + Math.Min(kinds - 1, (int)(_rng.NextDouble() * kinds)));
            goal.Type = type;
            switch (type)
            {
                case GoalType.HarvestCrop:
                {
                    int top = 0, plots = 0;
                    foreach (var p in State.PlotArray)
                    {
                        if (p.IsStony) continue;
                        plots++;
                        top = Math.Max(top, Math.Min(p.BedTier, State.Stats.MaxTierUnlocked));
                    }
                    goal.Tier = Math.Min(top, (int)(_rng.NextDouble() * (top + 1)));
                    goal.Target = Math.Max(1, Math.Ceiling(Config.GoalHarvestsPerPlot * Math.Max(1, plots)));
                    break;
                }
                case GoalType.Combo:
                    goal.Target = Math.Min(Config.GoalComboMax, Config.GoalComboBase + Config.GoalComboPerYear * (State.Year - 1));
                    break;
                case GoalType.Coins:
                    goal.Target = Math.Ceiling(State.LastYearCoins * Config.GoalCoinsGrowth);
                    break;
            }
            goal.Reward = Math.Max(Config.GoalMinReward, State.LastYearCoins * Config.GoalRewardShare);
            GoalSet?.Invoke(goal);
        }

        /// <summary>
        /// Stars for the finished year from how fresh its crops were (crops lost to crows count as 0), and the share of
        /// the year's coins they pay on top.
        /// </summary>
        private void GradeYear()
        {
            int counted = State.HarvestsThisYear + State.CropsLostThisYear;
            double quality = counted > 0 ? State.YearFreshSum / counted : 0;
            int stars = 1;
            var t = Config.GradeThresholds;
            if (counted > 0 && t != null && t.Length >= 2)
                stars = quality >= t[1] ? 3 : quality >= t[0] ? 2 : 1;
            var b = Config.GradeBonusByStars;
            double bonus = b != null && stars < b.Length ? State.CoinsThisYear * b[stars] : 0;
            if (bonus > 0) AddCoins(bonus, false);
            State.LastGrade = stars;
            State.LastGradeBonus = bonus;
            State.LastYearCoins = State.CoinsThisYear;
            YearGraded?.Invoke(stars, bonus);
        }

        /// <summary>From <see cref="FarmConfig.WeatherFirstYear"/>, a year may bring one spell: a storm any time, fog in spring, a heat wave in summer.</summary>
        private void PlanWeather()
        {
            State.Weather = Weather.Clear;
            State.WeatherLeft = 0f;
            State.PlannedWeather = Weather.Clear;
            State.PlannedWeatherTime = 0f;
            if (State.Year < Config.WeatherFirstYear || State.GoldenYearActive) return;
            if (_rng.NextDouble() >= Config.WeatherChance) return;
            var kind = (Weather)(1 + Math.Min(2, (int)(_rng.NextDouble() * 3)));
            float length = State.Stats.YearLength;
            float dur = WeatherSeconds(kind);
            float from, to;
            switch (kind)
            {
                case Weather.Fog: from = 0.02f * length; to = length / 3f - dur; break;
                case Weather.HeatWave: from = length / 3f; to = 2f * length / 3f - dur; break;
                default: from = 0.15f * length; to = 0.85f * length - dur; break;
            }
            State.PlannedWeather = kind;
            State.PlannedWeatherTime = from + (float)_rng.NextDouble() * Math.Max(0f, to - from);
        }

        private float WeatherSeconds(Weather kind) =>
            kind == Weather.Storm ? Config.StormSeconds : kind == Weather.HeatWave ? Config.HeatWaveSeconds : kind == Weather.Fog ? Config.FogSeconds : 0f;

        private void UpdateWeather(float dt)
        {
            if (State.Weather != Weather.Clear)
            {
                State.WeatherLeft -= dt;
                if (State.WeatherLeft > 0f) return;
                State.Weather = Weather.Clear;
                State.WeatherLeft = 0f;
                WeatherChanged?.Invoke(Weather.Clear);
                return;
            }
            if (State.PlannedWeather == Weather.Clear || State.YearTime < State.PlannedWeatherTime) return;
            State.Weather = State.PlannedWeather;
            State.WeatherLeft = WeatherSeconds(State.Weather);
            State.PlannedWeather = Weather.Clear;
            WeatherChanged?.Invoke(State.Weather);
        }

        /// <summary>Starts a weather spell now (tests and the UI tour).</summary>
        public void DebugStartWeather(Weather kind)
        {
            if (State.Phase != Phase.Year) return;
            State.Weather = kind;
            State.WeatherLeft = WeatherSeconds(kind);
            WeatherChanged?.Invoke(kind);
        }

        /// <summary>Sets this year's goal directly (tests and the UI tour).</summary>
        public void DebugSetGoal(GoalType type, double target, int tier = 0, double reward = 10)
        {
            var goal = State.Goal;
            goal.Clear();
            goal.Type = type;
            goal.Target = target;
            goal.Tier = tier;
            goal.Reward = reward;
            if (type == GoalType.Coins) goal.Progress = State.CoinsThisYear;
            GoalSet?.Invoke(goal);
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
                if (a.HasTarget && !Wants(a, State.GetPlot(a.Target)))
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
                        if (!Wants(a, p) || IsTargetedByOther(p.Pos, a)) continue;
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
                        var worked = State.GetPlot(a.Target);
                        if (a.Role == ApprenticeRole.Waterer)
                        {
                            worked.State = PlotState.Wet;
                            worked.Progress = 0f;
                            worked.DryTimer = 0f;
                            PlotWatered?.Invoke(worked.Pos);
                        }
                        else Harvest(worked, HarvestSource.Apprentice, a.Index);
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

        /// <summary>A harvester wants Ripe plots; a waterer wants Dry ones it can water (GDD §4.2 v2.0).</summary>
        private static bool Wants(ApprenticeState a, Plot p) =>
            a.Role == ApprenticeRole.Waterer ? p.State == PlotState.Dry && !p.IsStony : p.IsRipe;

        /// <summary>Switches an apprentice between harvesting and watering; it drops whatever it was doing.</summary>
        public bool SetApprenticeRole(int index, ApprenticeRole role)
        {
            if (index < 0 || index >= State.ApprenticeList.Count) return false;
            var a = State.ApprenticeList[index];
            if (a.Role == role) return false;
            a.Role = role;
            a.HasTarget = false;
            a.IsHarvesting = false;
            a.HarvestProgress = 0f;
            return true;
        }

        // ------------------------------------------------------------------ scarecrows and bees (GDD §4.3/§5.1 v2.0)

        /// <summary>True when a placed scarecrow keeps crows off this plot.</summary>
        public bool IsGuarded(GridPos plot)
        {
            float r2 = Config.ScarecrowRadius * Config.ScarecrowRadius;
            foreach (var c in State.ScarecrowList)
            {
                float dx = plot.X - (c.X - 0.5f), dy = plot.Y - (c.Y - 0.5f);
                if (dx * dx + dy * dy <= r2) return true;
            }
            return false;
        }

        /// <summary>Moves a scarecrow to a plot corner (0..GridSize on both axes); not onto another one, not in the Heritage phase.</summary>
        public bool MoveScarecrow(int index, GridPos corner)
        {
            var list = State.ScarecrowList;
            if (State.Phase == Phase.Heritage || index < 0 || index >= list.Count) return false;
            int g = State.GridSize;
            if (corner.X < 0 || corner.Y < 0 || corner.X > g || corner.Y > g) return false;
            for (int i = 0; i < list.Count; i++) if (i != index && list[i] == corner) return false;
            if (list[index] == corner) return false;
            list[index] = corner;
            ScarecrowMoved?.Invoke(index);
            return true;
        }

        /// <summary>One scarecrow per level: new ones stand where they guard the most plots nothing guards yet.</summary>
        private void SyncScarecrows()
        {
            var list = State.ScarecrowList;
            int want = Math.Max(0, State.Stats.ScarecrowCount);
            int g = State.GridSize;
            for (int i = 0; i < list.Count; i++)
                list[i] = new GridPos(Math.Max(0, Math.Min(g, list[i].X)), Math.Max(0, Math.Min(g, list[i].Y)));
            while (list.Count > want) list.RemoveAt(list.Count - 1);
            while (list.Count < want)
            {
                list.Add(BestScarecrowCorner());
                ScarecrowMoved?.Invoke(list.Count - 1);
            }
        }

        private GridPos BestScarecrowCorner()
        {
            int g = State.GridSize;
            float r2 = Config.ScarecrowRadius * Config.ScarecrowRadius, mid = g / 2f;
            var best = new GridPos(g / 2, g / 2);
            int bestGain = -1;
            float bestDist = float.MaxValue;
            for (int cy = 0; cy <= g; cy++)
            for (int cx = 0; cx <= g; cx++)
            {
                var c = new GridPos(cx, cy);
                if (State.ScarecrowList.Contains(c)) continue;
                int gain = 0;
                foreach (var p in State.PlotArray)
                {
                    float dx = p.Pos.X - (cx - 0.5f), dy = p.Pos.Y - (cy - 0.5f);
                    if (dx * dx + dy * dy <= r2 && !IsGuarded(p.Pos)) gain++;
                }
                float d = (cx - mid) * (cx - mid) + (cy - mid) * (cy - mid);
                if (gain > bestGain || (gain == bestGain && d < bestDist))
                {
                    best = c;
                    bestGain = gain;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>The beehive speeds the Sun on the right-most columns, by the sunflowers.</summary>
        private float BeeBoost(Plot p) =>
            State.Stats.Beehive && p.Pos.X >= State.GridSize - Config.BeeColumns ? Config.BeeSunBoost : 1f;

        /// <summary>Is this plot one the bees work?</summary>
        public bool IsBeePlot(GridPos pos) => State.Stats.Beehive && pos.X >= State.GridSize - Config.BeeColumns;

        // ------------------------------------------------------------------ tractor by hand (GDD §4.4 v2.0)

        /// <summary>0..1: how far the tractor's timer has run (0 while sweeping or not owned).</summary>
        public float TractorCharge
        {
            get
            {
                var t = State.Tractor;
                if (!t.Owned || t.Sweeping) return 0f;
                float interval = TractorInterval();
                return interval <= 0f ? 1f : Math.Max(0f, Math.Min(1f, 1f - t.TimeToNextSweep / interval));
            }
        }

        /// <summary>Sends the tractor now, down the row with the most Ripe plots, once it is at least half charged.</summary>
        public bool TriggerTractor()
        {
            var t = State.Tractor;
            if (State.Phase != Phase.Year || !t.Owned || t.Sweeping || TractorCharge < Config.TractorManualReady) return false;
            return TryStartSweep();
        }

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
            // GDD §4.3 (v2.0): the farm dog goes for a crow that has settled, then rests. No bounty: that is the tap's.
            if (State.DogCooldown > 0f) State.DogCooldown = Math.Max(0f, State.DogCooldown - dt);
            if (State.Stats.FarmDog && State.DogCooldown <= 0f)
            {
                // The crow that has sat longest (ties: lowest row, then column), so the choice does not hang on list order.
                int pick = -1;
                for (int i = 0; i < crows.Count; i++)
                {
                    if (crows[i].Timer < Config.DogReactSeconds) continue;
                    if (pick < 0 || crows[i].Timer > crows[pick].Timer || (crows[i].Timer == crows[pick].Timer &&
                        (crows[i].Pos.Y < crows[pick].Pos.Y || (crows[i].Pos.Y == crows[pick].Pos.Y && crows[i].Pos.X < crows[pick].Pos.X))))
                        pick = i;
                }
                if (pick >= 0)
                {
                    var pos = crows[pick].Pos;
                    State.GetPlot(pos).HasCrow = false;
                    crows.RemoveAt(pick);
                    State.DogCooldown = Config.DogCooldownSeconds;
                    DogChased?.Invoke(pos);
                    CrowScared?.Invoke(new CrowEvent(pos, 0));
                }
            }
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
                    State.Combo = 0; // a crop lost to a crow breaks the streak
                    State.CropsLostThisYear++;
                    CrowAte?.Invoke(new CrowEvent(crow.Pos));
                }
            }

            if (State.Year < Config.CrowFirstYear || State.GoldenYearActive) return;
            if (State.Weather == Weather.Storm || State.Weather == Weather.Fog) return; // no crow flies in it

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
                if (p.IsRipe && !p.HasCrow && !State.IsUnderRing(p.Pos) && !IsGuarded(p.Pos)) _scratch.Add(p);
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

        /// <summary>Golden Year field: the largest field, every plot the top tier (Golden Wheat), golden, just planted (Wet).</summary>
        private void PlantGoldenField()
        {
            State.PlotArray = null;
            State.GridSize = 0;
            BuildField(Config.MaxGridSize, false);
            foreach (var p in State.PlotArray)
            {
                p.BedTier = Config.MaxTier;
                p.Choice = -1;
                p.IsGolden = true;
                p.State = PlotState.Wet;
                p.Progress = 0f;
            }
        }

        /// <summary>GDD §2.4 (v1.8): the ground a field expansion adds may be fertile or stony; the starting field never is.</summary>
        private void RollNewGround(int oldSize)
        {
            foreach (var p in State.PlotArray)
            {
                if (p.Pos.X < oldSize && p.Pos.Y < oldSize) continue;
                double r = _rng.NextDouble();
                if (r < Config.StonyChance)
                {
                    p.Kind = PlotKind.Stony;
                    p.Reset(); // stones first: a new plot that would have started wet waits for the clearing
                }
                else if (r < Config.StonyChance + Config.FertileChance) p.Kind = PlotKind.Fertile;
            }
        }

        private Plot FindLowestUpgradablePlot(Plot exclude)
        {
            int cap = State.Stats.MaxTierUnlocked;
            Plot lowest = null;
            foreach (var p in State.PlotArray) // row-major: first lowest wins ties
                if (p != exclude && p.BedTier < cap && (lowest == null || p.BedTier < lowest.BedTier)) lowest = p;
            return lowest;
        }

        private void ApplyPurchase(SkillNode node)
        {
            switch (node.Effect)
            {
                case EffectType.ExpandField:
                {
                    int oldSize = State.GridSize;
                    BuildField(Math.Min(Config.MaxGridSize, State.GridSize + 1), State.Stats.FertileStart);
                    RollNewGround(oldSize);
                    FieldExpanded?.Invoke();
                    break;
                }
                case EffectType.UpgradePlot:
                {
                    var plot = FindLowestUpgradablePlot(null);
                    if (plot != null) plot.BedTier++;
                    if (State.Stats.BulkUpgrade)
                    {
                        var second = FindLowestUpgradablePlot(null);
                        if (second != null) second.BedTier++;
                    }
                    break;
                }
            }
        }

        private void ResolveStats()
        {
            State.Stats = StatResolver.Resolve(Config, Almanac.Levels, Heritage.Levels, Almanac.Nodes, Heritage.Nodes);
            if (State.GoldenYearActive)
            {
                State.Stats.YearLength = Config.GoldenYearSeconds;
                State.Stats.FrostWarningSeconds = 0f;
            }
            State.RingRadius = _ringRadiusOverride ?? State.Stats.RingRadius;
            bool ownedBefore = State.Tractor.Owned;
            State.Tractor.Owned = State.Stats.TractorLevel > 0;
            if (State.Tractor.Owned && !ownedBefore) ResetTractor();
            if (State.PlotArray != null)
            {
                SyncApprenticeCount();
                UpdateGreenhouseRate();
                SyncScarecrows();
            }
        }
    }
}
